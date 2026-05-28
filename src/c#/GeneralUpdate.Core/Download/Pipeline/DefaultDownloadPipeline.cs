using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;

namespace GeneralUpdate.Core.Download.Pipeline;

/// <summary>
/// Default post-download processing pipeline that performs SHA256 hash verification
/// on downloaded files to ensure their integrity matches the expected hash value
/// provided by the server.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDownloadPipeline"/> and provides integrity verification
/// after a file has been downloaded.
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item>Checks whether an expected hash value has been configured. If not, skips verification and returns the file path directly.</item>
///   <item>Computes the SHA256 hash of the downloaded file.</item>
///   <item>Compares the computed hash against the expected hash using a case-insensitive comparison.</item>
///   <item>If the hashes match, returns the original file path.</item>
///   <item>If the hashes do not match, throws <see cref="InvalidDataException"/> with details of the mismatch.</item>
/// </list>
/// </para>
/// <para>
/// The SHA256 computation is offloaded to a background thread via <c>Task.Run</c>
/// to avoid blocking the calling thread. Cancellation is supported through <c>CancellationToken</c>.
/// </para>
/// </remarks>
public class DefaultDownloadPipeline : IDownloadPipeline
{
    private readonly string? _expectedHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDownloadPipeline"/> class
    /// with the expected SHA256 hash value.
    /// </summary>
    /// <param name="expectedHash">
    /// The expected SHA256 hash value as a hexadecimal string (case-insensitive).
    /// If null or empty, hash verification is skipped.
    /// </param>
    public DefaultDownloadPipeline(string? expectedHash = null)
        => _expectedHash = expectedHash;

    /// <summary>
    /// Processes the downloaded file by performing SHA256 hash verification
    /// against the expected hash value.
    /// </summary>
    /// <param name="downloadedPath">The full path to the downloaded file.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the hash computation.</param>
    /// <returns>The original file path if hash verification passes or is skipped.</returns>
    /// <exception cref="InvalidDataException">Thrown when the computed SHA256 hash does not match the expected value.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the <paramref name="token"/>.</exception>
    /// <remarks>
    /// <para>
    /// Processing flow:
    /// </para>
    /// <list type="number">
    ///   <item>If <c>_expectedHash</c> is null or empty, the file path is returned immediately without verification.</item>
    ///   <item>Otherwise, the SHA256 hash of the file is computed asynchronously via <see cref="ComputeSha256Async"/>.</item>
    ///   <item>The computed hash is compared with the expected hash using case-insensitive ordinal comparison.</item>
    ///   <item>If the hashes match, the file path is returned. Otherwise, an <see cref="InvalidDataException"/> is thrown.</item>
    /// </list>
    /// </remarks>
    public async Task<string> ProcessAsync(string downloadedPath, CancellationToken token = default)
    {
        if (!string.IsNullOrEmpty(_expectedHash))
        {
            var actual = await ComputeSha256Async(downloadedPath, token).ConfigureAwait(false);
            if (!string.Equals(actual, _expectedHash, System.StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"SHA256 mismatch for {downloadedPath}: expected {_expectedHash}, got {actual}");
        }
        return downloadedPath;
    }

    /// <summary>
    /// Computes the SHA256 hash of the specified file.
    /// </summary>
    /// <param name="path">The file path for which to compute the hash.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A lowercase hexadecimal SHA256 hash string without separators (e.g., "a1b2c3d4...").</returns>
    /// <remarks>
    /// <para>
    /// The hash computation is performed on a background thread via <c>Task.Run</c>
    /// to avoid blocking the calling thread.
    /// </para>
    /// <para>
    /// The returned hash string is lowercase with no hyphens or separators.
    /// </para>
    /// </remarks>
    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        using var sha = SHA256.Create();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await Task.Run(() => sha.ComputeHash(fs), token).ConfigureAwait(false);
        return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
