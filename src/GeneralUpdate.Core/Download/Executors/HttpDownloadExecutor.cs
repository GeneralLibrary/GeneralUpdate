using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Network;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Executors;

/// <summary>
/// HTTP-based file download executor supporting resumable downloads via the Range request header
/// and chunked streaming downloads.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDownloadExecutor"/> and provides the core functionality
/// for downloading files from HTTP endpoints.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
///   <item><term>Resumable downloads</term><description>Uses the HTTP Range header to continue
///         downloads from where they were interrupted. Can be enabled or disabled via the
///         <c>enableResume</c> parameter. Automatically falls back to a full download when the
///         server does not support partial content responses.</description></item>
///   <item><term>Chunked streaming download</term><description>Reads and writes data streams in
///         8 KB chunks, avoiding loading the entire file into memory.</description></item>
///   <item><term>Progress reporting</term><description>Reports download progress via
///         <c>IProgress&lt;DownloadProgress&gt;</c> every 250 milliseconds, including bytes
///         downloaded, total bytes, and percentage.</description></item>
///   <item><term>Timeout control</term><description>Supports configurable per-request timeouts
///         via the <c>timeout</c> parameter.</description></item>
///   <item><term>Authentication support</term><description>Reads authentication scheme and token
///         from <c>DownloadAsset</c> to provide HTTP Bearer or custom authentication for
///         authorized download sources.</description></item>
///   <item><term>Cancellation support</term><description>Supports cancelling in-progress downloads
///         via <c>CancellationToken</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// This executor is used by higher-level components such as <c>DefaultDownloadOrchestrator</c>
/// and <c>OssStrategy</c> as the actual HTTP download engine.
/// </para>
/// </remarks>
public class HttpDownloadExecutor : IDownloadExecutor
{
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly bool _enableResume;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpDownloadExecutor"/> class
    /// with the specified HTTP client, timeout, and resume options.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance used to send HTTP requests. Must not be null.</param>
    /// <param name="timeout">The timeout duration for each HTTP request. Defaults to 30 seconds.</param>
    /// <param name="enableResume">Whether to enable resumable download support. Defaults to true.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
    public HttpDownloadExecutor(HttpClient client, TimeSpan? timeout = null, bool enableResume = true)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _enableResume = enableResume;
    }

    /// <summary>
    /// Asynchronously executes the download of a single asset file.
    /// </summary>
    /// <param name="asset">The asset information to download, including URL, file name, size, and hash.</param>
    /// <param name="destPath">The destination local file path for the download.</param>
    /// <param name="progress">An optional progress reporter for reporting download progress.</param>
    /// <param name="token">An optional cancellation token to cancel the download operation.</param>
    /// <returns>A <see cref="DownloadResult"/> containing the download outcome (success/failure, bytes downloaded, duration, etc.).</returns>
    /// <remarks>
    /// <para>
    /// Download flow:
    /// </para>
    /// <list type="number">
    ///   <item>Checks whether a partially downloaded file already exists at the destination (for resume support).</item>
    ///   <item>Creates an HTTP GET request, adding a Range header if resume is enabled and a partial file exists.</item>
    ///   <item>If the asset provides an authentication scheme and token, adds an Authorization header.</item>
    ///   <item>Sends the request and reads the response stream.</item>
    ///   <item>If the server returns a non-206 PartialContent status code, starts the download from the beginning.</item>
    ///   <item>Reads and writes the file in chunks using <see cref="StreamDownloadAsync"/> with an 8 KB buffer.</item>
    ///   <item>Reports 100% progress after the download completes.</item>
    /// </list>
    /// <para>
    /// If an exception occurs during download (excluding <c>OperationCanceledException</c>),
    /// a <c>DownloadResult</c> containing the error information is returned instead of throwing.
    /// </para>
    /// </remarks>
    public async Task<DownloadResult> ExecuteAsync(
        DownloadAsset asset, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        int retries = 0;
        long existingBytes = 0;

        // Check for existing partial file (resume support; skip when disabled)
        if (_enableResume && File.Exists(destPath))
        {
            existingBytes = new FileInfo(destPath).Length;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.Url);
            if (_enableResume && existingBytes > 0)
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);

            // Apply per-asset auth if provided by server (e.g. GeneralSpacestation signed URLs or Bearer tokens)
            if (!string.IsNullOrEmpty(asset.AuthScheme) && !string.IsNullOrEmpty(asset.AuthToken))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(asset.AuthScheme, asset.AuthToken);
            }

            // Apply global auth provider and extra headers (e.g. X-Tenant-Id) from HttpClientProvider.
            // This ensures download requests carry the same auth context as version validation
            // and status reporting requests.
            await HttpClientProvider.ApplyAuthAsync(request, token).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(_timeout);

            using var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            if (_enableResume && existingBytes > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                existingBytes = 0;
                File.Delete(destPath);
            }

            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1;

            var mode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
            using var fs = new FileStream(destPath, mode, FileAccess.Write, FileShare.None);
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            var (downloaded, elapsed) = await StreamDownloadAsync(stream, fs, totalBytes, existingBytes,
                destPath, progress, sw, token).ConfigureAwait(false);

            progress?.Report(new DownloadProgress(
                Path.GetFileName(destPath), downloaded,
                totalBytes > 0 ? totalBytes + existingBytes : null,
                100, DownloadStatus.Completed));

            return new DownloadResult(asset, destPath, downloaded, elapsed, retries, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new DownloadResult(asset, destPath, existingBytes, sw.Elapsed, retries, false, ex.Message);
        }
    }

    /// <summary>
    /// Shared download loop: reads data from a source stream, writes to a destination stream,
    /// and reports download progress. This method is reused by both the HTTP and Oss executors
    /// to avoid duplicating the buffering/reading/writing/progress-reporting logic.
    /// </summary>
    /// <param name="source">The source data stream (typically from an HTTP response stream).</param>
    /// <param name="dest">The destination file stream.</param>
    /// <param name="totalBytes">The total content size reported by the server (may be -1 if unknown).</param>
    /// <param name="existingBytes">The number of already-downloaded bytes (for resume support).</param>
    /// <param name="destPath">The destination file path (used only for file name display in progress reports).</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <param name="sw">A stopwatch for measuring download duration.</param>
    /// <param name="token">A cancellation token.</param>
    /// <returns>A tuple containing the actual bytes downloaded and the elapsed duration.</returns>
    /// <remarks>
    /// <para>
    /// The download loop uses an 8192-byte (8 KB) buffer for chunked reading to avoid
    /// excessive memory consumption.
    /// </para>
    /// <para>
    /// Progress is reported every 250 milliseconds, or immediately upon download completion.
    /// Progress information includes the file name, bytes downloaded, total bytes (if known),
    /// and completion percentage.
    /// </para>
    /// </remarks>
    internal static async Task<(long Downloaded, TimeSpan Elapsed)> StreamDownloadAsync(
        Stream source, Stream dest, long totalBytes, long existingBytes,
        string destPath, IProgress<DownloadProgress>? progress, Stopwatch sw, CancellationToken token)
    {
        var buffer = new byte[8192];
        long downloaded = existingBytes;
        long lastReport = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
        {
            await dest.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
            downloaded += read;

            var now = sw.ElapsedMilliseconds;
            if (now - lastReport >= 250 || downloaded == totalBytes + existingBytes)
            {
                lastReport = now;
                var pct = totalBytes > 0 ? (double)downloaded / (totalBytes + existingBytes) * 100 : -1;
                progress?.Report(new DownloadProgress(
                    Path.GetFileName(destPath), downloaded,
                    totalBytes > 0 ? totalBytes + existingBytes : null,
                    pct, DownloadStatus.Downloading));
            }
        }

        sw.Stop();
        return (downloaded, sw.Elapsed);
    }
}
