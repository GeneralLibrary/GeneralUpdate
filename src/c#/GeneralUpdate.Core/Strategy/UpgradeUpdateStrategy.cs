using GeneralUpdate.Core.Differential;
using GeneralUpdate.Differential.Abstractions;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Upgrade-side update strategy. Receives process info from the client side,
/// applies updates via the pipeline, and starts the main application.
/// </summary>
/// <remarks>
/// This is the AppType.Upgrade role strategy. It composes an OS-specific
/// strategy for platform operations (Windows/Linux/Mac).
///
/// <b>Design:</b> Upgrade does NOT validate versions or download packages.
/// The client has already validated versions, downloaded all packages, and
/// passed the results via ProcessInfo. Upgrade only applies updates and
/// starts the main application -- zero network.
/// </remarks>
public class UpgradeUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;

    /// <summary>Lifecycle hooks injected by the bootstrap.</summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>Update status reporter injected by the bootstrap.</summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

    /// <summary>Sets a custom OS-level strategy (injected via <c>.Strategy&lt;T&gt;()</c>).
    /// When set, this replaces the automatic platform detection in <see cref="ResolveOsStrategy"/>.</summary>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

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
        if (_configInfo == null) throw new InvalidOperationException("UpgradeUpdateStrategy not configured.");

        var ctx = BuildUpdateContext();
        try
        {
            GeneralTracer.Debug("UpgradeUpdateStrategy.ExecuteAsync start.");

            // Hooks: allow cancellation before applying updates
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("UpgradeUpdateStrategy: update cancelled by OnBeforeUpdateAsync hook.");
                return;
            }

            _osStrategy!.Create(_configInfo);

            // Apply MainApp updates -- Client already applied Upgrade packages, IPC only has MainApp versions
            if (_configInfo.UpdateVersions?.Count > 0)
            {
                GeneralTracer.Info("UpgradeUpdateStrategy: applying " + _configInfo.UpdateVersions.Count +
                                   " MainApp update(s).");
                await _osStrategy.ExecuteAsync();
            }
            else
            {
                GeneralTracer.Info("UpgradeUpdateStrategy: no updates to apply, starting application directly.");
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
                GeneralTracer.Info("UpgradeUpdateStrategy: LaunchClientAfterUpdate=false, skipping app launch.");
            }
        }
        catch (Exception ex)
        {
            await SafeOnUpdateErrorAsync(ctx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(ctx, ex).ConfigureAwait(false);
            GeneralTracer.Error("UpgradeUpdateStrategy.ExecuteAsync failed.", ex);
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