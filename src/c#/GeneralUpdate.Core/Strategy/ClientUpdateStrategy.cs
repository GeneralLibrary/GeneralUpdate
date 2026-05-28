using GeneralUpdate.Core.Differential;
using GeneralUpdate.Differential.Abstractions;
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
using GeneralUpdate.Core.Pipeline;

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
    private IStrategy? _customOsStrategy;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private Download.Abstractions.IDownloadOrchestrator? _orchestrator;
    private Download.Abstractions.IDownloadPolicy? _customDownloadPolicy;
    private Download.Abstractions.IDownloadExecutor? _customDownloadExecutor;
    private Func<string?, Download.Abstractions.IDownloadPipeline>? _customDownloadPipelineFactory;
    private int _mainRecordId;

    /// <summary>Which side(s) need updating, determined by server validation.</summary>
    private enum UpdateScenario
    {
        None,
        UpgradeOnly,
        MainOnly,
        Both
    }

    /// <summary>Lifecycle hooks injected by the bootstrap.</summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>Update status reporter injected by the bootstrap.</summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

    /// <summary>Download source (e.g., HTTP, SignalR Hub). Injected by bootstrap via HubConfig or extension registry (<c>.DownloadSource&lt;T&gt;()</c>).</summary>
    public Download.Abstractions.IDownloadSource? DownloadSource { get; set; }

    public ClientUpdateStrategy() { }

    public ClientUpdateStrategy(Download.Abstractions.IDownloadOrchestrator? orchestrator)
        => _orchestrator = orchestrator;

    /// <summary>Sets a custom OS-level strategy (injected via <c>.Strategy&lt;T&gt;()</c>).
    /// When set, this replaces the automatic platform detection in <see cref="ResolveOsStrategy"/>.</summary>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>Sets a custom download orchestrator (injected via <c>.DownloadOrchestrator&lt;T&gt;()</c>).</summary>
    public void SetOrchestrator(Download.Abstractions.IDownloadOrchestrator? orchestrator) => _orchestrator = orchestrator;

    /// <summary>Sets a custom download retry policy (injected via <c>.DownloadPolicy&lt;T&gt;()</c>).</summary>
    public void SetDownloadPolicy(Download.Abstractions.IDownloadPolicy? policy) => _customDownloadPolicy = policy;

    /// <summary>Sets a custom download executor (injected via <c>.DownloadExecutor&lt;T&gt;()</c>).</summary>
    public void SetDownloadExecutor(Download.Abstractions.IDownloadExecutor? executor) => _customDownloadExecutor = executor;

    /// <summary>Sets a custom download pipeline factory (injected via <c>.DownloadPipeline&lt;T&gt;()</c>).</summary>
    public void SetDownloadPipelineFactory(Func<string?, Download.Abstractions.IDownloadPipeline>? factory) => _customDownloadPipelineFactory = factory;

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
        if (_osStrategy is AbstractStrategy abs)
        {
            if (_pendingDiffPipeline != null) abs.DiffPipeline = _pendingDiffPipeline;
        }
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

    private DiffPipeline? _pendingDiffPipeline;

    /// <summary>Sets the DiffPipeline on the underlying OS-level strategy for parallel patch application.</summary>
    public void SetDiffPipeline(DiffPipeline? diffPipeline)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.DiffPipeline = diffPipeline;
        else
            _pendingDiffPipeline = diffPipeline;
    }

    public async Task StartAppAsync()
    {
        if (_osStrategy != null)
            await _osStrategy.StartAppAsync();
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
        // Standard mode �?silent mode is handled by GeneralUpdateBootstrap.LaunchSilentAsync().
        // Runtime options (Encoding, Format, DownloadTimeOut, etc.) are already
        // populated on _configInfo by Bootstrap.ApplyRuntimeOptions().
        await ExecuteStandardWorkflowAsync();
    }

    private async Task ExecuteStandardWorkflowAsync()
    {
        GeneralTracer.Info(
            $"ClientUpdateStrategy: validating client={_configInfo!.ClientVersion}, upgrade={_configInfo.UpgradeClientVersion}");

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

        // Call server validation — returns assets plus per-side flags from the two Validate calls
        var sourceResult = await downloadSource.ListAsync().ConfigureAwait(false);
        var downloadPlan = Download.DownloadPlanBuilder.Build(sourceResult.Assets, _configInfo.ClientVersion);

        // Detect update status from SERVER validation results: IsMainUpdate is true only when
        // the server returned version info for the Client call, IsUpgradeUpdate only when
        // the server returned version info for the Upgrade call (requirement 1).
        _configInfo.IsMainUpdate = sourceResult.HasMainUpdate;
        _configInfo.IsUpgradeUpdate = sourceResult.HasUpgradeUpdate;
        _configInfo.LastVersion = downloadPlan.Assets.LastOrDefault()?.Version;

        var scenario = (_configInfo.IsMainUpdate, _configInfo.IsUpgradeUpdate) switch
        {
            (false, false) => UpdateScenario.None,
            (false, true) => UpdateScenario.UpgradeOnly,
            (true, false) => UpdateScenario.MainOnly,
            (true, true) => UpdateScenario.Both,
        };
        GeneralTracer.Info($"ClientUpdateStrategy: Scenario={scenario}, AssetCount={downloadPlan.Assets.Count}");

        // Dispatch update info event with populated version data (full GeneralSpacestation-compatible fields)
        var versionInfos = downloadPlan.Assets.Select(a => new VersionInfo
        {
            RecordId = a.RecordId,
            Name = a.Name,
            Url = a.Url,
            Size = a.Size,
            Hash = a.SHA256,
            Version = a.Version,
            IsForcibly = a.IsForcibly,
            IsFreeze = a.IsFreeze,
            AppType = a.AppType,
            UpgradeMode = a.UpgradeMode,
            IsCrossVersion = a.IsCrossVersion,
            FromVersion = a.FromVersion
        }).ToList();

        var versionResp = new VersionRespDTO
        {
            Code = versionInfos.Count > 0 ? 200 : 404,
            Body = versionInfos,
            Message = versionInfos.Count > 0 ? $"Found {versionInfos.Count} update(s)." : "No updates available."
        };

        var updateInfoArgs = new UpdateInfoEventArgs(versionResp);

        // Capture the first RecordId for status reporting to GeneralSpacestation
        _mainRecordId = downloadPlan.Assets.FirstOrDefault().RecordId;
        EventManager.Instance.Dispatch(this, updateInfoArgs);

        var isForcibly = downloadPlan.IsForcibly;
        if (CanSkip(isForcibly, updateInfoArgs))
        {
            GeneralTracer.Info("ClientUpdateStrategy: update skipped.");
            return;
        }

        // Scenario None: nothing to update — exit early
        if (scenario == UpdateScenario.None)
        {
            GeneralTracer.Info("ClientUpdateStrategy: no update available for client or upgrade.");
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

        // Check failed version
        if (!string.IsNullOrEmpty(_configInfo.LastVersion) && CheckFail(_configInfo.LastVersion))
        {
            GeneralTracer.Warn(
                $"ClientUpdateStrategy: version {_configInfo.LastVersion} matches known-failed upgrade.");
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

        // Download ALL packages via orchestrator (requirement 6: client downloads everything
        // regardless of whether client or upgrade needs updating)
        var orchOptions = Download.Models.DownloadOrchestratorOptions.From(_configInfo);
        GeneralTracer.Info($"ClientUpdateStrategy: downloading {downloadPlan.Assets.Count} asset(s).");
        if (_orchestrator != null)
        {
            await _orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
        }
        else
        {
            var httpClient = GeneralUpdate.Core.Network.HttpClientProvider.Shared;
            var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(
                httpClient, orchOptions, _customDownloadPolicy,
                _customDownloadExecutor, _customDownloadPipelineFactory);
            await orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
        }

        await SafeReportDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);
        await SafeOnDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);

        // Build VersionInfo list with AppType preserved from server response.
        var downloadVersions = downloadPlan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = _configInfo.Format.ToExtension(),
            AppType = a.AppType ?? (int)AppType.Client
        }).ToList();

        var upgradeVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Upgrade).ToList();
        var clientVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Client).ToList();
        GeneralTracer.Info(
            $"ClientUpdateStrategy: Upgrade packages={upgradeVersions.Count}, MainApp packages={clientVersions.Count}");

        // ── Dispatch by scenario — one switch, four states, zero nested if-else ──
        switch (scenario)
        {
            case UpdateScenario.UpgradeOnly:
                await ApplyUpgradePackagesAsync(upgradeVersions).ConfigureAwait(false);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx).ConfigureAwait(false);
                GeneralTracer.Info("ClientUpdateStrategy: Upgrade-only update applied, client continues running.");
                break;

            case UpdateScenario.MainOnly:
                SendProcessIpc(clientVersions);
                await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
                await LaunchUpgradeProcessAsync().ConfigureAwait(false);
                break;

            case UpdateScenario.Both:
                await ApplyUpgradePackagesAsync(upgradeVersions).ConfigureAwait(false);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx).ConfigureAwait(false);
                SendProcessIpc(clientVersions);
                await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
                await LaunchUpgradeProcessAsync().ConfigureAwait(false);
                break;
        }
    }

    #endregion

    #region Scenario actions

    private async Task ApplyUpgradePackagesAsync(List<VersionInfo> upgradeVersions)
    {
        if (upgradeVersions.Count == 0) return;
        GeneralTracer.Info("ClientUpdateStrategy: applying Upgrade packages in place.");
        _configInfo!.UpdateVersions = upgradeVersions;
        _osStrategy!.Create(_configInfo);
        await _osStrategy.ExecuteAsync().ConfigureAwait(false);
    }

    private void SendProcessIpc(List<VersionInfo> clientVersions)
    {
        var processInfo = ConfigurationMapper.MapToProcessInfo(
            _configInfo!, clientVersions,
            _configInfo!.BlackFormats ?? BlackListDefaults.DefaultBlackFormats,
            _configInfo.BlackFiles ?? BlackListDefaults.DefaultBlackFiles,
            _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);

        _configInfo.ProcessInfo = JsonSerializer.Serialize(processInfo,
            ProcessInfoJsonContext.Default.ProcessInfo);
        new EncryptedFileProcessInfoProvider().Send(processInfo);
        GeneralTracer.Info("ClientUpdateStrategy: ProcessInfo sent with MainApp versions only.");
    }

    private async Task LaunchUpgradeProcessAsync()
    {
        if (_osStrategy is AbstractStrategy abs)
        {
            abs.LaunchAppName = _configInfo!.UpdateAppName;
            abs.LaunchBowl = false;
            abs.UseUpdatePath = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath);
        }

        GeneralTracer.Info(
            $"ClientUpdateStrategy: launching upgrade process {_configInfo!.UpdateAppName} via OS strategy.");
        await _osStrategy!.StartAppAsync();
    }

    #endregion

    #region Helpers

    private IStrategy ResolveOsStrategy()
    {
        if (_customOsStrategy != null)
            return _customOsStrategy;
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
            _configInfo.SkipDirectorys?.Count > 0
                ? _configInfo.SkipDirectorys
                : BlackListDefaults.DefaultSkipDirectories
        );
        StorageManager.BlackListMatcher = new DefaultBlackListMatcher(effectiveConfig);
    }

    private void Backup()
    {
        GeneralTracer.Info(
            $"ClientUpdateStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
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
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? AppDomain.CurrentDomain.BaseDirectory,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.Client
        );
    }

    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}");
            return true;
        }
    }

    private async Task SafeOnBeforeStartAppAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}");
        }
    }

    private async Task SafeOnUpdateErrorAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}");
        }
    }

    private async Task SafeOnAfterUpdateAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}");
        }
    }

    private async Task SafeOnDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            var downloadCtx = new Hooks.DownloadContext(
                _configInfo?.MainAppName ?? _configInfo?.UpdateAppName ?? "unknown",
                _configInfo?.LastVersion ?? "",
                0, TimeSpan.Zero, _configInfo?.TempPath, true);
            await Hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnDownloadCompletedAsync hook failed: {ex.Message}");
        }
    }

    private async Task SafeReportUpdateStartedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Updating, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}");
        }
    }

    private async Task SafeReportDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Updating, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report DownloadCompleted failed: {ex.Message}");
        }
    }

    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Failure, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}");
        }
    }

    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Success, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}");
        }
    }

    #endregion
}