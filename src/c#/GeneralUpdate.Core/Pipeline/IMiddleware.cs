using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline
{
    /// <summary>
    /// Pipeline middleware.
    /// </summary>
    public interface IMiddleware
    {
        Task InvokeAsync(PipelineContext context);
    }
}