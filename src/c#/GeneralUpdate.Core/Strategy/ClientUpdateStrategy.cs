using GeneralUpdate.Core.Differential;
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
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Ipc;
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
        if (_pendingDirtyStrategy != null && _osStrategy is AbstractStrategy abs)
            abs.DirtyStrategy = _pendingDirtyStrategy;
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

    private IDirtyStrategy? _pendingDirtyStrategy;

    /// <summary>Sets the directory-level dirty strategy on the underlying OS-level strategy for differential patch updates.
    /// Safe to call before or after Create(). If called before, the strategy is cached and applied when Create() resolves _osStrategy.</summary>
    public void SetDirtyStrategy(IDirtyStrategy? dirtyStrategy)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.DirtyStrategy = dirtyStrategy;
        else
            _pendingDirtyStrategy = dirtyStrategy;
    }

    /// <summary>Sets the file-level binary differ on the underlying OS-level strategy.</summary>
    public void SetBinaryDiffer(IBinaryDiffer? binaryDiffer)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.BinaryDiffer = binaryDiffer;
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
        // Standard mode — silent mode is handled by GeneralUpdateBootstrap.LaunchSilentAsync().
        // Runtime options (Encoding, Format, DownloadTimeOut, etc.) are already
        // populated on _configInfo by Bootstrap.ApplyRuntimeOptions().
        await ExecuteStandardWorkflowAsync();
    }

    private async Task ExecuteStandardWorkflowAsync()
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

        // Backup — conditionally skipped when BackupEnabled is false
        if (_configInfo.BackupEnabled != false)
        {
            Backup();
        }
        else
        {
            GeneralTracer.Info("ClientUpdateStrategy: backup skipped (BackupEnabled=false).");
        }

        _osStrategy!.Create(_configInfo);

        // Download via orchestrator — wired with options from GlobalConfigInfo
        var orchOptions = Download.Models.DownloadOrchestratorOptions.From(_configInfo);
        GeneralTracer.Info($"ClientUpdateStrategy: downloading {downloadPlan.Assets.Count} asset(s).");
        if (_orchestrator != null)
        {
            await _orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
        }
        else
        {
            var httpClient = GeneralUpdate.Core.Network.HttpClientProvider.Shared;
            try
            {
                var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(httpClient, orchOptions);
                await orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
            }
            finally { }
        }

        await SafeReportDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);
        await SafeOnDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);

        // Phase: apply Upgrade packages — update Upgrade.exe itself before launching it.
        // Safe because MainApp and Upgrade.exe are different files (no lock conflict).
        var allVersions = downloadPlan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = _configInfo.Format ?? "ZIP",
            AppType = a.IsForcibly ? null : null // preserve original AppType
        }).ToList();

        // Rebuild the full VersionInfo list with AppType preserved from download source
        var downloadVersions = downloadPlan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = _configInfo.Format ?? "ZIP",
            AppType = _configInfo.IsUpgradeUpdate == true && a.Version != _configInfo.ClientVersion
                ? (int)AppType.Upgrade : (int)AppType.Client
        }).ToList();

        // Split: Upgrade versions vs MainApp versions
        var upgradeVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Upgrade).ToList();
        var clientVersions = downloadVersions.Where(v => v.AppType != (int)AppType.Upgrade).ToList();

        GeneralTracer.Info($"ClientUpdateStrategy: Upgrade packages={upgradeVersions.Count}, MainApp packages={clientVersions.Count}");

        // Apply Upgrade packages now (update Upgrade.exe before launching it)
        if (upgradeVersions.Count > 0)
        {
            GeneralTracer.Info("ClientUpdateStrategy: applying Upgrade packages.");
            _configInfo.UpdateVersions = upgradeVersions;
            _osStrategy!.Create(_configInfo);
            await _osStrategy.ExecuteAsync();
        }

        // Send IPC with remaining MainApp versions for the upgrade process
        var processInfo = ConfigurationMapper.MapToProcessInfo(
            _configInfo, clientVersions,
            _configInfo.BlackFormats ?? BlackListDefaults.DefaultBlackFormats,
            _configInfo.BlackFiles ?? BlackListDefaults.DefaultBlackFiles,
            _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);

        _configInfo.ProcessInfo = JsonSerializer.Serialize(processInfo,
            ProcessInfoJsonContext.Default.ProcessInfo);
        new EncryptedFileProcessInfoProvider().Send(processInfo);
        GeneralTracer.Info("ClientUpdateStrategy: ProcessInfo sent with MainApp versions only.");

        await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
        await SafeReportUpdateAppliedAsync(hooksCtx).ConfigureAwait(false);
        await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);

        // Launch the upgrade process to apply MainApp updates
        var updaterPath = Path.Combine(_configInfo.InstallPath, _configInfo.AppName);
        if (!File.Exists(updaterPath))
            throw new FileNotFoundException($"Upgrade application not found: {updaterPath}");

        GeneralTracer.Info($"ClientUpdateStrategy: launching upgrade process {updaterPath}");
        Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = updaterPath });
        GeneralTracer.Info("ClientUpdateStrategy: upgrade process launched, exiting.");
        await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
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

    private void InitBlackList()
    {
        var effectiveConfig = new BlackListConfig(
            _configInfo!.BlackFiles?.Count > 0 ? _configInfo.BlackFiles : BlackListDefaults.DefaultBlackFiles,
            _configInfo.BlackFormats?.Count > 0 ? _configInfo.BlackFormats : BlackListDefaults.DefaultBlackFormats,
            _configInfo.SkipDirectorys?.Count > 0 ? _configInfo.SkipDirectorys : BlackListDefaults.DefaultSkipDirectories
        );
        StorageManager.BlackListMatcher = new DefaultBlackListMatcher(effectiveConfig);
    }

    private void Backup()
    {
        GeneralTracer.Info($"ClientUpdateStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
        StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
            _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);
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

    private async Task SafeOnAfterUpdateAsync(Hooks.UpdateContext ctx)
    {
        try { await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeOnDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            var downloadCtx = new Hooks.DownloadContext(
                _configInfo?.MainAppName ?? _configInfo?.AppName ?? "unknown",
                _configInfo?.LastVersion ?? "",
                0, TimeSpan.Zero, _configInfo?.TempPath, true);
            await Hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"OnDownloadCompletedAsync hook failed: {ex.Message}"); }
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

    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.UpdateApplied, ctx.AppType, DateTimeOffset.UtcNow
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}"); }
    }

    #endregion
}
