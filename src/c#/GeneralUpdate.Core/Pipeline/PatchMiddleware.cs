using System;
using System.Threading.Tasks;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Differential patch middleware.
/// Full implementation requires GeneralUpdate.Differential (circular dependency — see T2).
/// Currently a no-op placeholder; patches are applied externally.
/// </summary>
public class PatchMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        GeneralTracer.Info("PatchMiddleware.InvokeAsync: differential patching is not available in this build. " +
            "IBinaryDiffer injection via Bootstrap.BinaryDiffer<T>() will be re-enabled in a future PR.");
        await Task.CompletedTask;
    }
}
