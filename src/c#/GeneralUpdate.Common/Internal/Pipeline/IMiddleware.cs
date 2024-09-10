using System.Threading.Tasks;

namespace GeneralUpdate.Common.Internal.Pipeline
{
    /// <summary>
    /// Pipeline middleware.
    /// </summary>
    public interface IMiddleware
    {
        Task InvokeAsync(PipelineContext context, IMiddleware middleware);
    }
}