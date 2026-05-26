using System;
using System.Threading.Tasks;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Differential patch middleware. Applies binary patches (BSDIFF, HDiffPatch, etc.)
/// to bring files from an old version to a new version.
///
/// The <see cref="IBinaryDiffer"/> implementation is resolved from
/// <see cref="PipelineContext"/> (key "BinaryDiffer"), set by
/// <see cref="GeneralUpdate.Core.Strategy.AbstractStrategy"/> when the differ is injected via
/// <c>Bootstrap.BinaryDiffer&lt;T&gt;()</c>. Without injection, patches are skipped.
/// </summary>
public class PatchMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        var sourcePath = context.Get<string>("SourcePath");
        var targetPath = context.Get<string>("PatchPath");

        // Resolve differ from pipeline context (injected via AbstractStrategy)
        var differ = context.Get<IBinaryDiffer>("BinaryDiffer");

        if (differ == null)
        {
            GeneralTracer.Info("PatchMiddleware.InvokeAsync: no IBinaryDiffer injected — patch skipped. " +
                "Use Bootstrap.BinaryDiffer<T>() to enable differential patching.");
            return;
        }

        GeneralTracer.Info($"PatchMiddleware.InvokeAsync: applying differential patch. SourcePath={sourcePath}, PatchPath={targetPath}");
        try
        {
            await differ.DirtyAsync(sourcePath, targetPath, targetPath);
            GeneralTracer.Info("PatchMiddleware.InvokeAsync: differential patch applied successfully.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("PatchMiddleware.InvokeAsync: failed to apply differential patch.", ex);
            throw;
        }
    }
}
