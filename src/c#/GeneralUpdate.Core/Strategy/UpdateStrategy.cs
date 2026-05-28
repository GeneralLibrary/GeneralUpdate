using System;
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
///   <item><description>Receives the <see cref="GlobalConfigInfo"/> passed from the client via the <see cref="Create"/> method,
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
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;

    /// <summary>
    /// Gets or sets the lifecycle hooks. Injected by the bootstrap to execute custom logic at key points in the update flow.
    /// </summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// Gets or sets the update status reporter. Injected by the bootstrap to report update progress and results to the server or caller.
    /// </summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

    /// <summary>
    /// Sets a custom OS-level strategy (injected via <c>.Strategy&lt;T&gt;()</c>).
    /// When set, replaces the automatic platform detection logic in <see cref="ResolveOsStrategy"/>.
    /// </summary>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>
    /// Initializes the upgrade-side strategy. Receives the global configuration information passed from the client
    /// and resolves the strategy instance for the current operating system.
    /// </summary>
    /// <param name="parameter">Global configuration information containing update package paths, hash values, version information, etc.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown by <see cref="ResolveOsStrategy"/> when the current OS is not supported.</exception>
    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
        if (_osStrategy is AbstractStrategy abs)
        {
            if (_pendingDiffPipeline != null) abs.DiffPipeline = _pendingDiffPipeline;
        }
    }

    /// <summary>
    /// Executes the upgrade-side update flow. Follows the lifecycle order: pre-update hook, OS update pipeline,
    /// post-update hook, pre-start-app hook, and main application launch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Detailed Execution Flow:</b>
    /// <list type="number">
    ///   <item><description><b>OnBeforeUpdate Hook:</b> Calls <see cref="Hooks.IUpdateHooks.OnBeforeUpdateAsync"/>.
    ///   If it returns <c>false</c>, the update is cancelled.</description></item>
    ///   <item><description><b>OS Update Pipeline:</b> Passes <c>_configInfo.UpdateVersions</c> to
    ///   <see cref="IStrategy.ExecuteAsync"/>, where the OS strategy processes each version's update package
    ///   (<c>Hash</c> verification → <c>Decompress</c> extraction → <c>Patch</c> application).</description></item>
    ///   <item><description><b>OnAfterUpdate Hook:</b> Calls <see cref="Hooks.IUpdateHooks.OnAfterUpdateAsync"/>
    ///   to notify the caller that all updates have been applied.</description></item>
    ///   <item><description><b>Report Success:</b> Reports the update success status via
    ///   <see cref="Download.Reporting.IUpdateReporter"/>.</description></item>
    ///   <item><description><b>OnBeforeStartApp Hook:</b> Calls <see cref="Hooks.IUpdateHooks.OnBeforeStartAppAsync"/>,
    ///   allowing the caller to perform additional operations before launching the application
    ///   (such as setting executable permissions).</description></item>
    ///   <item><description><b>Launch Application:</b> When <c>LaunchClientAfterUpdate</c> is <c>true</c>,
    ///   launches the main application (<c>MainAppName</c>) and the Bowl helper process via the OS strategy.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Exception Handling:</b> Any exception in the entire flow is caught by the <c>try-catch</c> block,
    /// which sequentially triggers <see cref="Hooks.IUpdateHooks.OnUpdateErrorAsync"/>, reports the update failure status,
    /// logs the error, and dispatches the exception event via <see cref="EventManager"/>.
    /// </para>
    /// </remarks>
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
            if (_configInfo.UpdateVersions?.Count > 0)
            {
                GeneralTracer.Info("UpdateStrategy: applying " + _configInfo.UpdateVersions.Count +
                                   " MainApp update(s).");
                await _osStrategy.ExecuteAsync();
            }
            else
            {
                GeneralTracer.Info("UpdateStrategy: no updates to apply, starting application directly.");
            }

            // Hooks: after all updates applied
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);

            // Report: update applied successfully
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
    /// <remarks>
    /// If the OS strategy is not yet initialized, the pipeline is stored in a pending field
    /// and passed to the OS strategy's <c>DiffPipeline</c> property when <see cref="Create"/> is called.
    /// </remarks>
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
    /// <remarks>
    /// This method is called externally (e.g., by the Bowl process) to start the main application after the upgrade completes.
    /// </remarks>
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
            AppType.Upgrade
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

    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Success,
                    1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}");
        }
    }

    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter
                .ReportAsync(
                    new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Failure, 1))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}");
        }
    }

    #endregion
}