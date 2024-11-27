using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace GeneralUpdate.Common.Internal.Pipeline
{
    /// <summary>
    /// Pipeline builder.
    /// </summary>
    public sealed class PipelineBuilder(PipelineContext context)
    {
        /// <summary>
        /// LIFO，Last In First Out.
        /// </summary>
        private ImmutableStack<IMiddleware> _middlewareStack = ImmutableStack<IMiddleware>.Empty;

        public PipelineBuilder UseMiddleware<TMiddleware>() where TMiddleware : IMiddleware, new()
        {
            var middleware = new TMiddleware();
            _middlewareStack = _middlewareStack.Push(middleware);
            return this;
        }

        public PipelineBuilder UseMiddlewareIf<TMiddleware>(bool? condition)
            where TMiddleware : IMiddleware, new()
        {
            if (condition is null or false) 
                return this;
            
            var middleware = new TMiddleware();
            _middlewareStack = _middlewareStack.Push(middleware);
            return this;
        }

        public async Task Build()
        {
            foreach (var middleware in _middlewareStack)
            {
                await middleware.InvokeAsync(context);
            }
        }
    }
}