using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline
{
    /// <summary>
    /// Pipeline builder — middleware execute in FIFO (registration) order.
    /// </summary>
    public sealed class PipelineBuilder(PipelineContext context)
    {
        /// <summary>
        /// LIFO£¬Last In First Out.
        /// </summary>
        private ImmutableQueue<IMiddleware> _middlewareQueue = ImmutableQueue<IMiddleware>.Empty;

        public PipelineBuilder UseMiddleware<TMiddleware>() where TMiddleware : IMiddleware, new()
        {
            var middleware = new TMiddleware();
            _middlewareQueue = _middlewareQueue.Enqueue(middleware);
            return this;
        }

        public PipelineBuilder UseMiddlewareIf<TMiddleware>(bool? condition)
            where TMiddleware : IMiddleware, new()
        {
            if (condition is null or false) 
                return this;
            
            var middleware = new TMiddleware();
            _middlewareQueue = _middlewareQueue.Enqueue(middleware);
            return this;
        }

        public async Task Build()
        {
            foreach (var middleware in _middlewareQueue)
            {
                await middleware.InvokeAsync(context);
            }
        }
    }
}