using System.Threading.Tasks;

namespace GeneralUpdate.Common.Internal.Pipeline
{
    /// <summary>
    /// Pipeline middleware.
    /// </summary>
    public interface IMiddleware
    {
        Task InvokeAsync(IContext context, IMiddleware middleware);
    }
}