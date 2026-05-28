using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline
{
    /// <summary>
    /// 管道构建器，采用 FIFO（先进先出）顺序注册和执行中间件。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PipelineBuilder"/> 是 GeneralUpdate 管道模式的核心编排组件。
    /// 它管理一个 <see cref="IMiddleware"/> 实例的不可变队列，中间件按注册顺序排队，
    /// 在调用 <see cref="Build"/> 时按 FIFO 顺序依次异步执行。
    /// </para>
    /// <para>
    /// 典型的更新管道流程如下：
    /// <list type="number">
    ///   <item><description><see cref="HashMiddleware"/> — 验证下载压缩包的 SHA256 哈希值。</description></item>
    ///   <item><description><see cref="CompressMiddleware"/> — 将压缩包解压到目标目录。</description></item>
    ///   <item><description><see cref="PatchMiddleware"/> — 应用二进制差异补丁（如果启用了补丁功能）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 使用 <see cref="UseMiddleware{TMiddleware}"/> 无条件注册中间件，
    /// 或使用 <see cref="UseMiddlewareIf{TMiddleware}(bool?)"/> 根据条件决定是否注册。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var context = new PipelineContext();
    /// // 设置上下文数据...
    /// var builder = new PipelineBuilder(context)
    ///     .UseMiddleware&lt;HashMiddleware&gt;()
    ///     .UseMiddlewareIf&lt;CompressMiddleware&gt;(true)
    ///     .UseMiddlewareIf&lt;PatchMiddleware&gt;(isPatchEnabled);
    /// await builder.Build();
    /// </code>
    /// </example>
    public sealed class PipelineBuilder(PipelineContext context)
    {
        /// <summary>
        /// 中间件不可变队列，FIFO（先进先出）顺序。
        /// </summary>
        /// <remarks>
        /// 使用 <see cref="ImmutableQueue{T}"/> 保证注册后队列内容不被意外修改。
        /// 每次 <see cref="Enqueue"/> 操作都返回一个新的队列实例，原始实例保持不变。
        /// </remarks>
        private ImmutableQueue<IMiddleware> _middlewareQueue = ImmutableQueue<IMiddleware>.Empty;

        /// <summary>
        /// 向管道注册一个中间件。每次调用都会将中间件实例排队到队列尾部。
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// 要注册的中间件类型。必须实现 <see cref="IMiddleware"/> 接口且具有无参数构造函数。
        /// </typeparam>
        /// <returns>当前 <see cref="PipelineBuilder"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 此方法始终注册中间件，无论任何条件。如果需要条件注册，请使用
        /// <see cref="UseMiddlewareIf{TMiddleware}(bool?)"/>。
        /// 中间件实例通过 <c>new TMiddleware()</c> 创建，因此类型必须具有公开的无参数构造函数。
        /// </remarks>
        public PipelineBuilder UseMiddleware<TMiddleware>() where TMiddleware : IMiddleware, new()
        {
            var middleware = new TMiddleware();
            _middlewareQueue = _middlewareQueue.Enqueue(middleware);
            return this;
        }

        /// <summary>
        /// 根据条件向管道注册中间件。仅当条件为 <c>true</c> 时才注册。
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// 要注册的中间件类型。必须实现 <see cref="IMiddleware"/> 接口且具有无参数构造函数。
        /// </typeparam>
        /// <param name="condition">
        /// 注册条件。如果为 <c>null</c> 或 <c>false</c>，则跳过注册；
        /// 如果为 <c>true</c>，则创建并注册中间件实例。
        /// </param>
        /// <returns>当前 <see cref="PipelineBuilder"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 此方法适用于需要根据运行时配置或功能开关决定是否启用某个处理阶段的场景。
        /// 例如，仅在启用差异补丁时才注册 <see cref="PatchMiddleware"/>：
        /// <code>
        /// builder.UseMiddlewareIf&lt;PatchMiddleware&gt;(isPatchEnabled);
        /// </code>
        /// </remarks>
        public PipelineBuilder UseMiddlewareIf<TMiddleware>(bool? condition)
            where TMiddleware : IMiddleware, new()
        {
            if (condition is null or false)
                return this;

            var middleware = new TMiddleware();
            _middlewareQueue = _middlewareQueue.Enqueue(middleware);
            return this;
        }

        /// <summary>
        /// 按 FIFO 顺序异步执行所有已注册的中间件。
        /// </summary>
        /// <returns>表示异步操作的任务。当所有中间件执行完毕时，该任务完成。</returns>
        /// <remarks>
        /// <para>
        /// 此方法遍历 <see cref="_middlewareQueue"/>，对每个中间件依次调用
        /// <see cref="IMiddleware.InvokeAsync(PipelineContext)"/>。
        /// 中间件按注册顺序（先进先出）串行执行——前一个中间件完成后才会执行下一个。
        /// </para>
        /// <para>
        /// 如果任何中间件抛出异常，管道将立即终止，后续中间件不会执行。
        /// 调用者应捕获并处理异常以执行适当的错误恢复逻辑。
        /// </para>
        /// </remarks>
        public async Task Build()
        {
            foreach (var middleware in _middlewareQueue)
            {
                await middleware.InvokeAsync(context);
            }
        }
    }
}
