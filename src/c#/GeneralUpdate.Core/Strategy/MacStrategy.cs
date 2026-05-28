using System;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// macOS 平台专用的更新策略。
/// 实现了针对 macOS 操作系统的更新流程，包括管道构建、哈希校验、解压缩和应用补丁。
/// </summary>
/// <remarks>
/// <para>
/// 此类继承自 <c>AbstractStrategy</c>，提供了 macOS 环境下的完整更新生命周期管理。
/// 其管道流程与 Linux 策略相似，但在 <c>StartAppAsync</c> 中会额外验证文件是否存在。
/// </para>
/// <para>
/// 核心流程：
/// <list type="number">
///   <item><c>BuildPipeline</c> — 构建中间件管道，按顺序执行哈希校验→解压缩→（可选）应用补丁。</item>
///   <item><c>ExecuteAsync</c> — 执行基类的管道流程，并记录开始信息（使用 <c>ConfigureAwait(false)</c> 避免上下文切换死锁）。</item>
///   <item><c>Create</c> — 直接保存配置信息到内部字段。</item>
///   <item><c>StartAppAsync</c> — 启动已更新的主应用程序，然后退出当前更新程序进程。</item>
/// </list>
/// </para>
/// </remarks>
public class MacStrategy : AbstractStrategy
{
    /// <summary>
    /// 异步执行更新策略的主流程。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// 此方法首先记录执行开始信息，然后调用基类的 <c>ExecuteAsync</c> 方法执行实际的管道流程。
    /// 使用 <c>ConfigureAwait(false)</c> 配置以避免在 UI 上下文中发生死锁。
    /// </remarks>
    public override async Task ExecuteAsync()
    {
        GeneralTracer.Info("MacStrategy: executing pipeline");
        await base.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 异步启动已更新的主应用程序，然后退出当前更新程序进程。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// <para>
    /// 此方法在更新流程完成后调用。与 Windows/Linux 策略的不同之处在于：
    /// </para>
    /// <list type="number">
    ///   <item>使用 <c>LaunchAppName</c> 获取主应用程序名称，如果未设置则抛出异常。</item>
    ///   <item>调用 <c>ResolveAppPath</c> 解析应用程序路径。</item>
    ///   <item>在启动前使用 <c>File.Exists</c> 验证文件是否存在（macOS 特有）。</item>
    ///   <item>使用 <c>Process.Start</c> 启动主应用程序。</item>
    ///   <item>释放 <c>GeneralTracer</c> 资源并调用 <c>GracefulExit.CurrentProcessAsync()</c> 退出更新程序进程。</item>
    /// </list>
    /// <para>
    /// 任何异常都会被 <c>EventManager</c> 捕获并分发为 <c>ExceptionEventArgs</c> 事件。
    /// </para>
    /// </remarks>
    public override async Task StartAppAsync()
    {
        try
        {
            var appName = LaunchAppName ?? throw new InvalidOperationException("LaunchAppName must be set before calling StartAppAsync.");
            var mainApp = ResolveAppPath(appName, UseUpdatePath);

            if (!string.IsNullOrEmpty(mainApp) && File.Exists(mainApp))
            {
                GeneralTracer.Info($"MacStrategy.StartApp: launching app={mainApp}");
                System.Diagnostics.Process.Start(mainApp);
            }
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StartApp method in MacStrategy threw an exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            GeneralTracer.Info("MacStrategy.StartApp: releasing tracer and terminating updater process.");
            GeneralTracer.Dispose();
            await GracefulExit.CurrentProcessAsync();
        }
    }

    /// <summary>
    /// 使用全局配置信息创建并初始化策略实例。
    /// </summary>
    /// <param name="configInfo">全局配置信息，包含安装路径、应用名称、版本号等设置。</param>
    /// <remarks>
    /// macOS 策略的 <c>Create</c> 实现直接保存配置信息到内部字段 <c>_configinfo</c>，
    /// 而不调用基类的实现。这提供了更轻量级的初始化方式。
    /// </remarks>
    public override void Create(GlobalConfigInfo configInfo) => _configinfo = configInfo;

    /// <summary>
    /// 构建 macOS 平台的更新中间件管道。
    /// </summary>
    /// <param name="context">管道上下文，包含版本和补丁信息。</param>
    /// <returns>配置好的 <c>PipelineBuilder</c> 实例，包含哈希校验、解压缩和（可选）补丁中间件。</returns>
    /// <remarks>
    /// <para>
    /// 管道按以下顺序组装中间件：
    /// </para>
    /// <list type="number">
    ///   <item><c>HashMiddleware</c> — 计算并验证文件哈希，确保数据完整性。</item>
    ///   <item><c>CompressMiddleware</c> — 解压缩下载的更新包。</item>
    ///   <item><c>PatchMiddleware</c> — （可选）应用二进制补丁。仅在 <c>_configinfo.PatchEnabled</c> 为 true 时启用。</item>
    /// </list>
    /// </remarks>
    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        GeneralTracer.Info($"MacStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}");
        var builder = new PipelineBuilder(context)
            .UseMiddleware<HashMiddleware>()
            .UseMiddleware<CompressMiddleware>()
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
        return builder;
    }
}
