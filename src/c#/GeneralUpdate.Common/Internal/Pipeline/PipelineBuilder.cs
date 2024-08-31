using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace GeneralUpdate.Common.Internal.Pipeline
{
    /// <summary>
    /// Pipeline builder.
    /// </summary>
    public sealed class PipelineBuilder(IContext context = null)
    {
        private ImmutableStack<IMiddleware> _middlewareStack = ImmutableStack<IMiddleware>.Empty;

        public PipelineBuilder Use<TMiddleware>(TMiddleware middleware) where TMiddleware : IMiddleware, new()
        {
            _middlewareStack = _middlewareStack.Push(middleware);
            return this;
        }

        public PipelineBuilder UseIf<TMiddleware>(TMiddleware middleware, Func<bool> condition)
            where TMiddleware : IMiddleware, new()
        {
            if (!condition()) return this;
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