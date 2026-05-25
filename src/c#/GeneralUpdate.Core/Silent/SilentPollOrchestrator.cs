using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Strategy;

namespace GeneralUpdate.Core.Silent;

/// <summary>
/// Silent update poll orchestrator — periodically checks for updates,
/// downloads them in the background, and optionally auto-installs.
/// Replaces the legacy <c>SilentUpdateMode</c> class.
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

    public SilentPollOrchestrator(GlobalConfigInfo configInfo, SilentOptions options)
    {
        _configInfo = configInfo ?? throw new ArgumentNullException(nameof(configInfo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Inject hooks and reporter for lifecycle callbacks during silent polling.</summary>
    public SilentPollOrchestrator WithHooks(IUpdateHooks? hooks) { _hooks = hooks; return this; }
    public SilentPollOrchestrator WithReporter(IUpdateReporter? reporter) { _reporter = reporter; return this; }

    /// <summary>Start background polling loop.</summary>
    public Task StartAsync()
    {
        GeneralTracer.Info($"SilentPollOrchestrator: starting. PollInterval={_options.PollInterval.TotalMinutes}min, AutoInstall={_options.AutoInstall}");

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

    /// <summary>Stop polling and cancel any in-flight operation.</summary>
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

        // Use the new download source
        var downloadSource = new HttpDownloadSource(
            _configInfo.UpdateUrl,
            _configInfo.ClientVersion,
            _configInfo.UpgradeClientVersion,
            _configInfo.AppSecretKey,
            GetPlatform(),
            _configInfo.ProductId,
            _configInfo.Scheme,
            _configInfo.Token);

        var assets = await downloadSource.ListAsync(token).ConfigureAwait(false);
        var plan = DownloadPlanBuilder.Build(assets, _configInfo.ClientVersion);

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

        // ═══ Hooks: allow cancellation before starting update ═══
        var updateCtx = new UpdateContext(
            _configInfo.MainAppName ?? _configInfo.AppName,
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

        // Configure for update
        BlackListManager.Instance?.AddBlackFiles(_configInfo.BlackFiles);
        BlackListManager.Instance?.AddBlackFormats(_configInfo.BlackFormats);
        BlackListManager.Instance?.AddSkipDirectorys(_configInfo.SkipDirectorys);

        _configInfo.LastVersion = latestVersion;
        _configInfo.UpdateVersions = new List<VersionInfo>(); // legacy compat
        _configInfo.TempPath = StorageManager.GetTempDirectory("silent_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        // Backup
        StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
            BlackListManager.Instance.SkipDirectorys);

        // Build ProcessInfo
        var processInfo = ConfigurationMapper.MapToProcessInfo(
            _configInfo, new List<VersionInfo>(),
            BlackListManager.Instance.BlackFormats.ToList(),
            BlackListManager.Instance.BlackFiles.ToList(),
            BlackListManager.Instance.SkipDirectorys.ToList());
        _configInfo.ProcessInfo = JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);

        // ═══ Reporter: update started ═══
        var startTime = DateTimeOffset.UtcNow;
        if (_reporter != null)
        {
            try
            {
                await _reporter.ReportAsync(new UpdateReport(
                    updateCtx.AppName, updateCtx.CurrentVersion, updateCtx.TargetVersion,
                    UpdateEvent.UpdateStarted, AppType.Client, startTime), token).ConfigureAwait(false);
            }
            catch (Exception ex) { GeneralTracer.Warn($"Reporter UpdateStarted failed: {ex.Message}"); }
        }

        // Download using new orchestrator
        GeneralTracer.Info($"SilentPollOrchestrator: downloading {plan.Assets.Count} asset(s).");
        var httpClient = new System.Net.Http.HttpClient();
        var downloadSuccessCount = 0;
        var downloadFailedCount = 0;
        var downloadTotalBytes = 0L;
        var downloadElapsed = TimeSpan.Zero;
        try
        {
            var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(httpClient);
            var report = await orchestrator.ExecuteAsync(plan, _configInfo.TempPath, token: token).ConfigureAwait(false);
            downloadSuccessCount = report.SuccessCount;
            downloadFailedCount = report.FailedCount;
            downloadTotalBytes = report.TotalBytes;
            downloadElapsed = report.TotalDuration;
            GeneralTracer.Info($"SilentPollOrchestrator: download complete. Success={downloadSuccessCount}, Failed={downloadFailedCount}");

            // ═══ Hooks + Reporter: download completed ═══
            if (_hooks != null)
            {
                try
                {
                    var downloadCtx = new DownloadContext(
                        plan.Assets.FirstOrDefault()?.Name ?? "update", latestVersion ?? "",
                        downloadTotalBytes, downloadElapsed,
                        _configInfo.TempPath, downloadFailedCount == 0);
                    await _hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
                }
                catch (Exception ex) { GeneralTracer.Warn($"Hook OnDownloadCompletedAsync failed: {ex.Message}"); }
            }

            if (_reporter != null)
            {
                try
                {
                    await _reporter.ReportAsync(new UpdateReport(
                        updateCtx.AppName, updateCtx.CurrentVersion, updateCtx.TargetVersion,
                        UpdateEvent.DownloadCompleted, AppType.Client, DateTimeOffset.UtcNow,
                        DurationMs: downloadElapsed.TotalMilliseconds), token).ConfigureAwait(false);
                }
                catch (Exception ex) { GeneralTracer.Warn($"Reporter DownloadCompleted failed: {ex.Message}"); }
            }

            if (downloadFailedCount > 0)
            {
                GeneralTracer.Error($"SilentPollOrchestrator: download had {downloadFailedCount} failures, aborting update.");
                return;
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: download failed.", ex);
            if (_hooks != null)
            {
                try { await _hooks.OnUpdateErrorAsync(updateCtx, ex).ConfigureAwait(false); }
                catch (Exception hookEx) { GeneralTracer.Warn($"Hook OnUpdateErrorAsync failed: {hookEx.Message}"); }
            }
            if (_reporter != null)
            {
                try
                {
                    await _reporter.ReportAsync(new UpdateReport(
                        updateCtx.AppName, updateCtx.CurrentVersion, updateCtx.TargetVersion,
                        UpdateEvent.UpdateFailed, AppType.Client, DateTimeOffset.UtcNow,
                        ErrorMessage: ex.Message), token).ConfigureAwait(false);
                }
                catch (Exception reporterEx) { GeneralTracer.Warn($"Reporter UpdateFailed failed: {reporterEx.Message}"); }
            }
            return;
        }
        finally { httpClient.Dispose(); }

        // Execute pipeline
        try
        {
            var strategy = CreateStrategy();
            strategy.Create(_configInfo);
            await strategy.ExecuteAsync();

            GeneralTracer.Info("SilentPollOrchestrator: update prepared.");
            Interlocked.Exchange(ref _prepared, 1);

            // ═══ Hooks + Reporter: update applied ═══
            if (_hooks != null)
            {
                try { await _hooks.OnAfterUpdateAsync(updateCtx).ConfigureAwait(false); }
                catch (Exception ex) { GeneralTracer.Warn($"Hook OnAfterUpdateAsync failed: {ex.Message}"); }
            }

            if (_reporter != null)
            {
                try
                {
                    var elapsedMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                    await _reporter.ReportAsync(new UpdateReport(
                        updateCtx.AppName, updateCtx.CurrentVersion, updateCtx.TargetVersion,
                        UpdateEvent.UpdateApplied, AppType.Client, DateTimeOffset.UtcNow,
                        DurationMs: elapsedMs), token).ConfigureAwait(false);
                }
                catch (Exception ex) { GeneralTracer.Warn($"Reporter UpdateApplied failed: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: pipeline execution failed.", ex);
            if (_hooks != null)
            {
                try { await _hooks.OnUpdateErrorAsync(updateCtx, ex).ConfigureAwait(false); }
                catch (Exception hookEx) { GeneralTracer.Warn($"Hook OnUpdateErrorAsync failed: {hookEx.Message}"); }
            }
            if (_reporter != null)
            {
                try
                {
                    await _reporter.ReportAsync(new UpdateReport(
                        updateCtx.AppName, updateCtx.CurrentVersion, updateCtx.TargetVersion,
                        UpdateEvent.UpdateFailed, AppType.Client, DateTimeOffset.UtcNow,
                        ErrorMessage: ex.Message), token).ConfigureAwait(false);
                }
                catch (Exception reporterEx) { GeneralTracer.Warn($"Reporter UpdateFailed failed: {reporterEx.Message}"); }
            }
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _prepared) != 1 || Interlocked.Exchange(ref _updaterStarted, 1) == 1) return;

        try
        {
            Environments.SetEnvironmentVariable("ProcessInfo", _configInfo.ProcessInfo ?? string.Empty);
            var updaterPath = Path.Combine(_configInfo.InstallPath, _configInfo.AppName);
            if (File.Exists(updaterPath))
            {
                GeneralTracer.Info($"SilentPollOrchestrator: launching updater {updaterPath}");
                Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = updaterPath });
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: OnProcessExit failed.", ex);
        }
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

/// <summary>Silent polling configuration.</summary>
public sealed class SilentOptions
{
    /// <summary>Polling interval (default 1 hour).</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Whether to auto-install after download.</summary>
    public bool AutoInstall { get; set; } = false;
}
