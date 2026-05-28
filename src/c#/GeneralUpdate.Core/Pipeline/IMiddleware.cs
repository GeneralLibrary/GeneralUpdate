using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline
{
    /// <summary>
    /// 定义管道中间件的契约接口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 所有管道中间件必须实现此接口。<see cref="InvokeAsync(PipelineContext)"/> 方法在管道构建
    /// （<see cref="PipelineBuilder"/>）的 <see cref="PipelineBuilder.Build"/> 执行期间按注册顺序
    /// （FIFO）依次调用。每个中间件负责一个独立的处理阶段，例如哈希验证、解压缩或应用差异补丁。
    /// </para>
    /// <para>
    /// 中间件之间通过 <see cref="PipelineContext"/> 共享数据。上游中间件将计算结果写入上下文，
    /// 下游中间件从中读取所需的输入。
    /// </para>
    /// </remarks>
    /// <example>
    /// 以下示例演示如何实现一个自定义中间件：
    /// <code>
    /// public class MyMiddleware : IMiddleware
    /// {
    ///     public async Task InvokeAsync(PipelineContext context)
    ///     {
    ///         // 从上下文中读取数据
    ///         var data = context.Get&lt;string&gt;("MyKey");
    ///         // 执行处理逻辑
    ///         await Task.Run(() => { /* ... */ });
    ///         // 将结果写回上下文
    ///         context.Add("Result", "processed");
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IMiddleware
    {
        /// <summary>
        /// 异步执行中间件的处理逻辑。
        /// </summary>
        /// <param name="context">
        /// 管道上下文，包含当前执行环境的状态和中间件之间共享的数据。
        /// 实现应从该上下文读取输入参数，并将处理结果写入其中。
        /// </param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法由 <see cref="PipelineBuilder.Build"/> 在遍历中间件队列时依次调用。
        /// 实现应避免长时间阻塞，始终使用异步模式（<c>await</c>）。
        /// 如果处理失败，应抛出异常以中断管道执行。
        /// </remarks>
        Task InvokeAsync(PipelineContext context);
    }
}
