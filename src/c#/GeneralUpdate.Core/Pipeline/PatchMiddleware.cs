using System;
using System.Threading.Tasks;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Differential patch middleware. Uses <see cref="DiffPipeline"/> to apply binary
/// patches in parallel with progress reporting. The <see cref="DiffPipeline"/> is
/// built by <see cref="GeneralUpdateBootstrap"/> and injected via the pipeline context.
/// </summary>
public class PatchMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        var sourcePath = context.Get<string>("SourcePath");
        var targetPath = context.Get<string>("PatchPath");

        var diffPipeline = context.Get<DiffPipeline>("DiffPipeline")
            ?? throw new InvalidOperationException(
                "DiffPipeline not found in PipelineContext. " +
                "Ensure GeneralUpdateBootstrap builds and injects the DiffPipeline.");

        GeneralTracer.Info($"PatchMiddleware.InvokeAsync: applying differential patch. SourcePath={sourcePath}, PatchPath={targetPath}");
        try
        {
            await diffPipeline.DirtyAsync(sourcePath, targetPath);
            GeneralTracer.Info("PatchMiddleware.InvokeAsync: differential patch applied successfully.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("PatchMiddleware.InvokeAsync: failed to apply differential patch.", ex);
            throw;
        }
    }
}
