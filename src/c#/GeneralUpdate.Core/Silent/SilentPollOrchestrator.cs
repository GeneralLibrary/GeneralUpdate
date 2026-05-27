using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Strategy;

namespace GeneralUpdate.Core.Silent;

/// <summary>
/// Silent update poll orchestrator — periodically checks for updates,
/// downloads them in the background, and defers application to process exit.
/// Follows the same AppType-split pattern as <see cref="ClientUpdateStrategy"/>:
///   - Upgrade (AppType=2) packages are applied in place during the poll cycle
///     (they target UpdatePath, not the running app's InstallPath).
///   - Client (AppType=1) packages are deferred — stored in ProcessInfo and
///     handed off to the Upgrade process on exit.
/// </summary>
public class SilentPollOrchestrator : IDisposable
{
    private readonly GlobalConfigInfo _configInfo;
    private readonly SilentOptions _options;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private int _prepared;
    private int _updaterStarted;
    private IUpdateHooks? _hooks;
    private IUpdateReporter? _reporter;
    private Configuration.ProcessInfo? _preparedProcessInfo;
    private List<VersionInfo> _clientVersions = new();

    public SilentPollOrchestrator(GlobalConfigInfo configInfo, SilentOptions options)
    {
        _configInfo = configInfo ?? throw new ArgumentNullException(nameof(configInfo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public SilentPollOrchestrator WithHooks(IUpdateHooks? hooks) { _hooks = hooks; return this; }
    public SilentPollOrchestrator WithReporter(IUpdateReporter? reporter) { _reporter = reporter; return this; }

    public Task StartAsync()
    {
        GeneralTracer.Info($"SilentPollOrchestrator: starting. PollInterval={_options.PollInterval.TotalMinutes}min");

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        _cts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token));

        _pollingTask.ContinueWith(task =>
        {
            if (task.Exception != null)
                GeneralTracer.Error("SilentPollOrchestrator: polling exception.", task.Exception);
        }, TaskContinuationOptions.OnlyOnFaulted);

        return Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        GeneralTracer.Info("SilentPollOrchestrator: polling loop started.");
        while (!token.IsCancellationRequested && Volatile.Read(ref _prepared) == 0)
        {
            try
            {
                await PrepareUpdateIfNeededAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("SilentPollOrchestrator: poll cycle failed.", ex);
            }

            if (Volatile.Read(ref _prepared) == 1) break;

            try { await Task.Delay(_options.PollInterval, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PrepareUpdateIfNeededAsync(CancellationToken token)
    {
        GeneralTracer.Info($"SilentPollOrchestrator: checking for updates. Url={_configInfo.UpdateUrl}");

        var downloadSource = new HttpDownloadSource(
            _configInfo.UpdateUrl,
            _configInfo.ClientVersion,
            _configInfo.UpgradeClientVersion,
            _configInfo.AppSecretKey,
            GetPlatform(),
            _configInfo.ProductId,
            _configInfo.Scheme,
            _configInfo.Token);

        var sourceResult = await downloadSource.ListAsync(token).ConfigureAwait(false);
        var plan = DownloadPlanBuilder.Build(sourceResult.Assets, _configInfo.ClientVersion);

        if (!plan.HasAssets)
        {
            GeneralTracer.Info("SilentPollOrchestrator: no update available.");
            return;
        }

        var latestVersion = plan.Assets.LastOrDefault()?.Version;
        if (CheckFail(latestVersion))
        {
            GeneralTracer.Warn($"SilentPollOrchestrator: version {latestVersion} is a known-failed upgrade, skipping.");
            return;
        }

        // Hooks: allow cancellation before starting update
        var updateCtx = new UpdateContext(
            _configInfo.MainAppName ?? _configInfo.UpdateAppName,
            _configInfo.InstallPath,
            _configInfo.ClientVersion,
            latestVersion,
            AppType.Client);

        if (_hooks != null)
        {
            try
            {
                if (!await _hooks.OnBeforeUpdateAsync(updateCtx).ConfigureAwait(false))
                {
                    GeneralTracer.Info("SilentPollOrchestrator: update cancelled by hooks.");
                    return;
                }
            }
            catch (Exception ex) { GeneralTracer.Warn($"Hook OnBeforeUpdateAsync failed: {ex.Message}"); }
        }

        InitBlackList();

        _configInfo.LastVersion = latestVersion;
        _configInfo.TempPath = StorageManager.GetTempDirectory("silent_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        // Backup
        if (_configInfo.BackupEnabled != false)
        {
            StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
                _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);
        }

        // Reporter: update started
        if (_reporter != null)
        {
            try { await _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Updating, 1), token).ConfigureAwait(false); }
            catch (Exception ex) { GeneralTracer.Warn($"Reporter UpdateStarted failed: {ex.Message}"); }
        }

        // Download all packages in background
        GeneralTracer.Info($"SilentPollOrchestrator: downloading {plan.Assets.Count} asset(s).");
        var httpClient = GeneralUpdate.Core.Network.HttpClientProvider.Shared;
        try
        {
            var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(httpClient);
            var report = await orchestrator.ExecuteAsync(plan, _configInfo.TempPath, token: token).ConfigureAwait(false);
            GeneralTracer.Info($"SilentPollOrchestrator: download complete. Success={report.SuccessCount}, Failed={report.FailedCount}");

            if (report.FailedCount > 0)
            {
                GeneralTracer.Error($"SilentPollOrchestrator: download had {report.FailedCount} failures, aborting update.");
                return;
            }

            if (_hooks != null)
            {
                try
                {
                    var downloadCtx = new DownloadContext(
                        plan.Assets.FirstOrDefault()?.Name ?? "update", latestVersion ?? "",
                        report.TotalBytes, report.TotalDuration,
                        _configInfo.TempPath, report.FailedCount == 0);
                    await _hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
                }
                catch (Exception ex) { GeneralTracer.Warn($"Hook OnDownloadCompletedAsync failed: {ex.Message}"); }
            }
            if (_reporter != null)
            {
                try { await _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Updating, 1), token).ConfigureAwait(false); }
                catch (Exception ex) { GeneralTracer.Warn($"Reporter DownloadCompleted failed: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: download failed.", ex);
            TryReportError(updateCtx, ex);
            return;
        }

        // Split packages by AppType — mirrors ClientUpdateStrategy
        var downloadVersions = plan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = _configInfo.Format.ToExtension(),
            AppType = a.AppType ?? (int)AppType.Client
        }).ToList();

        var upgradeVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Upgrade).ToList();
        _clientVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Client).ToList();
        GeneralTracer.Info($"SilentPollOrchestrator: Upgrade packages={upgradeVersions.Count}, Client packages={_clientVersions.Count}");

        // Apply Upgrade packages in place — safe because they target UpdatePath
        if (upgradeVersions.Count > 0)
        {
            GeneralTracer.Info("SilentPollOrchestrator: applying Upgrade packages.");
            try
            {
                _configInfo.UpdateVersions = upgradeVersions;
                var strategy = CreateStrategy();
                strategy.Create(_configInfo);
                await strategy.ExecuteAsync().ConfigureAwait(false);
                GeneralTracer.Info("SilentPollOrchestrator: Upgrade packages applied.");
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("SilentPollOrchestrator: Upgrade package application failed.", ex);
                TryReportError(updateCtx, ex);
                return;
            }
        }

        // Build ProcessInfo with Client packages for IPC delivery on process exit
        if (_clientVersions.Count > 0)
        {
            _configInfo.LaunchClientAfterUpdate = _options.LaunchClientAfterUpdate;
            _preparedProcessInfo = ConfigurationMapper.MapToProcessInfo(
                _configInfo, _clientVersions,
                _configInfo.BlackFormats ?? BlackListDefaults.DefaultBlackFormats,
                _configInfo.BlackFiles ?? BlackListDefaults.DefaultBlackFiles,
                _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);
            _configInfo.ProcessInfo = JsonSerializer.Serialize(_preparedProcessInfo, ProcessInfoJsonContext.Default.ProcessInfo);

            Interlocked.Exchange(ref _prepared, 1);
            GeneralTracer.Info("SilentPollOrchestrator: update prepared, waiting for process exit.");
        }
        else if (upgradeVersions.Count > 0)
        {
            // Upgrade-only: packages already applied, no handoff needed
            Interlocked.Exchange(ref _prepared, 1);
            GeneralTracer.Info("SilentPollOrchestrator: upgrade-only update applied, no client handoff needed.");
        }

        if (_hooks != null)
        {
            try { await _hooks.OnAfterUpdateAsync(updateCtx).ConfigureAwait(false); }
            catch (Exception ex) { GeneralTracer.Warn($"Hook OnAfterUpdateAsync failed: {ex.Message}"); }
        }
        if (_reporter != null)
        {
            try { await _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Success, 1), token).ConfigureAwait(false); }
            catch (Exception ex) { GeneralTracer.Warn($"Reporter UpdateApplied failed: {ex.Message}"); }
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _prepared) != 1 || Interlocked.Exchange(ref _updaterStarted, 1) == 1) return;

        try
        {
            // Resolve updater location — prefers UpdatePath, falls back to InstallPath
            var updaterDir = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath)
                ? (Path.IsPathRooted(_configInfo.UpdatePath)
                    ? _configInfo.UpdatePath
                    : Path.Combine(_configInfo.InstallPath, _configInfo.UpdatePath))
                : _configInfo.InstallPath;
            var updaterPath = Path.Combine(updaterDir, _configInfo.UpdateAppName);

            if (!File.Exists(updaterPath))
            {
                GeneralTracer.Warn($"SilentPollOrchestrator: updater not found at {updaterPath}, cannot launch.");
                return;
            }

            // Send ProcessInfo with Client packages via encrypted file IPC BEFORE starting Upgrade
            if (_preparedProcessInfo != null && _clientVersions.Count > 0)
            {
                new EncryptedFileProcessInfoProvider().Send(_preparedProcessInfo);
                GeneralTracer.Info($"SilentPollOrchestrator: ProcessInfo sent with {_clientVersions.Count} Client package(s).");
            }

            Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = updaterPath });
            GeneralTracer.Info($"SilentPollOrchestrator: launched updater {updaterPath}");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: OnProcessExit failed.", ex);
        }
    }

    private void TryReportError(UpdateContext ctx, Exception ex)
    {
        if (_hooks != null)
        {
            try { _hooks.OnUpdateErrorAsync(ctx, ex).GetAwaiter().GetResult(); }
            catch (Exception hookEx) { GeneralTracer.Warn($"Hook OnUpdateErrorAsync failed: {hookEx.Message}"); }
        }
        if (_reporter != null)
        {
            try { _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Failure, 1)).GetAwaiter().GetResult(); }
            catch (Exception reporterEx) { GeneralTracer.Warn($"Reporter UpdateFailed failed: {reporterEx.Message}"); }
        }
    }

    private void InitBlackList()
    {
        var effectiveConfig = new BlackListConfig(
            _configInfo.BlackFiles?.Count > 0 ? _configInfo.BlackFiles : BlackListDefaults.DefaultBlackFiles,
            _configInfo.BlackFormats?.Count > 0 ? _configInfo.BlackFormats : BlackListDefaults.DefaultBlackFormats,
            _configInfo.SkipDirectorys?.Count > 0 ? _configInfo.SkipDirectorys : BlackListDefaults.DefaultSkipDirectories
        );
        StorageManager.BlackListMatcher = new DefaultBlackListMatcher(effectiveConfig);
    }

    private static IStrategy CreateStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return new MacStrategy();
        throw new PlatformNotSupportedException();
    }

    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    private static bool CheckFail(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail)) return false;
        return new Version(fail) >= new Version(version);
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

public sealed class SilentOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to launch the client application after the upgrade process finishes.
    /// Default: true (current behavior). Set to false when the caller wants to
    /// manually control restart timing (e.g. maintenance windows, orchestrated rollouts).
    /// </summary>
    public bool LaunchClientAfterUpdate { get; set; } = true;
}
