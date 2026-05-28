using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Hooks;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// 定义更新策略的协定接口。
    /// 所有平台特定的更新策略（Windows、Linux、macOS 以及 OSS）都必须实现此接口，
    /// 以提供完整的更新生命周期管理。
    /// </summary>
    /// <remarks>
    /// <para>更新生命周期按以下顺序执行：</para>
    /// <list type="number">
    ///   <item><see cref="Create"/> — 使用全局配置初始化策略。</item>
    ///   <item><see cref="ExecuteAsync"/> — 执行更新流程（哈希校验、解压缩、应用补丁等）。</item>
    ///   <item><see cref="StartAppAsync"/> — 启动已更新的主应用程序并退出更新程序进程。</item>
    /// </list>
    /// <para>
    /// 如果需要扩展自定义策略，请继承此接口。
    /// 平台相关策略（如 <c>WindowsStrategy</c>、<c>LinuxStrategy</c>、<c>MacStrategy</c>）
    /// 通常继承自 <c>AbstractStrategy</c> 基类，该基类提供了管道执行的标准实现。
    /// </para>
    /// </remarks>
    public interface IStrategy
    {
        /// <summary>
        /// 获取或设置更新生命周期钩子，用于在更新前后执行自定义回调。
        /// </summary>
        /// <remarks>
        /// <see cref="IUpdateHooks"/> 提供了多个可重写的方法，包括：
        /// <see cref="IUpdateHooks.OnBeforeUpdateAsync"/>、<see cref="IUpdateHooks.OnAfterUpdateAsync"/>、
        /// <see cref="IUpdateHooks.OnBeforeStartAppAsync"/> 以及错误处理回调。
        /// 默认实现使用 <c>NoOpUpdateHooks</c>（空操作）。
        /// </remarks>
        IUpdateHooks Hooks { get; set; }

        /// <summary>
        /// 获取或设置更新状态报告器，用于向服务器或事件系统报告更新进度和结果。
        /// </summary>
        /// <remarks>
        /// 报告器可用于将更新状态（正在更新、成功、失败）上报给远程服务（如 GeneralSpacestation），
        /// 或通过 <c>EventManager</c> 触发本地事件。默认实现使用 <c>NoOpUpdateReporter</c>（空操作）。
        /// </remarks>
        IUpdateReporter Reporter { get; set; }

        /// <summary>
        /// 异步执行更新策略的主要流程。
        /// </summary>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法是更新流程的核心。典型的执行流程包括：
        /// <list type="bullet">
        ///   <item>下载更新包或从配置源获取版本信息。</item>
        ///   <item>通过中间件管道执行哈希校验、解压缩和应用补丁。</item>
        ///   <item>触发更新前后的生命周期钩子。</item>
        ///   <item>报告更新状态（开始、进度、完成或失败）。</item>
        /// </list>
        /// </remarks>
        Task ExecuteAsync();

        /// <summary>
        /// 异步启动已更新的主应用程序，然后退出当前的更新程序进程。
        /// </summary>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// <para>
        /// 此方法在更新流程完成后调用。它会：
        /// </para>
        /// <list type="number">
        ///   <item>解析主应用程序的可执行文件路径。</item>
        ///   <item>使用 <c>Process.Start</c> 启动主应用程序。</item>
        ///   <item>调用 <c>GracefulExit.CurrentProcessAsync()</c> 优雅地终止更新程序进程。</item>
        /// </list>
        /// <para>
        /// 对于 Windows 策略，如果配置了 <c>Bowl</c> 辅助进程，还会同时启动该进程。
        /// </para>
        /// </remarks>
        Task StartAppAsync();

        /// <summary>
        /// 使用全局配置信息创建并初始化策略实例。
        /// </summary>
        /// <param name="parameter">全局配置信息，包含安装路径、应用名称、版本号等设置。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="parameter"/> 为 null 时抛出。</exception>
        /// <remarks>
        /// 此方法必须在调用 <see cref="ExecuteAsync"/> 之前调用，以提供策略执行所需的全部配置参数。
        /// 配置信息包括 <c>InstallPath</c>、<c>MainAppName</c>、<c>UpdateAppName</c>、<c>ClientVersion</c>、
        /// <c>PatchEnabled</c> 等关键设置。
        /// </remarks>
        void Create(GlobalConfigInfo parameter);
    }
}