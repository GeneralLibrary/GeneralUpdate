using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Pipelines.Middleware;
using GeneralUpdate.Core.Pipelines.Pipeline;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines
{
    public class PipelineBuilder<TContext> : IPipelineBuilder where TContext : BaseContext
    {
        private IList<MiddlewareNode> nodes = new List<MiddlewareNode>();
        private MiddlewareStack _components;
        private readonly TContext _context;

        public PipelineBuilder(TContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _context = context;
        }

        /// <summary>
        /// Add middleware to the stack.
        /// </summary>
        /// <param name="middleware"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public IPipelineBuilder Use(IMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            nodes.Add(new MiddlewareNode(middleware.InvokeAsync));
            return this;
        }

        /// <summary>
        /// Start the pipeline and execute the middleware sequentially.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<IPipelineBuilder> Build()
        {
            if (nodes == null || nodes.Count == 0) throw new ArgumentNullException(nameof(nodes));
            _components = new MiddlewareStack(nodes);
            await _components.Pop().Next.Invoke(_context, _components);
            return this;
        }
    }
}