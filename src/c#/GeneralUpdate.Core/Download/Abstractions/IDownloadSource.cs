using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>
/// Defines a contract for retrieving the list of downloadable assets from a remote source.
/// </summary>
/// <remarks>
/// <para>
/// Implementations encapsulate the logic of connecting to a remote service—such as an HTTP
/// version-validation API, an OSS bucket, or a SignalR hub—and returning a structured list
/// of <see cref="DownloadAsset"/> objects for the orchestrator to process.
/// </para>
/// <para>
/// Built-in implementations:
/// <list type="bullet">
///   <item><description><c>HttpDownloadSource</c> — calls a version-validation API for Client and Upgrade app types.</description></item>
///   <item><description><c>OssDownloadSource</c> — downloads and parses a version JSON file from an object storage URL.</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IDownloadSource
{
    /// <summary>
    /// Asynchronously retrieves the list of downloadable assets.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="DownloadSourceResult"/> containing the list of <see cref="DownloadAsset"/> objects
    /// and flags indicating whether main or upgrade updates are available.</returns>
    Task<DownloadSourceResult> ListAsync(CancellationToken token = default);
}

/// <summary>
/// Defines a post-download processing pipeline that transforms or verifies a downloaded file.
/// </summary>
/// <remarks>
/// <para>
/// Implementations perform one or more post-processing steps after a file has been downloaded,
/// such as:
/// </para>
/// <list type="bullet">
///   <item><description>SHA256 hash verification against an expected value.</description></item>
///   <item><description>Archive decompression (zip, tar, gz).</description></item>
///   <item><description>Decryption of an encrypted package.</description></item>
/// </list>
/// <para>
/// The default implementation is <c>DefaultDownloadPipeline</c>, which performs SHA256 hash verification.
/// </para>
/// </remarks>
public interface IDownloadPipeline
{
    /// <summary>
    /// Processes the downloaded file at the specified path, performing verification or transformation.
    /// </summary>
    /// <param name="downloadedPath">The full path to the downloaded file.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>The file path after processing (may differ if the file was extracted or transformed).</returns>
    /// <exception cref="InvalidDataException">Thrown when verification fails (e.g., hash mismatch).</exception>
    Task<string> ProcessAsync(string downloadedPath, CancellationToken token = default);
}
