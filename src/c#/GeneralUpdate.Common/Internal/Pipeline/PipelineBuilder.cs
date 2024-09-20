using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace GeneralUpdate.Common.Internal.Pipeline
{
    /// <summary>
    /// Pipeline builder.
    /// </summary>
    public sealed class PipelineBuilder(PipelineContext context = null)
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

        public PipelineBuilder UseMiddlewareIf<TMiddleware>(Func<bool> condition)
            where TMiddleware : IMiddleware, new()
        {
            if (!condition()) return this;
            var middleware = new TMiddleware();
            _middlewareStack = _middlewareStack.Push(middleware);
            return this;
        }

        public async Task Build()
        {
            while (!_middlewareStack.IsEmpty)
            {
                _middlewareStack.Pop(out var middleware);
                await middleware.InvokeAsync(context);
            }
        }
    }
}