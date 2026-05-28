using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Compress;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Decompression middleware responsible for extracting the downloaded archive to the target directory.
/// </summary>
/// <remarks>
/// <para>
/// This middleware reads the following keys from <see cref="PipelineContext"/>:
/// <list type="bullet">
///   <item><description><c>"Format"</c> — <see cref="Configuration.Format"/> The archive format (e.g., ZIP, GZip).</description></item>
///   <item><description><c>"ZipFilePath"</c> — The source archive file path.</description></item>
///   <item><description><c>"PatchPath"</c> — The differential patch temporary directory path.</description></item>
///   <item><description><c>"Encoding"</c> — <see cref="Encoding"/> The character encoding used for decompression.</description></item>
///   <item><description><c>"SourcePath"</c> — The application installation target path.</description></item>
///   <item><description><c>"PatchEnabled"</c> — Whether the differential patch mode is enabled.</description></item>
/// </list>
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item><description>Reads configuration parameters from the context.</description></item>
///   <item><description>Determines the decompression target path based on the value of <c>"PatchEnabled"</c>:
///          If patching is enabled, decompress to <c>"PatchPath"</c>; otherwise, decompress directly to <c>"SourcePath"</c>.</description></item>
///   <item><description>Calls <see cref="CompressProvider.Decompress"/> to perform the actual decompression operation.</description></item>
/// </list>
/// </para>
/// <para>
/// This middleware should be registered after <see cref="HashMiddleware"/> (to ensure the archive integrity
/// has been verified) and before <see cref="PatchMiddleware"/> (if differential patching is needed).
/// </para>
/// </remarks>
public class CompressMiddleware : IMiddleware
{
    /// <summary>
    /// Asynchronously executes the decompression operation.
    /// </summary>
    /// <param name="context">The pipeline context containing the archive path, format, encoding, and target path configuration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="System.Exception">Any exception that occurs during decompression. Exception details are logged via <see cref="GeneralTracer"/>.</exception>
    /// <remarks>
    /// <para>
    /// The decompression operation is performed on a background thread (via <see cref="Task.Run"/>) to avoid blocking
    /// the calling thread. If <c>"PatchEnabled"</c> is <c>true</c>, the archive is extracted to the <c>"PatchPath"</c> directory,
    /// and <see cref="PatchMiddleware"/> will subsequently apply the patches to <c>"SourcePath"</c>.
    /// If <c>false</c>, the archive is extracted directly to <c>"SourcePath"</c>, completing the update.
    /// </para>
    /// <para>
    /// Note: This method does not handle exceptions directly; exceptions propagate upward for
    /// <see cref="PipelineBuilder.Build"/> to halt pipeline execution.
    /// </para>
    /// </remarks>
    public Task InvokeAsync(PipelineContext context)
    {
        return Task.Run(() =>
        {
            var format = context.Get<Configuration.Format>("Format");
            var sourcePath = context.Get<string>("ZipFilePath");
            var patchPath = context.Get<string>("PatchPath");
            var encoding = context.Get<Encoding>("Encoding");
            var appPath = context.Get<string>("SourcePath");
            var patchEnabled = context.Get<bool?>("PatchEnabled");
            var targetPath = patchEnabled == false ? appPath : patchPath;
            GeneralTracer.Info($"CompressMiddleware.InvokeAsync: decompressing package. Format={format}, Source={sourcePath}, Target={targetPath}, PatchEnabled={patchEnabled}");
            try
            {
                CompressProvider.Decompress(format, sourcePath, targetPath, encoding);
                GeneralTracer.Info("CompressMiddleware.InvokeAsync: decompression completed successfully.");
            }
            catch (System.Exception ex)
            {
                GeneralTracer.Error("CompressMiddleware.InvokeAsync: decompression failed.", ex);
                throw;
            }
        });
    }
}
