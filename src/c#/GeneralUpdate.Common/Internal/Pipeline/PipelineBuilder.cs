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
            var middleware = _middlewareStack.Peek();
            await middleware.InvokeAsync(context, _middlewareStack.Peek());
        }
    }
}