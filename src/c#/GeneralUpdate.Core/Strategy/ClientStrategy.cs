using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Client update strategy. Responsible for version verification with the server, downloading update packages,
/// constructing process information required by the upgrade process, and launching the upgrade process.
/// </summary>
/// <remarks>
/// <para>This strategy serves the <c>AppType.Client</c> role. The complete update workflow is as follows:</para>
/// <para>
/// 1. <b>Version Verification</b>: Sends version information to the server via <see cref="Download.Abstractions.IDownloadSource.ListAsync"/>,
///    and sets the <c>IsMainUpdate</c> and <c>IsUpgradeUpdate</c> flags based on the response to determine the update scenario.
/// </para>
/// <para>
/// 2. <b>Event Dispatch</b>: Constructs an <see cref="UpdateInfoEventArgs"/> and dispatches it to subscribers via <c>EventManager</c>.
/// </para>
/// <para>
/// 3. <b>Skip Check</b>: If the update is not forced and the pre-check callback returns <c>true</c>, the update is skipped.
/// </para>
/// <para>
/// 4. <b>Pre-Update Hook</b>: Calls <c>Hooks.OnBeforeUpdateAsync</c>, allowing external logic to cancel the update.
/// </para>
/// <para>
/// 5. <b>Backup</b>: Backs up the current installation directory to a temporary directory (can be disabled via <c>BackupEnabled</c>).
/// </para>
/// <para>
/// 6. <b>Download</b>: Downloads all update packages through the download orchestrator, supporting custom policies, executors, and processing pipelines.
/// </para>
/// <para>
/// 7. <b>Scenario Dispatch</b>: Executes different workflows based on the server validation result:
///    - <c>UpgradeOnly</c>: Applies upgrade program update packages in place only;
///    - <c>MainOnly</c>: Serializes main program update packages as <c>ProcessContract</c> and sends via IPC, then starts the upgrade process;
///    - <c>Both</c>: Applies upgrade program update packages first, then sends main program info and starts the upgrade process.
/// </para>
/// <para>
/// 8. <b>Upgrade Application</b>: Calls <c>ApplyUpgradePackagesAsync</c> to apply incremental/full update packages in place through the OS strategy pipeline.
/// </para>
/// <para>
/// 9. <b>IPC Communication</b>: Serializes main program update information as JSON via <c>SendProcessIpc</c> and sends it to the upgrade process through encrypted IPC.
/// </para>
/// <para>
/// 10. <b>Launch Upgrade Process</b>: Delegates to the OS strategy via <c>LaunchUpgradeProcessAsync</c> to start the upgrade executable.</para>
/// <para>
/// This class uses a <b>two-layer strategy design</b>: <c>ClientStrategy</c> acts as the "role" strategy responsible for orchestrating the update flow;
/// it internally composes an OS-specific platform strategy (<see cref="WindowsStrategy"/>, <see cref="LinuxStrategy"/>, <see cref="MacStrategy"/>)
/// to handle platform-related operations (file operations, process management, installation path determination, etc.).
/// </para>
/// </remarks>
public class ClientStrategy : IStrategy
{
    private UpdateContext? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private Download.Abstractions.IDownloadOrchestrator? _orchestrator;
    private Download.Abstractions.IDownloadPolicy? _customDownloadPolicy;
    private Download.Abstractions.IDownloadExecutor? _customDownloadExecutor;
    private Func<string?, Download.Abstractions.IDownloadPipeline>? _customDownloadPipelineFactory;
    private int _mainRecordId;
    private int _upgradeRecordId;
    private int _reportType = 1; // 1=Upgrade(active poll), 2=Push(SignalR push)

    /// <summary>
    /// When <c>false</c>, skips <see cref="LaunchUpgradeProcessAsync"/> in MainOnly / Both scenarios.
    /// The caller (e.g. <see cref="Silent.SilentPollOrchestrator"/>) is responsible for launching the
    /// upgrade process at a later point. Default is <c>true</c> — standard immediate-launch behaviour.
    /// </summary>
    public bool LaunchAfterPrepare { get; set; } = true;

    /// <summary>
    /// After <see cref="ExecuteAsync"/> completes, <c>true</c> indicates that client packages were
    /// staged via <see cref="SendProcessIpc"/> and the upgrade process should be launched to apply them.
    /// </summary>
    public bool HasPreparedClientUpdate { get; private set; }

    /// <summary>
    /// Update scenario determined by the server validation result, indicating which update targets are needed.
    /// </summary>
    private enum UpdateScenario
    {
        None,
        UpgradeOnly,
        MainOnly,
        Both
    }

    /// <summary>
    /// Gets or sets the update lifecycle hooks. Registered and injected by the bootstrap via <c>.Hooks&lt;T&gt;()</c>.
    /// </summary>
    /// <value>An <c>IUpdateHooks</c> hook instance. Defaults to <c>NoOpUpdateHooks</c> (no-operation implementation).</value>
    /// <remarks>
    /// Hook callbacks are safely invoked at key points in the update flow (wrapped in try-catch), so a single hook failure does not block the flow.
    /// See <see cref="SafeOnBeforeUpdateAsync"/>, <see cref="SafeOnAfterUpdateAsync"/>, and related methods.
    /// </remarks>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// Gets or sets the update status reporter. Registered and injected by the bootstrap via <c>.UpdateReporter&lt;T&gt;()</c>.
    /// </summary>
    /// <value>An <c>IUpdateReporter</c> reporter instance. Defaults to <c>HttpUpdateReporter</c>.</value>
    /// <remarks>
    /// The reporter reports status to the server when the update starts, download completes, or the update is applied or fails.
    /// All reporting calls are wrapped in try-catch, so a reporting failure does not block the flow.
    /// See <see cref="SafeReportUpdateStartedAsync"/>, <see cref="SafeReportDownloadCompletedAsync"/>, and related methods.
    /// </remarks>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.HttpUpdateReporter();

    /// <summary>
    /// Gets or sets the download data source. Registered and injected by the bootstrap via <c>.DownloadSource&lt;T&gt;()</c>,
    /// or configured via <c>HubConfig</c> in the bootstrap.
    /// </summary>
    /// <value>An <c>IDownloadSource</c> data source instance. When <c>null</c>, <c>HttpDownloadSource</c> is used by default.</value>
    /// <remarks>
    /// In <see cref="ExecuteStandardWorkflowAsync"/>, if this property is <c>null</c>,
    /// an <c>HttpDownloadSource</c> instance is automatically created based on the <c>UpdateUrl</c>, version number,
    /// and other information in <c>UpdateContext</c>.
    /// </remarks>
    public Download.Abstractions.IDownloadSource? DownloadSource { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientStrategy"/> class.
    /// </summary>
    /// <remarks>
    /// Default constructor. All properties use default values (no-op hooks, no-op reporter).
    /// The download orchestrator defaults to <c>null</c> and will be set to a default <c>DefaultDownloadOrchestrator</c>
    /// in <see cref="ExecuteStandardWorkflowAsync"/>.
    /// The strategy instance must be initialized via <see cref="Create"/> with a <see cref="UpdateContext"/>.
    /// </remarks>
    public ClientStrategy() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientStrategy"/> class with a custom download orchestrator.
    /// </summary>
    /// <param name="orchestrator">Custom download orchestrator instance to take over the batch download flow. If <c>null</c>, the default orchestrator is used.</param>
    /// <remarks>
    /// The orchestrator passed via this constructor takes priority over the value set via <see cref="SetOrchestrator"/>.
    /// </remarks>
    public ClientStrategy(Download.Abstractions.IDownloadOrchestrator? orchestrator)
        => _orchestrator = orchestrator;

    /// <summary>
    /// Sets a custom OS-level strategy. Registered and injected by the bootstrap via <c>.Strategy&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="strategy">Custom OS strategy instance. When set, replaces the automatic platform detection in <see cref="ResolveOsStrategy"/>.</param>
    /// <remarks>
    /// When this strategy is set, <see cref="ResolveOsStrategy"/> will skip the <c>RuntimeInformation.IsOSPlatform</c> check
    /// and use this injected strategy directly. Suitable for scenarios requiring completely custom platform behavior.
    /// </remarks>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>
    /// Sets a custom download orchestrator. Registered and injected by the bootstrap via <c>.DownloadOrchestrator&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="orchestrator">Custom download orchestrator instance. When <c>null</c>, the default <c>DefaultDownloadOrchestrator</c> is used.</param>
    /// <remarks>
    /// When set, this orchestrator is used during the download phase of <see cref="ExecuteStandardWorkflowAsync"/>.
    /// If an orchestrator was already injected via the constructor, the constructor value takes precedence.
    /// A custom orchestrator fully takes over the download flow; settings from <see cref="SetDownloadPolicy"/>, <see cref="SetDownloadExecutor"/>,
    /// and <see cref="SetDownloadPipelineFactory"/> are ignored.
    /// </remarks>
    public void SetOrchestrator(Download.Abstractions.IDownloadOrchestrator? orchestrator) => _orchestrator = orchestrator;

    /// <summary>
    /// Sets the report type for status reporting. Injected by the bootstrap for push-triggered updates.
    /// </summary>
    /// <param name="reportType">1 = Upgrade (active poll), 2 = Push (SignalR push). Default is 1.</param>
    /// <remarks>
    /// When a push notification triggers the update (via SignalR hub), the caller should set this to 2
    /// so the server can distinguish push-triggered updates from active poll updates.
    /// </remarks>
    public void SetReportType(int reportType) => _reportType = reportType;

    /// <summary>
    /// Sets a custom download retry policy. Registered and injected by the bootstrap via <c>.DownloadPolicy&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="policy">Custom download policy instance. When <c>null</c>, the default retry behavior is used.</param>
    /// <remarks>
    /// Only effective when using the default download orchestrator (<c>DefaultDownloadOrchestrator</c>).
    /// If a custom orchestrator is set via <see cref="SetOrchestrator"/>, this policy is ignored.
    /// The download policy controls the wait time between retries and the maximum number of retries.
    /// </remarks>
    public void SetDownloadPolicy(Download.Abstractions.IDownloadPolicy? policy) => _customDownloadPolicy = policy;

    /// <summary>
    /// Sets a custom single-file download executor. Registered and injected by the bootstrap via <c>.DownloadExecutor&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="executor">Custom download executor instance. When <c>null</c>, the default HTTP download executor is used.</param>
    /// <remarks>
    /// Only effective when using the default download orchestrator (<c>DefaultDownloadOrchestrator</c>).
    /// If a custom orchestrator is set via <see cref="SetOrchestrator"/>, this executor is ignored.
    /// Can be used to implement file download via FTP, SFTP, or custom protocols.
    /// </remarks>
    public void SetDownloadExecutor(Download.Abstractions.IDownloadExecutor? executor) => _customDownloadExecutor = executor;

    /// <summary>
    /// Sets a custom download post-processing pipeline factory. Registered and injected by the bootstrap via <c>.DownloadPipeline&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="factory">Pipeline factory delegate that receives a file path and returns an <c>IDownloadPipeline</c> instance. When <c>null</c>, pipeline processing is skipped.</param>
    /// <remarks>
    /// Only effective when using the default download orchestrator (<c>DefaultDownloadOrchestrator</c>).
    /// If a custom orchestrator is set via <see cref="SetOrchestrator"/>, this factory is ignored.
    /// The pipeline factory is called after each file download completes and can be used for post-processing operations
    /// such as hash verification, decryption, or virus scanning.
    /// </remarks>
    public void SetDownloadPipelineFactory(Func<string?, Download.Abstractions.IDownloadPipeline>? factory) => _customDownloadPipelineFactory = factory;

    /// <summary>
    /// Initializes the strategy instance with global configuration information. Resolves the OS-specific strategy and passes the differential update pipeline.
    /// </summary>
    /// <param name="parameter">Global configuration information containing version number, install path, update URL, app secret key, etc.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This method is called before <see cref="ExecuteAsync"/> and completes the following initialization:
    /// <para>- Stores the configuration in the <c>_configInfo</c> field;</para>
    /// <para>- Resolves the platform strategy for the current OS via <see cref="ResolveOsStrategy"/>;</para>
    /// <para>- If the OS strategy inherits from <c>AbstractStrategy</c>, passes the pending differential pipeline (<c>DiffPipeline</c>).</para>
    /// </remarks>
    public void Create(UpdateContext parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
        if (_osStrategy is AbstractStrategy abs)
        {
            if (_pendingDiffPipeline != null) abs.DiffPipeline = _pendingDiffPipeline;
            abs.Reporter = this.Reporter;
        }
    }

    /// <summary>
    /// Entry method for executing the client update process. Cleans up conflicting processes, executes the standard workflow,
    /// and triggers error events and reports on exceptions.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the strategy has not been configured via <see cref="Create"/>.</exception>
    /// <remarks>
    /// <para>Execution flow:</para>
    /// <para>1. Calls <see cref="CallSmallBowlHomeAsync"/> to shut down potentially conflicting upgrade processes (Bowl).</para>
    /// <para>2. Calls <see cref="ExecuteWorkflowAsync"/> to execute the core update workflow.</para>
    /// <para>3. If the above steps throw an exception, safely invokes in order: error hook, failure report, log, and event dispatch.</para>
    /// <para>All exceptions are caught and dispatched as <see cref="ExceptionEventArgs"/> via <see cref="EventManager"/> without propagating upward.</para>
    /// </remarks>
    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("ClientStrategy not configured.");

        HasPreparedClientUpdate = false;

        try
        {
            GeneralTracer.Debug("ClientStrategy.ExecuteAsync start.");
            await CallSmallBowlHomeAsync(_configInfo.Bowl);
            await ExecuteWorkflowAsync();
        }
        catch (Exception ex)
        {
            var errCtx = BuildUpdateContext();
            await SafeOnUpdateErrorAsync(errCtx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(errCtx, ex).ConfigureAwait(false);
            GeneralTracer.Error("ClientStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    private DiffPipeline? _pendingDiffPipeline;

    /// <summary>
    /// Sets the differential update pipeline for parallel application of incremental patches on the OS strategy.
    /// </summary>
    /// <param name="diffPipeline">Differential update pipeline instance. If <c>null</c>, clears the pending pipeline.</param>
    /// <remarks>
    /// If the OS strategy (resolved via <see cref="ResolveOsStrategy"/>) is not yet initialized, the pipeline is stored in the
    /// <c>_pendingDiffPipeline</c> field and passed to the OS strategy's <c>DiffPipeline</c> property when <see cref="Create"/> is called.
    /// The differential pipeline supports parallel application of incremental patches, significantly speeding up upgrade package application.
    /// </remarks>
    public void SetDiffPipeline(DiffPipeline? diffPipeline)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.DiffPipeline = diffPipeline;
        else
            _pendingDiffPipeline = diffPipeline;
    }

    /// <summary>
    /// Starts the updated application. Delegates to the resolved OS-specific strategy.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method delegates the launch call to the underlying OS strategy (<see cref="WindowsStrategy"/>, <see cref="LinuxStrategy"/>, or <see cref="MacStrategy"/>).
    /// It is called within <see cref="LaunchUpgradeProcessAsync"/> to start the upgrade process.
    /// If <c>_osStrategy</c> is <c>null</c> (<see cref="Create"/> has not been called yet), the call is safely ignored.
    /// </remarks>
    public async Task StartAppAsync()
    {
        if (_osStrategy != null)
            await _osStrategy.StartAppAsync();
    }

    /// <summary>
    /// Registers an update pre-check callback. Called after version validation completes but before actual download and backup,
    /// allowing the update to be skipped based on business logic.
    /// </summary>
    /// <param name="func">Pre-check callback function that receives an <see cref="UpdateInfoEventArgs"/> parameter and returns <c>true</c> to skip the update, <c>false</c> to continue.</param>
    /// <returns>Returns the current <see cref="ClientStrategy"/> instance, supporting fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>The pre-check callback is invoked in the <see cref="CanSkip"/> method and is only effective for non-forced updates.</para>
    /// <para>The callback receives the full <see cref="UpdateInfoEventArgs"/> and can decide whether to skip based on business logic
    /// such as version number, release date, or update content.</para>
    /// <para>For example: skip an update when the new version only contains non-critical fixes and the current version is below a certain threshold.</para>
    /// </remarks>
    public ClientStrategy UseUpdatePrecheck(Func<UpdateInfoEventArgs, bool> func)
    {
        _updatePrecheck = func ?? throw new ArgumentNullException(nameof(func));
        return this;
    }

    #region Workflow

    /// <summary>
    /// Executes the core update workflow. Determines whether to run standard mode or silent mode based on runtime configuration.
    /// </summary>
    /// <remarks>
    /// The current implementation always calls <see cref="ExecuteStandardWorkflowAsync"/>.
    /// Runtime options (<c>Encoding</c>, <c>Format</c>, <c>DownloadTimeOut</c>, etc.)
    /// are already set on <c>_configInfo</c> by <c>Bootstrap.ApplyRuntimeOptions()</c>.
    /// </remarks>
    private async Task ExecuteWorkflowAsync()
    {
        // Standard mode �?silent mode is handled by GeneralUpdateBootstrap.LaunchSilentAsync().
        // Runtime options (Encoding, Format, DownloadTimeOut, etc.) are already
        // populated on _configInfo by Bootstrap.ApplyRuntimeOptions().
        await ExecuteStandardWorkflowAsync();
    }

    /// <summary>
    /// Executes the standard client update workflow. Includes version validation, event dispatch, skip check, hook invocation,
    /// backup, download, scenario dispatch, and upgrade process launch.
    /// </summary>
    /// <remarks>
    /// <para>Complete execution flow:</para>
    /// <para>
    /// <b>Step 1 - Version Validation</b>: Uses <c>DownloadSource</c> or the default <c>HttpDownloadSource</c> to call the server for version validation.
    /// The response contains the update asset manifest and update flags for the main program/upgrade program.
    /// </para>
    /// <para>
    /// <b>Step 2 - Scenario Determination</b>: Determines the update scenario based on the server's <c>HasMainUpdate</c> and <c>HasUpgradeUpdate</c> flags:
    /// <c>None</c> (no update needed), <c>UpgradeOnly</c> (upgrade program only), <c>MainOnly</c> (main program only), <c>Both</c> (both need updating).
    /// </para>
    /// <para>
    /// <b>Step 3 - Event Dispatch</b>: Constructs an <see cref="UpdateInfoEventArgs"/> and dispatches it via <c>EventManager.Instance.Dispatch</c>.
    /// </para>
    /// <para>
    /// <b>Step 4 - Skip Check</b>: If the update is not forced and the pre-check callback returns <c>true</c>, the update is skipped.
    /// </para>
    /// <para>
    /// <b>Step 5 - Pre-Update Hook</b>: Calls <c>Hooks.OnBeforeUpdateAsync</c>; cancels the update if it returns <c>false</c>.
    /// </para>
    /// <para>
    /// <b>Step 6 - Backup</b>: Backs up the current installation directory to a temporary directory (can be disabled via <c>BackupEnabled</c>).
    /// Also initializes the blacklist configuration to exclude files that do not need to be backed up.
    /// </para>
    /// <para>
    /// <b>Step 7 - Failed Version Check</b>: Checks whether the current version is a known failed upgrade via the <c>UpgradeFail</c> environment variable,
    /// avoiding repeated failed upgrades.
    /// </para>
    /// <para>
    /// <b>Step 8 - Download</b>: Downloads all update packages through the download orchestrator.
    /// Prefers the custom orchestrator (<c>_orchestrator</c>), otherwise uses <c>DefaultDownloadOrchestrator</c>,
    /// which supports custom retry policies, download executors, and post-processing pipelines.
    /// </para>
    /// <para>
    /// <b>Step 9 - Scenario Dispatch</b>: Executes different operations based on the determined scenario:
    /// <list type="bullet">
    /// <item><description><c>UpgradeOnly</c>: Calls <see cref="ApplyUpgradePackagesAsync"/> to apply upgrade packages in place.</description></item>
    /// <item><description><c>MainOnly</c>: Calls <see cref="SendProcessIpc"/> to send main program update information,
    /// then calls <see cref="LaunchUpgradeProcessAsync"/> to start the upgrade process.</description></item>
    /// <item><description><c>Both</c>: Applies upgrade packages first, then sends main program information and starts the upgrade process.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private async Task ExecuteStandardWorkflowAsync()
    {
        GeneralTracer.Info(
            $"ClientStrategy: validating client={_configInfo!.ClientVersion}, upgrade={_configInfo.UpgradeClientVersion}");

        // Discover identity metadata from the manifest in InstallPath BEFORE
        // constructing HttpDownloadSource so the server call sees real values.
        // UpdateStrategy writes versions back to InstallPath after each update;
        // using the parameterless Load() would read from BaseDirectory instead
        // and could pick up a stale manifest when InstallPath is customized.
        // Only fills empty fields — caller-provided values take precedence.
        AppMetadataDiscoverer.Discover(_configInfo!);

        // Use injected DownloadSource (Hub/HTTP), or default to HttpDownloadSource
        var downloadSource = DownloadSource ?? new Download.Sources.HttpDownloadSource(
            _configInfo.UpdateUrl,
            _configInfo.ClientVersion,
            _configInfo.UpgradeClientVersion,
            _configInfo.AppSecretKey,
            GetPlatform(),
            _configInfo.ProductId,
            _configInfo.Scheme,
            _configInfo.Token,
            _configInfo.AuthScheme,
            _configInfo.BasicUsername,
            _configInfo.BasicPassword);

        // Call server validation — returns assets from the two Validate calls
        var sourceResult = await downloadSource.ListAsync().ConfigureAwait(false);

        var localClientVersion = _configInfo!.ClientVersion;
        var localUpgradeVersion = _configInfo.UpgradeClientVersion ?? localClientVersion;

        // Pre-resolve upgrade version with client-version fallback so that
        // HasUpdate and Build agree on the same version. Build internally
        // falls back to clientVersion when upgradeClientVersion is null or
        // unparseable; applying the same fallback here avoids a mismatch where
        // the scenario says "update needed" but the download plan ends up empty.
        var resolvedUpgradeVersion =
            !string.IsNullOrWhiteSpace(localUpgradeVersion) ? localUpgradeVersion : localClientVersion;

        // ═══════════════════════════════════════════════════════════════
        // Version comparison: take max server version per AppType and
        // compare once against the local manifest version.
        // Each AppType uses its own version track — no cross-fallback.
        // ═══════════════════════════════════════════════════════════════
        _configInfo.IsMainUpdate = Download.DownloadPlanBuilder.HasUpdate(
            sourceResult.Assets,
            AppType.Client,
            localClientVersion);

        _configInfo.IsUpgradeUpdate = Download.DownloadPlanBuilder.HasUpdate(
            sourceResult.Assets,
            AppType.Upgrade,
            resolvedUpgradeVersion);

        var scenario = (_configInfo.IsMainUpdate, _configInfo.IsUpgradeUpdate) switch
        {
            (false, false) => UpdateScenario.None,
            (false, true) => UpdateScenario.UpgradeOnly,
            (true, false) => UpdateScenario.MainOnly,
            (true, true) => UpdateScenario.Both,
        };

        // Scenario None: local is already the latest — dispatch empty event and exit early
        if (scenario == UpdateScenario.None)
        {
            GeneralTracer.Info("ClientStrategy: local version is already the latest, no update needed.");
            var emptyResp = new VersionRespDTO
            {
                Code = 404,
                Body = new List<VersionEntry>(),
                Message = "No updates available."
            };
            EventManager.Instance.Dispatch(this, new UpdateInfoEventArgs(emptyResp));
            return;
        }

        // Build the download plan only when an update is actually needed
        var downloadPlan = Download.DownloadPlanBuilder.Build(
            sourceResult.Assets,
            localClientVersion,
            resolvedUpgradeVersion);
        _configInfo.LastVersion = downloadPlan.Assets.LastOrDefault()?.Version;
        GeneralTracer.Info($"ClientStrategy: Scenario={scenario}, AssetCount={downloadPlan.Assets.Count}");

        // Dispatch update info event with populated version data (full GeneralSpacestation-compatible fields)
        var versionInfos = downloadPlan.Assets.Select(a => new VersionEntry
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

        // Capture RecordIds per AppType for status reporting.
        // Client packages are reported by UpdateStrategy (Bowl) via IPC; Upgrade packages
        // are applied in-place and reported by ClientStrategy.
        //
        // Note: _mainRecordId treats null AppType as Client (matching the fallback in
        // downloadVersions at line ~530). _upgradeRecordId requires an explicit Upgrade
        // match — assets with omitted AppType are never treated as Upgrade packages.
        _mainRecordId = downloadPlan.Assets
            .FirstOrDefault(a => (a.AppType ?? (int)AppType.Client) == (int)AppType.Client)?.RecordId ?? 0;
        _upgradeRecordId = downloadPlan.Assets
            .FirstOrDefault(a => a.AppType == (int)AppType.Upgrade)?.RecordId ?? 0;
        EventManager.Instance.Dispatch(this, updateInfoArgs);

        var isForcibly = downloadPlan.IsForcibly;
        if (CanSkip(isForcibly, updateInfoArgs))
        {
            GeneralTracer.Info("ClientStrategy: update skipped.");
            return;
        }

        // Hooks: allow cancellation before download
        var hooksCtx = BuildUpdateContext();
        if (!await SafeOnBeforeUpdateAsync(hooksCtx).ConfigureAwait(false))
        {
            GeneralTracer.Info("ClientStrategy: update cancelled by OnBeforeUpdateAsync hook.");
            return;
        }

        // Report: update started
        await SafeReportUpdateStartedAsync(hooksCtx).ConfigureAwait(false);

        InitBlackPolicy();
        _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        // Check failed version
        if (!string.IsNullOrEmpty(_configInfo.LastVersion) && CheckFail(_configInfo.LastVersion))
        {
            GeneralTracer.Warn(
                $"ClientStrategy: version {_configInfo.LastVersion} matches known-failed upgrade.");
            return;
        }

        // Backup — conditionally skipped when BackupEnabled is false
        if (_configInfo.BackupEnabled != false)
        {
            Backup();
        }
        else
        {
            GeneralTracer.Info("ClientStrategy: backup skipped (BackupEnabled=false).");
        }

        _osStrategy!.Create(_configInfo);

        // Download ALL packages via orchestrator (requirement 6: client downloads everything
        // regardless of whether client or upgrade needs updating)
        var orchOptions = Download.Models.DownloadOrchestratorOptions.From(_configInfo);
        GeneralTracer.Info($"ClientStrategy: downloading {downloadPlan.Assets.Count} asset(s).");
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

        // Build VersionEntry list with AppType preserved from server response.
        var downloadVersions = downloadPlan.Assets.Select(a => new VersionEntry
        {
            RecordId = a.RecordId,
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
            $"ClientStrategy: Upgrade packages={upgradeVersions.Count}, MainApp packages={clientVersions.Count}");

        // ── Dispatch by scenario — one switch, four states, zero nested if-else ──
        switch (scenario)
        {
            case UpdateScenario.UpgradeOnly:
                await ApplyUpgradePackagesAsync(upgradeVersions).ConfigureAwait(false);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx, _upgradeRecordId).ConfigureAwait(false);
                GeneralTracer.Info("ClientStrategy: Upgrade-only update applied, client continues running.");
                break;

            case UpdateScenario.MainOnly:
                SendProcessIpc(clientVersions);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx, _mainRecordId).ConfigureAwait(false);
                if (LaunchAfterPrepare)
                {
                    await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
                    await LaunchUpgradeProcessAsync().ConfigureAwait(false);
                }
                break;

            case UpdateScenario.Both:
                await ApplyUpgradePackagesAsync(upgradeVersions).ConfigureAwait(false);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx, _upgradeRecordId).ConfigureAwait(false);
                SendProcessIpc(clientVersions);
                if (LaunchAfterPrepare)
                {
                    await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
                    await LaunchUpgradeProcessAsync().ConfigureAwait(false);
                }
                break;
            case UpdateScenario.None:
            default:
                throw new InvalidOperationException($"Unhandled update scenario: {scenario}");
        }
    }

    #endregion

    #region Scenario actions

    /// <summary>
    /// Applies upgrade (Upgrade) update packages in place. Delegates the actual patch application to the OS strategy.
    /// </summary>
    /// <param name="upgradeVersions">The list of version information for the upgrade program.</param>
    /// <remarks>
    /// This method only processes update packages of type <c>AppType.Upgrade</c>.
    /// It sets the update package paths in <c>_configInfo.UpdateVersions</c> and then delegates to the OS strategy
    /// (such as <see cref="WindowsStrategy"/>) to apply patches one by one through its pipeline (incremental/full).
    /// </remarks>
    private async Task ApplyUpgradePackagesAsync(List<VersionEntry> upgradeVersions)
    {
        if (upgradeVersions.Count == 0) return;
        GeneralTracer.Info("ClientStrategy: applying Upgrade packages in place.");
        _configInfo!.UpdateVersions = upgradeVersions;
        _osStrategy!.Create(_configInfo);
        await _osStrategy.ExecuteAsync().ConfigureAwait(false);

        // Only advance the manifest version when every package was applied
        // successfully. AbstractStrategy catches per-package failures and
        // continues the loop, so ExecuteAsync() completing is not a
        // reliable success signal on its own.
        if ((_osStrategy as AbstractStrategy)?.AllPackagesSucceeded == true)
            WriteBackUpgradeVersion(upgradeVersions, _configInfo!.InstallPath);
    }

    /// <summary>
    /// Serializes the main program (Client) update information as <c>ProcessContract</c> and sends it to the upgrade process via encrypted IPC.
    /// </summary>
    /// <param name="clientVersions">The list of version information for the main program.</param>
    /// <remarks>
    /// <para>This method performs the following operations:</para>
    /// <para>1. Uses <c>ConfigurationMapper.MapToProcessContract</c> to map configuration information and version list into a <c>ProcessContract</c> object;</para>
    /// <para>2. Serializes the <c>ProcessContract</c> as a JSON string and stores it in <c>_configInfo.ProcessContract</c>;</para>
    /// <para>3. Sends the encrypted process information to the upgrade process via <c>EncryptedFileProcessContractProvider</c>.</para>
    /// <para>After the upgrade process (Bowl) receives this information, it will perform the actual installation and replacement operations
    /// based on the <c>ProcessContract</c>.</para>
    /// </remarks>
    private void SendProcessIpc(List<VersionEntry> clientVersions)
    {
        var processInfo = ConfigurationMapper.MapToProcessContract(
            _configInfo!, clientVersions,
            _configInfo!.Formats ?? BlackDefaults.DefaultFormats,
            _configInfo.Files ?? BlackDefaults.DefaultFiles,
            _configInfo.Directories ?? BlackDefaults.DefaultDirectories,
            _reportType);

        _configInfo.ProcessContract = JsonSerializer.Serialize(processInfo,
            ProcessContractJsonContext.Default.ProcessContract);
        new EncryptedFileProcessContractProvider().Send(processInfo);
        HasPreparedClientUpdate = true;
        GeneralTracer.Info("ClientStrategy: ProcessContract sent with MainApp versions only.");
    }

    /// <summary>
    /// Launches the upgrade process (Bowl/Upgrade App), delegating to the OS strategy for platform-specific process startup.
    /// </summary>
    /// <remarks>
    /// <para>Configures the OS strategy launch parameters before starting:</para>
    /// <para>- <c>LaunchAppName</c>: Set to <c>_configInfo.UpdateAppName</c>, specifying the upgrade program file name to launch;</para>
    /// <para>- <c>LaunchBowl</c>: Set to <c>false</c> to avoid recursively launching the Bowl process;</para>
    /// <para>- <c>UseUpdatePath</c>: Determined by whether <c>_configInfo.UpdatePath</c> is empty.</para>
    /// <para>After calling <see cref="StartAppAsync"/>, the upgrade process takes over the subsequent installation and replacement operations.</para>
    /// </remarks>
    private async Task LaunchUpgradeProcessAsync()
    {
        if (_osStrategy is AbstractStrategy abs)
        {
            abs.LaunchAppName = _configInfo!.UpdateAppName;
            abs.LaunchBowl = false;
            abs.UseUpdatePath = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath);
        }

        GeneralTracer.Info(
            $"ClientStrategy: launching upgrade process {_configInfo!.UpdateAppName} via OS strategy.");
        await _osStrategy!.StartAppAsync();
    }

    /// <summary>
    /// Synchronously launches the upgrade process via the configured OS strategy
    /// after running the pre-launch lifecycle hook. Designed for
    /// <see cref="Silent.SilentPollOrchestrator"/> to call from
    /// <see cref="AppDomain.ProcessExit"/>, where the process is already shutting
    /// down and only path resolution + hook + process start are needed.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="LaunchUpgradeProcessAsync"/>, this method does NOT call
    /// <see cref="AbstractStrategy.StartAppAsync"/> — it only resolves the path
    /// via the strategy's <see cref="AbstractStrategy.ResolveAppPath"/> and starts
    /// the process, without the extra shutdown / Bowl / tracer-dispose work that
    /// is only appropriate when the current process is about to exit voluntarily.
    /// </remarks>
    internal void LaunchUpgradeProcessSync()
    {
        // Run the pre-launch lifecycle hook (e.g. UnixPermissionHooks for chmod +x).
        // In the standard flow this runs inside ExecuteStandardWorkflowAsync; in
        // silent mode it was deferred and must run now, before the process starts.
        var ctx = BuildUpdateContext();
        SafeOnBeforeStartAppAsync(ctx).GetAwaiter().GetResult();

        if (_osStrategy is AbstractStrategy abs)
        {
            abs.LaunchAppName = _configInfo!.UpdateAppName;
            abs.LaunchBowl = false;
            abs.UseUpdatePath = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath);
            abs.StartProcess(abs.LaunchAppName!, abs.UseUpdatePath);
            return;
        }

        // Fallback: custom IStrategy (non-AbstractStrategy).
        // For a custom strategy we can't use the platform path resolution, so we
        // fall back to a simple InstallPath + UpdateAppName lookup.
        var updaterDir = !string.IsNullOrWhiteSpace(_configInfo!.UpdatePath)
            ? (Path.IsPathRooted(_configInfo.UpdatePath)
                ? _configInfo.UpdatePath
                : Path.Combine(_configInfo.InstallPath, _configInfo.UpdatePath))
            : _configInfo.InstallPath;
        var appPath = Path.Combine(updaterDir, _configInfo.UpdateAppName);
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"Upgrade application not found: {appPath}");
        GeneralTracer.Info($"ClientStrategy: launching upgrade process {appPath}");
        Process.Start(appPath);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves the update strategy for the current operating system. Prefers a custom strategy, otherwise auto-selects based on the OS.
    /// </summary>
    /// <returns>An OS-specific strategy instance (<see cref="WindowsStrategy"/>, <see cref="LinuxStrategy"/>, or <see cref="MacStrategy"/>).</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current OS is not supported (not Windows, Linux, or macOS).</exception>
    /// <remarks>
    /// <para>Resolution priority:</para>
    /// <para>1. Custom strategy set via <see cref="SetOsStrategy"/> (injected by the bootstrap via <c>.Strategy&lt;T&gt;()</c>);</para>
    /// <para>2. Automatic OS detection via <c>RuntimeInformation.IsOSPlatform</c> to instantiate the corresponding strategy.</para>
    /// <para>If neither matches, a <see cref="PlatformNotSupportedException"/> is thrown.</para>
    /// </remarks>
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

    /// <summary>
    /// Initializes the file blacklist configuration. Used to exclude files, formats, and directories that do not need processing
    /// during backup and file operations.
    /// </summary>
    /// <remarks>
    /// Prefers the blacklist configured in <c>_configInfo</c>; if not configured, uses the defaults from <see cref="BlackDefaults"/>.
    /// The blacklist includes: excluded file names (e.g., config files), excluded file extensions (e.g., .log),
    /// and skipped directories (e.g., temporary directories).
    /// </remarks>
    private void InitBlackPolicy()
    {
        var effectiveConfig = new BlackPolicy(
            _configInfo!.Files?.Count > 0 ? _configInfo.Files : BlackDefaults.DefaultFiles,
            _configInfo.Formats?.Count > 0 ? _configInfo.Formats : BlackDefaults.DefaultFormats,
            _configInfo.Directories?.Count > 0
                ? _configInfo.Directories
                : BlackDefaults.DefaultDirectories
        );
        StorageManager.BlackMatcher = new BlackMatcher(effectiveConfig);
    }

    /// <summary>
    /// Backs up the current installation directory to the specified backup directory for rollback on update failure.
    /// </summary>
    /// <remarks>
    /// The backup operation is performed via <c>StorageManager.Backup</c>, excluding directories configured in the blacklist.
    /// The backup directory path format is: {InstallPath}/backup_{ClientVersion}.
    /// This step can be skipped by setting <c>UpdateContext.BackupEnabled</c> to <c>false</c>.
    /// </remarks>
    private void Backup()
    {
        GeneralTracer.Info(
            $"ClientStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
        StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
            _configInfo.Directories ?? BlackDefaults.DefaultDirectories);
    }

    /// <summary>
    /// Determines whether the current update can be skipped.
    /// </summary>
    /// <param name="isForcibly">Whether the update is forced. A forced update cannot be skipped.</param>
    /// <param name="updateInfo">Update information event arguments containing the version list and response status.</param>
    /// <returns>Returns <c>true</c> if the update can be skipped; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// <para>Skip conditions:</para>
    /// <para>1. <paramref name="isForcibly"/> is <c>false</c> (non-forced update);</para>
    /// <para>2. The pre-check callback registered via <see cref="UseUpdatePrecheck"/> returns <c>true</c>.</para>
    /// <para>If either condition is not met (forced update or no pre-check callback), the update cannot be skipped.</para>
    /// <para>This method is called after event dispatch and before backup in <see cref="ExecuteStandardWorkflowAsync"/>.</para>
    /// </remarks>
    private bool CanSkip(bool isForcibly, UpdateInfoEventArgs updateInfo)
    {
        if (isForcibly) return false;
        return _updatePrecheck?.Invoke(updateInfo) == true;
    }

    /// <summary>
    /// Checks whether the specified version has been recorded as a known failed upgrade version.
    /// </summary>
    /// <param name="version">The version string to check.</param>
    /// <returns>Returns <c>true</c> if the version was previously marked as a failed upgrade and the failed version in the environment variable
    /// is greater than or equal to the specified version.</returns>
    /// <remarks>
    /// Reads the known failed version number from the <c>UpgradeFail</c> environment variable.
    /// If the <c>UpgradeFail</c> environment variable is empty or <paramref name="version"/> is empty, returns <c>false</c>.
    /// Version comparison uses the semantic version comparison of the <see cref="Version"/> class.
    /// This mechanism avoids repeatedly attempting known failed upgrades.
    /// </remarks>
    private bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;
        return new Version(fail) >= new Version(version);
    }

    /// <summary>
    /// Gets the platform type for the current running OS.
    /// </summary>
    /// <returns>The current platform type (<see cref="PlatformType.Windows"/>, <see cref="PlatformType.Linux"/>,
    /// <see cref="PlatformType.MacOS"/>, or <see cref="PlatformType.Unknown"/>).</returns>
    /// <remarks>
    /// Uses <c>RuntimeInformation.IsOSPlatform</c> for runtime detection.
    /// The return value is used to inform the server of the client platform when constructing <c>HttpDownloadSource</c>.
    /// </remarks>
    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    /// <summary>
    /// After upgrade packages have been applied in-place, writes the latest upgrade version
    /// back to <c>generalupdate.manifest.json</c> so the next poll cycle starts from the
    /// correct <c>UpgradeClientVersion</c>.
    /// </summary>
    /// <param name="upgradeVersions">
    /// The upgrade version list that was just applied. The last element carries the
    /// highest target version.
    /// </param>
    private static void WriteBackUpgradeVersion(List<VersionEntry> upgradeVersions, string installPath)
    {
        var latestVersion = upgradeVersions.LastOrDefault()?.Version;
        if (string.IsNullOrEmpty(latestVersion)) return;

        try
        {
            ManifestInfo.TryUpdateVersion(
                installPath,
                upgradeClientVersion: latestVersion);
            GeneralTracer.Info(
                $"ClientStrategy: UpgradeClientVersion updated to {latestVersion} in manifest.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn(
                $"ClientStrategy: failed to write back UpgradeClientVersion: {ex.Message}");
        }
    }

    /// <summary>
    /// Shuts down conflicting processes (Bowl upgrade process) by name to release file locks.
    /// </summary>
    /// <param name="processName">The name of the process to shut down (without extension). Skipped if null or whitespace.</param>
    /// <remarks>
    /// This method is called at the entry point of the update flow to ensure the upgrade process (Bowl) is not running,
    /// preventing file locks from causing subsequent backup or replacement operations to fail.
    /// The shutdown is performed gracefully via <c>GracefulExit.ShutdownAsync</c>.
    /// If the specified process does not exist or an exception occurs during shutdown, this method logs a warning
    /// but does not block the flow.
    /// </remarks>
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

    /// <summary>
    /// Builds an update context object for passing to hook and reporter methods.
    /// </summary>
    /// <returns>An <see cref="Hooks.HookContext"/> instance containing the current update information.</returns>
    private Hooks.HookContext BuildUpdateContext()
    {
        return new Hooks.HookContext(
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? AppDomain.CurrentDomain.BaseDirectory,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.Client
        );
    }

    /// <summary>
    /// Safely invokes the pre-update hook. If the hook throws an exception, logs a warning and returns <c>true</c> (allows the update to continue).
    /// </summary>
    /// <param name="ctx">The update context.</param>
    /// <returns>The value returned by the hook; returns <c>true</c> if the hook throws an exception.</returns>
    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.HookContext ctx)
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

    /// <summary>
    /// Safely invokes the pre-start-app hook. If the hook throws an exception, logs a warning and continues the flow.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeOnBeforeStartAppAsync(Hooks.HookContext ctx)
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

    /// <summary>
    /// Safely invokes the update error hook. If the hook throws an exception, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    /// <param name="error">The exception that occurred during the update.</param>
    private async Task SafeOnUpdateErrorAsync(Hooks.HookContext ctx, Exception error)
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

    /// <summary>
    /// Safely invokes the post-update hook (after upgrade packages are applied). If the hook throws an exception, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeOnAfterUpdateAsync(Hooks.HookContext ctx)
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

    /// <summary>
    /// Safely invokes the download completed hook. If the hook throws an exception, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeOnDownloadCompletedAsync(Hooks.HookContext ctx)
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

    /// <summary>
    /// Safely reports the update started status. If the report fails, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeReportUpdateStartedAsync(Hooks.HookContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Updating, _reportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely reports the download completed status. If the report fails, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeReportDownloadCompletedAsync(Hooks.HookContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Updating, _reportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report DownloadCompleted failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely reports the update failed status. If the report fails, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    /// <param name="error">The exception that caused the update to fail.</param>
    private async Task SafeReportUpdateFailedAsync(Hooks.HookContext ctx, Exception error)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Failure, _reportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely reports the update applied success status. If the report fails, logs a warning.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeReportUpdateAppliedAsync(Hooks.HookContext ctx, int recordId)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(recordId,
                    (int)Download.Reporting.UpdateStatus.Success, _reportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}");
        }
    }

    #endregion
}