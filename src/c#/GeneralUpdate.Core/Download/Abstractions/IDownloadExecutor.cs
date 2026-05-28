using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>
/// Defines a contract for executing a single file download operation.
/// Implementations handle the actual data transfer from a remote source to a local file path.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must be thread-safe, as the same executor instance may be shared
/// across parallel download tasks within an <see cref="IDownloadOrchestrator"/>.
/// </para>
/// <para>
/// Built-in implementations include <c>HttpDownloadExecutor</c> (HTTP/HTTPS with resume support)
/// and <c>OssDownloadExecutor</c> (OSS signed URL downloads).
/// </para>
/// </remarks>
public interface IDownloadExecutor
{
    /// <summary>
    /// Asynchronously executes the download of a single asset to the specified destination path.
    /// </summary>
    /// <param name="asset">The <see cref="DownloadAsset"/> describing the resource to download (URL, name, hash, etc.).</param>
    /// <param name="destPath">The full local file path where the downloaded content will be written.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> receiver for download progress notifications.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the download operation.</param>
    /// <returns>A <see cref="DownloadResult"/> containing the outcome (success/failure, bytes downloaded, duration, etc.).</returns>
    Task<DownloadResult> ExecuteAsync(
        DownloadAsset asset, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default);
}

/// <summary>
/// Defines a retry, timeout, or circuit-breaker policy for download operations.
/// </summary>
/// <remarks>
/// <para>
/// Implementations wrap a download delegate and add cross-cutting concerns such as:
/// </para>
/// <list type="bullet">
///   <item><description>Retry with exponential backoff for transient failures.</description></item>
///   <item><description>Timeout enforcement per attempt.</description></item>
///   <item><description>Circuit-breaker logic to stop retrying after a threshold of failures.</description></item>
/// </list>
/// <para>
/// The default implementation is <c>DefaultRetryPolicy</c>, which retries on timeouts,
/// network I/O errors, and 5xx server errors using exponential backoff.
/// </para>
/// </remarks>
public interface IDownloadPolicy
{
    /// <summary>
    /// Executes the specified download action, applying the policy's retry/timeout logic.
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="action">The asynchronous operation to execute, accepting a <see cref="CancellationToken"/>.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the entire operation including retries.</param>
    /// <returns>The result of the action if it succeeds within the policy's constraints.</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="token"/>.</exception>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default);
}
