using System;
using System.Threading.Tasks;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Differential patch middleware that uses <see cref="DiffPipeline"/> to apply binary differential patches
/// to application files in parallel.
/// </summary>
/// <remarks>
/// <para>
/// This middleware reads the following keys from <see cref="PipelineContext"/>:
/// <list type="bullet">
///   <item><description><c>"SourcePath"</c> — The application installation target path (directory containing old version files).</description></item>
///   <item><description><c>"PatchPath"</c> — The storage path for differential patch files (contains decompressed patch data).</description></item>
///   <item><description><c>"DiffPipeline"</c> — The <see cref="DiffPipeline"/> instance, built and injected by <see cref="GeneralUpdateBootstrap"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item><description>Retrieves the source path, patch path, and <see cref="DiffPipeline"/> instance from the context.</description></item>
///   <item><description>Calls <see cref="DiffPipeline.DirtyAsync(string, string, IProgress{DiffProgress}, System.Threading.CancellationToken)"/>
///         to apply patches to all old version files in parallel.</description></item>
///   <item><description>Progress during processing is reported through the progress reporting mechanism inside <see cref="DiffPipeline"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// This middleware should be registered after <see cref="HashMiddleware"/> and <see cref="CompressMiddleware"/>,
/// to ensure the archive has been verified for integrity and correctly extracted to the <c>"PatchPath"</c> directory.
/// If differential patching is not enabled (<c>"PatchEnabled"</c> is <c>false</c>), this middleware should not be
/// registered in the pipeline.
/// </para>
/// </remarks>
public class PatchMiddleware : IMiddleware
{
    /// <summary>
    /// Asynchronously executes the differential patch application logic.
    /// </summary>
    /// <param name="context">The pipeline context containing the source path, patch path, and <see cref="DiffPipeline"/> instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <see cref="PipelineContext"/> does not contain the <c>"DiffPipeline"</c> key.
    /// Callers must ensure that <see cref="GeneralUpdateBootstrap"/> has built and injected the <see cref="DiffPipeline"/> instance.
    /// </exception>
    /// <exception cref="Exception">Other exceptions that may occur during differential patch application.</exception>
    /// <remarks>
    /// <para>
    /// This method is the final processing stage of the update pipeline. It applies the patch files
    /// previously extracted to <c>"PatchPath"</c> onto the old version files in <c>"SourcePath"</c> in parallel,
    /// producing the updated files. Patch application uses
    /// <see cref="DiffPipeline.DirtyAsync(string, string, IProgress{DiffProgress}, System.Threading.CancellationToken)"/>,
    /// which internally controls concurrency via a semaphore and copies unknown new files after all files are processed.
    /// </para>
    /// <para>
    /// If <see cref="DiffPipeline"/> is not configured in the context, this middleware throws an
    /// <see cref="InvalidOperationException"/> with a clear error message guiding the user to check the
    /// bootstrapper configuration.
    /// </para>
    /// </remarks>
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
