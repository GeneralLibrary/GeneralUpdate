using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// Windows 平台专用的更新策略。
    /// 实现了针对 Windows 操作系统的更新流程，包括管道构建、哈希校验、解压缩和应用补丁。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 此类继承自 <c>AbstractStrategy</c>，提供了 Windows 环境下的完整更新生命周期管理。
    /// </para>
    /// <para>
    /// 核心流程：
    /// <list type="number">
    ///   <item><c>BuildPipeline</c> — 构建中间件管道，按顺序执行哈希校验→解压缩→（可选）应用补丁。</item>
    ///   <item><c>CreatePipelineContext</c> — 创建包含版本信息和补丁路径的管道上下文。</item>
    ///   <item><c>StartAppAsync</c> — 启动已更新的主应用程序（如果配置了 Bowl 辅助进程，也会同时启动），
    ///        然后释放跟踪器并优雅退出当前更新程序进程。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 补丁功能由 <c>PatchEnabled</c> 配置控制。当启用时，管道会包含 <c>PatchMiddleware</c>；
    /// 禁用时则跳过该步骤。
    /// </para>
    /// </remarks>
    public class WindowsStrategy : AbstractStrategy
    {
        /// <summary>
        /// 创建管道上下文，包含目标版本信息和补丁路径。
        /// </summary>
        /// <param name="version">目标版本信息。</param>
        /// <param name="patchPath">补丁文件的路径。</param>
        /// <returns>包含版本信息和补丁路径的 <c>PipelineContext</c> 实例。</returns>
        /// <remarks>
        /// 此方法被 <c>AbstractStrategy</c> 中的管道执行流程调用。
        /// 它会记录版本号和补丁路径，然后调用基类的 <c>CreatePipelineContext</c> 方法创建上下文对象。
        /// </remarks>
        protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
        {
            GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}");
            return base.CreatePipelineContext(version, patchPath);
        }

        /// <summary>
        /// 构建 Windows 平台的更新中间件管道。
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
            GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}");
            var builder = new PipelineBuilder(context)
                .UseMiddleware<HashMiddleware>()
                .UseMiddleware<CompressMiddleware>()
                .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
            return builder;
        }

        /// <summary>
        /// 异步启动已更新的主应用程序，然后退出当前更新程序进程。
        /// </summary>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// <para>
        /// 此方法在更新流程完成后调用。执行步骤如下：
        /// </para>
        /// <list type="number">
        ///   <item>使用 <c>LaunchAppName</c> 属性获取主应用程序名称，如果未设置则抛出异常。</item>
        ///   <item>调用 <c>ResolveAppPath</c> 解析应用程序的完整路径。</item>
        ///   <item>使用 <c>Process.Start</c> 启动主应用程序。</item>
        ///   <item>如果 <c>LaunchBowl</c> 为 true，还会启动 Bowl 辅助进程（用于界面交互或状态监控）。</item>
        ///   <item>释放 <c>GeneralTracer</c> 资源。</item>
        ///   <item>调用 <c>GracefulExit.CurrentProcessAsync()</c> 优雅终止更新程序进程。</item>
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
                var appPath = ResolveAppPath(appName, UseUpdatePath);
                if (string.IsNullOrEmpty(appPath))
                    throw new Exception($"Can't find the app {appName}!");

                GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.StartApp: launching app={appPath}");
                Process.Start(appPath);
                GeneralTracer.Info("GeneralUpdate.Core.WindowsStrategy.StartApp: app launched successfully.");

                if (LaunchBowl)
                {
                    var bowlAppPath = CheckPath(_configinfo.InstallPath, _configinfo.Bowl);
                    if (!string.IsNullOrEmpty(bowlAppPath))
                    {
                        GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.StartApp: launching Bowl process={bowlAppPath}");
                        Process.Start(bowlAppPath);
                    }
                }
            }
            catch (Exception e)
            {
                GeneralTracer.Error("The StartApp method in the GeneralUpdate.Core.WindowsStrategy class throws an exception.", e);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
            }
            finally
            {
                GeneralTracer.Info("GeneralUpdate.Core.WindowsStrategy.StartApp: releasing tracer and terminating updater process.");
                GeneralTracer.Dispose();
                await GracefulExit.CurrentProcessAsync();
            }
        }
    }
}
