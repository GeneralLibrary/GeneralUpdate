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
/// The <see cref="IBinaryDiffer"/> implementation is injected via
/// <c>Bootstrap.BinaryDiffer&lt;T&gt;()</c>. Without injection, patches are skipped.
/// </summary>
public class PatchMiddleware : IMiddleware
{
    private readonly IBinaryDiffer? _differ;

    /// <summary>Parameterless constructor (required by PipelineBuilder). Uses no differ.</summary>
    public PatchMiddleware() { }

    /// <summary>Creates a PatchMiddleware with an optional differ.</summary>
    /// <param name="differ">Binary differ implementation. If null, patches are skipped.</param>
    public PatchMiddleware(IBinaryDiffer? differ)
    {
        _differ = differ;
    }

    public async Task InvokeAsync(PipelineContext context)
    {
        var sourcePath = context.Get<string>("SourcePath");
        var targetPath = context.Get<string>("PatchPath");

        if (_differ == null)
        {
            GeneralTracer.Info("PatchMiddleware.InvokeAsync: no IBinaryDiffer injected — patch skipped. " +
                "Use Bootstrap.BinaryDiffer<T>() to enable differential patching.");
            return;
        }

        GeneralTracer.Info($"PatchMiddleware.InvokeAsync: applying differential patch. SourcePath={sourcePath}, PatchPath={targetPath}");
        try
        {
            await _differ.DirtyAsync(sourcePath, targetPath, targetPath);
            GeneralTracer.Info("PatchMiddleware.InvokeAsync: differential patch applied successfully.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("PatchMiddleware.InvokeAsync: failed to apply differential patch.", ex);
            throw;
        }
    }
}
