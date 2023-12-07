using GeneralUpdate.Core.Pipelines.Middleware;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Pipeline
{
    public interface IPipelineBuilder
    {
        /// <summary>
        /// reference middleware.
        /// </summary>
        /// <param name="middleware"></param>
        /// <returns></returns>
        IPipelineBuilder Use(IMiddleware middleware);

        /// <summary>
        /// start the pipeline.
        /// </summary>
        /// <returns></returns>
        Task<IPipelineBuilder> Build();
    }
}