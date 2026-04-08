using System;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Differential;

namespace GeneralUpdate.ClientCore.Pipeline;

public class PatchMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        var sourcePath = context.Get<string>("SourcePath");
        var targetPath = context.Get<string>("PatchPath");
        GeneralTracer.Info($"ClientCore.PatchMiddleware.InvokeAsync: applying differential patch. SourcePath={sourcePath}, PatchPath={targetPath}");
        try
        {
            await DifferentialCore.Dirty(sourcePath, targetPath);
            GeneralTracer.Info("ClientCore.PatchMiddleware.InvokeAsync: differential patch applied successfully.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("ClientCore.PatchMiddleware.InvokeAsync: failed to apply differential patch.", ex);
            throw;
        }
    }
}