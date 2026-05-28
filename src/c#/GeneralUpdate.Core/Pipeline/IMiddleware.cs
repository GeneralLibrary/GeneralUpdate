using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline
{
    /// <summary>
    /// Defines the contract interface for pipeline middleware.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All pipeline middleware must implement this interface. The <see cref="InvokeAsync(PipelineContext)"/> method is
    /// called sequentially in registration order (FIFO) during execution of the pipeline built by
    /// <see cref="PipelineBuilder"/>. Each middleware is responsible for an independent processing stage,
    /// such as hash verification, decompression, or applying differential patches.
    /// </para>
    /// <para>
    /// Middleware share data through <see cref="PipelineContext"/>. Upstream middleware write computed results
    /// into the context, and downstream middleware read the required inputs from it.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example demonstrates how to implement a custom middleware:
    /// <code>
    /// public class MyMiddleware : IMiddleware
    /// {
    ///     public async Task InvokeAsync(PipelineContext context)
    ///     {
    ///         // Read data from the context
    ///         var data = context.Get&lt;string&gt;("MyKey");
    ///         // Execute processing logic
    ///         await Task.Run(() => { /* ... */ });
    ///         // Write results back to the context
    ///         context.Add("Result", "processed");
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IMiddleware
    {
        /// <summary>
        /// Asynchronously executes the processing logic of the middleware.
        /// </summary>
        /// <param name="context">
        /// The pipeline context, which contains the state of the current execution environment and
        /// shared data between middleware. Implementations should read input parameters from this context
        /// and write processing results into it.
        /// </param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// This method is called sequentially by <see cref="PipelineBuilder.Build"/> when traversing the
        /// middleware queue. Implementations should avoid long blocking operations and always use asynchronous
        /// patterns (<c>await</c>). If processing fails, an exception should be thrown to halt pipeline execution.
        /// </remarks>
        Task InvokeAsync(PipelineContext context);
    }
}
