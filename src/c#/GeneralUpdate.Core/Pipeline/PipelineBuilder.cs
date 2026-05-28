using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline
{
    /// <summary>
    /// Pipeline builder that registers and executes middleware in FIFO (first-in, first-out) order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PipelineBuilder"/> is the core orchestration component of the GeneralUpdate pipeline pattern.
    /// It manages an immutable queue of <see cref="IMiddleware"/> instances. Middleware are enqueued in
    /// registration order, and during <see cref="Build"/> they execute sequentially in FIFO order asynchronously.
    /// </para>
    /// <para>
    /// A typical update pipeline flow is as follows:
    /// <list type="number">
    ///   <item><description><see cref="HashMiddleware"/> — Verifies the SHA256 hash of the downloaded archive.</description></item>
    ///   <item><description><see cref="CompressMiddleware"/> — Decompresses the archive to the target directory.</description></item>
    ///   <item><description><see cref="PatchMiddleware"/> — Applies binary differential patches (if patching is enabled).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Use <see cref="UseMiddleware{TMiddleware}"/> to unconditionally register middleware,
    /// or <see cref="UseMiddlewareIf{TMiddleware}(bool?)"/> to register conditionally.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var context = new PipelineContext();
    /// // Set context data...
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
        /// Immutable queue of middleware, maintained in FIFO (first-in, first-out) order.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="ImmutableQueue{T}"/> to guarantee that the queue contents cannot be accidentally
        /// modified after registration. Each <see cref="ImmutableQueue{T}.Enqueue(T)"/> operation returns a new
        /// queue instance, leaving the original instance unchanged.
        /// </remarks>
        private ImmutableQueue<IMiddleware> _middlewareQueue = ImmutableQueue<IMiddleware>.Empty;

        /// <summary>
        /// Registers a middleware type into the pipeline. Each call enqueues a middleware instance at the tail of the queue.
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// The type of middleware to register. Must implement the <see cref="IMiddleware"/> interface and have a parameterless constructor.
        /// </typeparam>
        /// <returns>The current <see cref="PipelineBuilder"/> instance, enabling chained calls.</returns>
        /// <remarks>
        /// This method always registers the middleware regardless of any condition. For conditional registration,
        /// use <see cref="UseMiddlewareIf{TMiddleware}(bool?)"/>.
        /// The middleware instance is created via <c>new TMiddleware()</c>, so the type must have a public parameterless constructor.
        /// </remarks>
        public PipelineBuilder UseMiddleware<TMiddleware>() where TMiddleware : IMiddleware, new()
        {
            var middleware = new TMiddleware();
            _middlewareQueue = _middlewareQueue.Enqueue(middleware);
            return this;
        }

        /// <summary>
        /// Conditionally registers a middleware type into the pipeline. The middleware is only registered when the condition is <c>true</c>.
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// The type of middleware to register. Must implement the <see cref="IMiddleware"/> interface and have a parameterless constructor.
        /// </typeparam>
        /// <param name="condition">
        /// The registration condition. If <c>null</c> or <c>false</c>, registration is skipped;
        /// if <c>true</c>, a middleware instance is created and registered.
        /// </param>
        /// <returns>The current <see cref="PipelineBuilder"/> instance, enabling chained calls.</returns>
        /// <remarks>
        /// This method is suitable for scenarios where certain processing stages should be enabled or disabled
        /// based on runtime configuration or feature flags. For example, register <see cref="PatchMiddleware"/>
        /// only when differential patching is enabled:
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
        /// Asynchronously executes all registered middleware in FIFO order.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task completes when all middleware have finished executing.</returns>
        /// <remarks>
        /// <para>
        /// This method traverses the <see cref="_middlewareQueue"/> and calls
        /// <see cref="IMiddleware.InvokeAsync(PipelineContext)"/> on each middleware in sequence.
        /// Middleware execute serially in registration order (first-in, first-out) — the next middleware
        /// starts only after the previous one has completed.
        /// </para>
        /// <para>
        /// If any middleware throws an exception, the pipeline terminates immediately and subsequent
        /// middleware will not execute. Callers should catch and handle exceptions to perform appropriate
        /// error recovery logic.
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
