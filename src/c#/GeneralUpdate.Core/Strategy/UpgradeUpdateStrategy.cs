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
/// 升级端更新策略。接收客户端通过加密 IPC 传递的进程信息，应用更新并启动主应用程序。
/// </summary>
/// <remarks>
/// <para>
/// 本策略对应 <c>AppType.Upgrade</c> 角色，采用两层策略设计：上层角色策略（本类）负责编排更新流程，
/// 下层操作系统策略（<see cref="WindowsStrategy"/>、<see cref="LinuxStrategy"/>、<see cref="MacStrategy"/>）
/// 负责执行具体的平台操作。
/// </para>
/// <para>
/// <b>执行流程：</b>
/// <list type="number">
///   <item><description>通过 <see cref="Create"/> 方法接收客户端传递的 <see cref="GlobalConfigInfo"/>，
///   其中包含已下载的更新包路径、哈希值等元数据。</description></item>
///   <item><description>调用 <see cref="Hooks.IUpdateHooks.OnBeforeUpdateAsync"/> 生命周期钩子，
///   允许调用方在应用更新前执行自定义逻辑或取消操作。</description></item>
///   <item><description>委托操作系统策略执行更新管道：通过 <c>Hash</c>（哈希校验）→
///   <c>Decompress</c>（解压缩）→ <c>Patch</c>（增量补丁）中间件链处理每个更新版本。</description></item>
///   <item><description>调用 <see cref="Hooks.IUpdateHooks.OnAfterUpdateAsync"/> 钩子，通知调用方所有更新已应用完毕。</description></item>
///   <item><description>调用 <see cref="Hooks.IUpdateHooks.OnBeforeStartAppAsync"/> 钩子，
///   允许调用方在启动主应用程序前执行额外操作（如设置可执行权限或准备资源文件）。</description></item>
///   <item><description>通过操作系统策略启动主应用程序（<c>MainAppName</c>）及 Bowl 辅助进程。</description></item>
/// </list>
/// </para>
/// <para>
/// <b>设计要点：</b>升级端不执行版本验证或下载操作。客户端已完成所有网络请求和下载任务，
/// 并通过进程信息传递结果。升级端仅负责应用更新和启动应用程序——零网络开销。
/// </para>
/// </remarks>
public class UpgradeUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;

    /// <summary>
    /// 获取或设置生命周期钩子。由引导程序注入，用于在更新流程的关键节点执行自定义逻辑。
    /// </summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// 获取或设置更新状态报告器。由引导程序注入，负责向服务器或调用方报告更新进度和结果。
    /// </summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

    /// <summary>
    /// 设置自定义操作系统级别策略（通过 <c>.Strategy&lt;T&gt;()</c> 注入）。
    /// 设置后将替换 <see cref="ResolveOsStrategy"/> 中的自动平台检测逻辑。
    /// </summary>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>
    /// 初始化升级端策略。接收客户端传递的全局配置信息，并解析当前操作系统对应的策略实例。
    /// </summary>
    /// <param name="parameter">全局配置信息，包含更新包路径、哈希值、版本信息等。</param>
    /// <exception cref="ArgumentNullException"><paramref name="parameter"/> 为 null 时抛出。</exception>
    /// <exception cref="PlatformNotSupportedException">当前操作系统不受支持时由 <see cref="ResolveOsStrategy"/> 抛出。</exception>
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
    /// 执行升级端更新流程。按照生命周期顺序依次执行：更新前钩子、操作系统更新管道、更新后钩子、启动前钩子、启动主应用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>执行流程详解：</b>
    /// <list type="number">
    ///   <item><description><b>OnBeforeUpdate 钩子：</b>调用 <see cref="Hooks.IUpdateHooks.OnBeforeUpdateAsync"/>，
    ///   如果返回 <c>false</c> 则取消本次更新。</description></item>
    ///   <item><description><b>操作系统更新管道：</b>将 <c>_configInfo.UpdateVersions</c> 传递给
    ///   <see cref="IStrategy.ExecuteAsync"/>，由 OS 策略逐一处理每个版本的更新包
    ///   （<c>Hash</c> 校验 → <c>Decompress</c> 解压 → <c>Patch</c> 补丁应用）。</description></item>
    ///   <item><description><b>OnAfterUpdate 钩子：</b>调用 <see cref="Hooks.IUpdateHooks.OnAfterUpdateAsync"/>，
    ///   通知调用方所有更新已应用完成。</description></item>
    ///   <item><description><b>报告更新成功：</b>通过 <see cref="Download.Reporting.IUpdateReporter"/>
    ///   报告更新成功状态。</description></item>
    ///   <item><description><b>OnBeforeStartApp 钩子：</b>调用 <see cref="Hooks.IUpdateHooks.OnBeforeStartAppAsync"/>，
    ///   允许调用方在启动应用前执行额外操作（如设置可执行权限）。</description></item>
    ///   <item><description><b>启动应用：</b>当 <c>LaunchClientAfterUpdate</c> 为 <c>true</c> 时，
    ///   通过 OS 策略启动主应用程序（<c>MainAppName</c>）及 Bowl 辅助进程。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>异常处理：</b>整个流程中任何异常均会被 <c>try-catch</c> 捕获，依次触发
    /// <see cref="Hooks.IUpdateHooks.OnUpdateErrorAsync"/> 钩子、报告更新失败状态、
    /// 记录错误日志并通过 <see cref="EventManager"/> 分发异常事件。
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 启动主应用程序。委托给底层操作系统策略执行平台相关的应用启动逻辑。
    /// </summary>
    /// <remarks>
    /// 此方法由外部调用（如 Bowl 进程），用于在升级完成后启动主应用。
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