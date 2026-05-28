using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Executors;

/// <summary>
/// OSS (Object Storage Service) download executor that downloads files via HTTP GET
/// using pre-signed URLs obtained from an OSS provider.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDownloadExecutor"/> and is designed to work with
/// object storage services such as AliYun OSS, AWS S3, MinIO, and Tencent COS.
/// </para>
/// <para>
/// The executor delegates the actual data transfer to <see cref="HttpDownloadExecutor.StreamDownloadAsync"/>
/// which provides shared buffered reading, writing, and progress-reporting logic.
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item>Sends an HTTP GET request to the asset's pre-signed URL.</item>
///   <item>Reads the response stream and writes it to the destination file using an 8 KB buffer.</item>
///   <item>Reports download progress through the <see cref="IProgress{T}"/> callback.</item>
///   <item>Returns a <see cref="DownloadResult"/> with success/failure status, bytes downloaded, and duration.</item>
/// </list>
/// </para>
/// <para>
/// Note: Unlike <c>HttpDownloadExecutor</c>, this executor does not support resume/download-range headers
/// because OSS pre-signed URLs are typically short-lived and single-use.
/// </para>
/// </remarks>
public class OssDownloadExecutor : IDownloadExecutor
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="OssDownloadExecutor"/> class.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance used to send HTTP requests. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
    public OssDownloadExecutor(HttpClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>
    /// Asynchronously downloads a single asset from an OSS pre-signed URL to the specified local path.
    /// </summary>
    /// <param name="asset">The <see cref="DownloadAsset"/> describing the resource to download, including its signed URL.</param>
    /// <param name="destPath">The full local file path where the downloaded content will be written.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> receiver for download progress notifications.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the download operation.</param>
    /// <returns>
    /// A <see cref="DownloadResult"/> containing the outcome of the download, including
    /// success/failure status, bytes downloaded, and elapsed duration.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Download flow:
    /// </para>
    /// <list type="number">
    ///   <item>Sends an HTTP GET with <see cref="HttpCompletionOption.ResponseHeadersRead"/> for streaming.</item>
    ///   <item>Ensures the response status is successful (throws on HTTP error codes).</item>
    ///   <item>Creates the destination directory if it does not exist.</item>
    ///   <item>Opens a file stream and reads the HTTP response content in chunks (delegated to <see cref="HttpDownloadExecutor.StreamDownloadAsync"/>).</item>
    ///   <item>Reports 100% completion progress when finished.</item>
    ///   <item>On exception (except <see cref="OperationCanceledException"/>), returns a failed <see cref="DownloadResult"/> instead of throwing.</item>
    /// </list>
    /// </remarks>
    public async Task<DownloadResult> ExecuteAsync(
        DownloadAsset asset, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await _client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var fs = new FileStream(destPath, FileMode.Create);
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            var (downloaded, elapsed) = await HttpDownloadExecutor.StreamDownloadAsync(
                stream, fs, total, 0, destPath, progress, sw, token).ConfigureAwait(false);

            progress?.Report(new DownloadProgress(
                Path.GetFileName(destPath), downloaded, total > 0 ? total : null, 100, DownloadStatus.Completed));
            return new DownloadResult(asset, destPath, downloaded, elapsed, 0, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new DownloadResult(asset, destPath, 0, sw.Elapsed, 0, false, ex.Message);
        }
    }
}
