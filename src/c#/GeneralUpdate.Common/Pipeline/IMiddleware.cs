using System.Threading.Tasks;

namespace GeneralUpdate.Common.Pipeline
{
    /// <summary>
    /// Pipeline middleware.
    /// </summary>
    public interface IMiddleware
    {
        Task InvokeAsync(IContext context, IMiddleware middleware);
    }
}