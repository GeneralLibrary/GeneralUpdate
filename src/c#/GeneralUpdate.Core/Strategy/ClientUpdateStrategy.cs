using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Client-side update strategy. Validates versions against server,
/// downloads update packages, creates process info for the upgrade side,
/// and starts the upgrade process.
/// </summary>
/// <remarks>
/// This is the AppType.Client role strategy. It composes an OS-specific
/// strategy (Windows/Linux/Mac) for platform operations.
/// </remarks>
public class ClientUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private readonly Download.Abstractions.IDownloadOrchestrator? _orchestrator;
    private readonly DiffMode _diffMode = DiffMode.Serial;

    /// <summary>Lifecycle hooks injected by the bootstrap.</summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();
    /// <summary>Update status reporter injected by the bootstrap.</summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();
    /// <summary>Download source (e.g., HTTP, SignalR Hub). Injected by bootstrap via HubConfig or extension registry (<c>.DownloadSource&lt;T&gt;()</c>).</summary>
    public Download.Abstractions.IDownloadSource? DownloadSource { get; set; }

    public ClientUpdateStrategy(Download.Abstractions.IDownloadOrchestrator? orchestrator = null) { _orchestrator = orchestrator; }

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
    }

    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("ClientUpdateStrategy not configured.");

        try
        {
            GeneralTracer.Debug("ClientUpdateStrategy.ExecuteAsync start.");
            await CallSmallBowlHomeAsync(_configInfo.Bowl);
            await ExecuteWorkflowAsync();
        }
        catch (Exception ex)
        {
            var errCtx = BuildUpdateContext();
            await SafeOnUpdateErrorAsync(errCtx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(errCtx, ex).ConfigureAwait(false);
            GeneralTracer.Error("ClientUpdateStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
    }

    public void StartApp()
    {
        _osStrategy?.StartApp();
    }

    /// <summary>Register a precheck callback invoked when update info is available.</summary>
    public ClientUpdateStrategy UseUpdatePrecheck(Func<UpdateInfoEventArgs, bool> func)
    {
        _updatePrecheck = func ?? throw new ArgumentNullException(nameof(func));
        return this;
    }

    #region Workflow

    private async Task ExecuteWorkflowAsync()
    {
        var defaultEncoding = Encoding.UTF8;
        var defaultTimeout = 60;
        if (true /* silent check would read from options */)
        {
            // Standard mode
            await ExecuteStandardWorkflowAsync(defaultEncoding, defaultTimeout);
        }
    }

    private async Task ExecuteStandardWorkflowAsync(Encoding encoding, int timeout)
    {
        GeneralTracer.Info($"ClientUpdateStrategy: validating client={_configInfo!.ClientVersion}, upgrade={_configInfo.UpgradeClientVersion}");

        // Use injected DownloadSource (Hub/HTTP), or default to HttpDownloadSource
        var downloadSource = DownloadSource ?? new Download.Sources.HttpDownloadSource(
            _configInfo.UpdateUrl,
            _configInfo.ClientVersion,
            _configInfo.UpgradeClientVersion,
            _configInfo.AppSecretKey,
            GetPlatform(),
            _configInfo.ProductId,
            _configInfo.Scheme,
            _configInfo.Token);

        var assets = await downloadSource.ListAsync().ConfigureAwait(false);
        var downloadPlan = Download.DownloadPlanBuilder.Build(assets, _configInfo.ClientVersion);

        // Detect update status
        _configInfo.IsMainUpdate = downloadPlan.HasAssets;
        _configInfo.IsUpgradeUpdate = assets.Any(a => a.Version != _configInfo.ClientVersion);
        _configInfo.LastVersion = downloadPlan.Assets.LastOrDefault()?.Version;
        GeneralTracer.Info($"ClientUpdateStrategy: IsMainUpdate={_configInfo.IsMainUpdate}, IsUpgradeUpdate={_configInfo.IsUpgradeUpdate}, AssetCount={downloadPlan.Assets.Count}");

        // Dispatch update info event
        var updateInfoArgs = new UpdateInfoEventArgs(null);
        EventManager.Instance.Dispatch(this, updateInfoArgs);

        var isForcibly = downloadPlan.IsForcibly;
        if (CanSkip(isForcibly, updateInfoArgs))
        {
            GeneralTracer.Info("ClientUpdateStrategy: update skipped.");
            return;
        }

        // Hooks: allow cancellation before download
        var hooksCtx = BuildUpdateContext();
        if (!await SafeOnBeforeUpdateAsync(hooksCtx).ConfigureAwait(false))
        {
            GeneralTracer.Info("ClientUpdateStrategy: update cancelled by OnBeforeUpdateAsync hook.");
            return;
        }

        // Report: update started
        await SafeReportUpdateStartedAsync(hooksCtx).ConfigureAwait(false);

        InitBlackList();
        ApplyRuntimeOptions(encoding, timeout);

        _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        if (!_configInfo.IsMainUpdate)
        {
            GeneralTracer.Info("ClientUpdateStrategy: no update available.");
            return;
        }

        // Check failed version
        if (!string.IsNullOrEmpty(_configInfo.LastVersion) && CheckFail(_configInfo.LastVersion))
        {
            GeneralTracer.Warn($"ClientUpdateStrategy: version {_configInfo.LastVersion} matches known-failed upgrade.");
            return;
        }

        // Build process info for the upgrade process
        // Convert DownloadAsset list to VersionInfo for ProcessInfo compatibility
        var downloadVersions = downloadPlan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = "ZIP"
        }).ToList();

        _configInfo.ProcessInfo = JsonSerializer.Serialize(
            ConfigurationMapper.MapToProcessInfo(
                _configInfo, downloadVersions,
                BlackListManager.Instance.BlackFormats.ToList(),
                BlackListManager.Instance.BlackFiles.ToList(),
                BlackListManager.Instance.SkipDirectorys.ToList()),
            ProcessInfoJsonContext.Default.ProcessInfo);

        // Backup
        Backup();

        _osStrategy!.Create(_configInfo);

        // Download via orchestrator
        GeneralTracer.Info($"ClientUpdateStrategy: downloading {downloadPlan.Assets.Count} asset(s).");
        if (_orchestrator != null)
        {
            await _orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
        }
        else
        {
            var httpClient = new System.Net.Http.HttpClient();
            try
            {
                var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(httpClient);
                await orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
            }
            finally { httpClient.Dispose(); }
        }

        await SafeReportDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);

        // Apply updates and start app
        await _osStrategy.ExecuteAsync();
        await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
        _osStrategy.StartApp();
    }

    #endregion

    #region Helpers

    private static IStrategy ResolveOsStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacStrategy();
        throw new PlatformNotSupportedException("The current operating system is not supported!");
    }

    private void ApplyRuntimeOptions(Encoding encoding, int timeout)
    {
        _configInfo!.Encoding = encoding;
        _configInfo.Format = Format.ZIP;
        _configInfo.DownloadTimeOut = timeout;
    }

    private void InitBlackList()
    {
        BlackListManager.Instance.AddBlackFiles(_configInfo!.BlackFiles);
        BlackListManager.Instance.AddBlackFormats(_configInfo.BlackFormats);
        BlackListManager.Instance.AddSkipDirectorys(_configInfo.SkipDirectorys);
    }

    private void Backup()
    {
        GeneralTracer.Info($"ClientUpdateStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
        StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
            BlackListManager.Instance.SkipDirectorys);
    }

    private bool CanSkip(bool isForcibly, UpdateInfoEventArgs updateInfo)
    {
        if (isForcibly) return false;
        return _updatePrecheck?.Invoke(updateInfo) == true;
    }

    private bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;
        return new Version(fail) >= new Version(version);
    }

    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    private async Task CallSmallBowlHomeAsync(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return;
            foreach (var process in processes)
            {
                GeneralTracer.Info($"Shutting down process {process.ProcessName} (ID: {process.Id})");
                await GracefulExit.ShutdownAsync(process).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("CallSmallBowlHomeAsync failed.", ex);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Hooks & Reporter safe wrappers
    // ════════════════════════════════════════════════════════════════

    private Hooks.UpdateContext BuildUpdateContext()
    {
        return new Hooks.UpdateContext(
            _configInfo?.AppName ?? "unknown",
            _configInfo?.InstallPath ?? AppDomain.CurrentDomain.BaseDirectory,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.Client
        );
    }

    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.UpdateContext ctx)
    {
        try { return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}"); return true; }
    }

    private async Task SafeOnBeforeStartAppAsync(Hooks.UpdateContext ctx)
    {
        try { await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeOnUpdateErrorAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try { await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateStartedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.UpdateStarted, ctx.AppType, DateTimeOffset.UtcNow
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}"); }
    }

    private async Task SafeReportDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.DownloadCompleted, ctx.AppType, DateTimeOffset.UtcNow
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report DownloadCompleted failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.UpdateFailed, ctx.AppType, DateTimeOffset.UtcNow,
                ErrorMessage: error.Message
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}"); }
    }

    #endregion
}
