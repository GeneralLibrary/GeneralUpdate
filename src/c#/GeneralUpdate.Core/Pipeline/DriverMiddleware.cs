using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Pipeline;

namespace GeneralUpdate.Core.Pipeline;

public class DriverMiddleware : IMiddleware
{
    public Task InvokeAsync(PipelineContext context)
    {
        throw new System.NotImplementedException();
    }
}