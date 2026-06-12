using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Upgrade-side update strategy. Receives process information passed from the client via encrypted IPC,
/// applies updates, and launches the main application.
/// </summary>
/// <remarks>
/// <para>
/// This strategy serves the <c>AppType.Upgrade</c> role and uses a two-layer strategy design:
/// the upper role strategy (this class) handles workflow orchestration,
/// while the lower OS-level strategy (<see cref="WindowsStrategy"/>, <see cref="LinuxStrategy"/>, <see cref="MacStrategy"/>)
/// handles platform-specific operations.
/// </para>
/// <para>
/// <b>Execution Flow:</b>
/// <list type="number">
///   <item><description>Receives the <see cref="UpdateContext"/> passed from the client via the <see cref="Create"/> method,
///   which contains already-downloaded update package paths, hash values, and other metadata.</description></item>
///   <item><description>Calls the <see cref="Hooks.IUpdateHooks.OnBeforeUpdateAsync"/> lifecycle hook,
///   allowing the caller to execute custom logic or cancel the operation before applying updates.</description></item>
///   <item><description>Delegates to the OS strategy to execute the update pipeline: processes each version through the
///   <c>Hash</c> (hash verification) → <c>Decompress</c> (extraction) → <c>Patch</c> (incremental patch) middleware chain.</description></item>
///   <item><description>Calls the <see cref="Hooks.IUpdateHooks.OnAfterUpdateAsync"/> hook to notify the caller that all updates have been applied.</description></item>
///   <item><description>Calls the <see cref="Hooks.IUpdateHooks.OnBeforeStartAppAsync"/> hook,
///   allowing the caller to perform additional operations before launching the main application
///   (such as setting executable permissions or preparing resource files).</description></item>
///   <item><description>Launches the main application (<c>MainAppName</c>) and the Bowl helper process through the OS strategy.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Design Note:</b> The upgrade side does not perform version validation or download operations.
/// The client has already completed all network requests and downloads, passing results through process information.
/// The upgrade side is responsible only for applying updates and launching the application -- zero network overhead.
/// </para>
/// </remarks>
public class UpdateStrategy : IStrategy
{
    private UpdateContext? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;
    private int _reportType = 1; // 1=Upgrade(active poll), 2=Push(SignalR push)

    /// <summary>
    /// Gets or sets the lifecycle hooks. Injected by the bootstrap to execute custom logic at key points in the update flow.
    /// </summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// Gets or sets the update status reporter. Injected by the bootstrap to report update progress and results to the server or caller.
    /// </summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.HttpUpdateReporter();

    /// <summary>
    /// Sets a custom OS-level strategy (injected via <c>.Strategy&lt;T&gt;()</c>).
    /// When set, replaces the automatic platform detection logic in <see cref="ResolveOsStrategy"/>.
    /// </summary>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>
    /// Sets the report type for status reporting. The caller (ClientStrategy) should pass the same
    /// report type it used, so the server can distinguish push-triggered updates from active polls.
    /// </summary>
    /// <param name="reportType">1 = Upgrade (active poll), 2 = Push (SignalR push). Default is 1.</param>
    public void SetReportType(int reportType) => _reportType = reportType;

    /// <summary>
    /// Initializes the upgrade-side strategy. Receives the global configuration information passed from the client
    /// and resolves the strategy instance for the current operating system.
    /// </summary>
    /// <param name="parameter">Global configuration information containing update package paths, hash values, version information, etc.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is null.</exception>
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
    /// Executes the upgrade-side update flow. Follows the lifecycle order: pre-update hook, OS update pipeline,
    /// post-update hook, pre-start-app hook, and main application launch.
    /// </summary>
    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("UpdateStrategy not configured.");

        var ctx = BuildUpdateContext();
        try
        {
            GeneralTracer.Debug("UpdateStrategy.ExecuteAsync start.");

            // Hooks: allow cancellation before applying updates
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("UpdateStrategy: update cancelled by OnBeforeUpdateAsync hook.");
                return;
            }

            _osStrategy!.Create(_configInfo);

            // Apply MainApp updates -- Client already applied Upgrade packages, IPC only has MainApp versions
            var pipelineSucceeded = true;
            if (_configInfo.UpdateVersions?.Count > 0)
            {
                GeneralTracer.Info("UpdateStrategy: applying " + _configInfo.UpdateVersions.Count +
                                   " MainApp update(s).");
                await _osStrategy.ExecuteAsync();

                // Only advance the manifest version when every package was applied
                // successfully. AbstractStrategy catches per-package failures and
                // continues the loop, so ExecuteAsync() completing is not a
                // reliable success signal on its own.
                // For custom IStrategy implementations that don't expose
                // AllPackagesSucceeded, assume success (coalesce to true)
                // since no failure was signalled via an exception.
                pipelineSucceeded = (_osStrategy as AbstractStrategy)?.AllPackagesSucceeded ?? true;
                if (pipelineSucceeded)
                {
                    WriteBackClientVersion();
                }
                else
                {
                    GeneralTracer.Warn("UpdateStrategy: one or more MainApp packages failed, " +
                                       "skipping manifest write and app launch.");
                }
            }
            else
            {
                GeneralTracer.Info("UpdateStrategy: no updates to apply, starting application directly.");
            }

            // When main app updates failed, do NOT launch the client — doing so would
            // restart it with old files, causing it to re-detect the update and loop.
            if (!pipelineSucceeded)
            {
                var failEx = new InvalidOperationException("MainApp pipeline did not complete successfully.");
                await SafeOnUpdateErrorAsync(ctx, failEx).ConfigureAwait(false);
                await SafeReportUpdateFailedAsync(ctx, failEx).ConfigureAwait(false);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(failEx, failEx.Message));
                return;
            }

            // Hooks: after all updates applied
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);

            // Report: update applied successfully — uses the first Client package's RecordId
            await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);

            // Hooks: before starting main app (e.g. chmod +x on Linux/macOS)
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            // Delegate to OS strategy: launch MainAppName + Bowl.
            // Skip if silent mode requested no-launch (e.g. maintenance windows).
            if (_configInfo.LaunchClientAfterUpdate)
            {
                if (_osStrategy is AbstractStrategy abs2)
                {
                    abs2.LaunchAppName = _configInfo.MainAppName;
                    abs2.LaunchBowl = true;
                }

                await _osStrategy.StartAppAsync();
            }
            else
            {
                GeneralTracer.Info("UpdateStrategy: LaunchClientAfterUpdate=false, skipping app launch.");
            }
        }
        catch (Exception ex)
        {
            await SafeOnUpdateErrorAsync(ctx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(ctx, ex).ConfigureAwait(false);
            GeneralTracer.Error("UpdateStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    private DiffPipeline? _pendingDiffPipeline;

    /// <summary>
    /// Sets the differential patch pipeline on the underlying OS-level strategy for parallel patch application.
    /// </summary>
    /// <param name="diffPipeline">The differential pipeline instance. If <c>null</c>, clears the pending pipeline.</param>
    public void SetDiffPipeline(DiffPipeline? diffPipeline)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.DiffPipeline = diffPipeline;
        else
            _pendingDiffPipeline = diffPipeline;
    }

    /// <summary>
    /// Starts the main application. Delegates to the underlying OS strategy for platform-specific application launch logic.
    /// </summary>
    public async Task StartAppAsync()
    {
        if (_osStrategy != null)
            await _osStrategy.StartAppAsync();
    }

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

    private Hooks.HookContext BuildUpdateContext()
    {
        return new Hooks.HookContext(
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? AppDomain.CurrentDomain.BaseDirectory,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.Upgrade
        );
    }

    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.HookContext ctx)
    {
        try { return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}"); return true; }
    }

    private async Task SafeOnAfterUpdateAsync(Hooks.HookContext ctx)
    {
        try { await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeOnBeforeStartAppAsync(Hooks.HookContext ctx)
    {
        try { await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeOnUpdateErrorAsync(Hooks.HookContext ctx, Exception error)
    {
        try { await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateAppliedAsync(Hooks.HookContext ctx)
    {
        try
        {
            var recordId = _configInfo?.UpdateVersions?.FirstOrDefault()?.RecordId ?? 0;
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(recordId,
                    (int)Download.Reporting.UpdateStatus.Success, _reportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}");
        }
    }

    private async Task SafeReportUpdateFailedAsync(Hooks.HookContext ctx, Exception error)
    {
        try
        {
            var recordId = _configInfo?.UpdateVersions?.FirstOrDefault()?.RecordId ?? 0;
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(recordId,
                    (int)Download.Reporting.UpdateStatus.Failure, _reportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}");
        }
    }

    /// <summary>
    /// After the main-app update pipeline completes, writes the new <c>ClientVersion</c>
    /// back to <c>generalupdate.manifest.json</c> in the client's install directory so the
    /// next poll cycle starts from the correct version.
    /// </summary>
    private void WriteBackClientVersion()
    {
        // Use the latest version from the applied update list; fall back to LastVersion
        // (which covers the single-package / full-update case).
        var latestVersion = _configInfo?.UpdateVersions?.LastOrDefault()?.Version
                            ?? _configInfo?.LastVersion;
        if (string.IsNullOrEmpty(latestVersion)) return;

        try
        {
            ManifestInfo.TryUpdateVersion(
                _configInfo!.InstallPath,
                clientVersion: latestVersion);
            GeneralTracer.Info(
                $"UpdateStrategy: ClientVersion updated to {latestVersion} in manifest.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn(
                $"UpdateStrategy: failed to write back ClientVersion: {ex.Message}");
        }
    }

    #endregion
}