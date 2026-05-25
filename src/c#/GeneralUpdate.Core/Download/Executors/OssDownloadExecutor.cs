using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Executors;

/// <summary>OSS-based download executor. Delegates to HTTP GET for OSS signed URLs.</summary>
public class OssDownloadExecutor : IDownloadExecutor
{
    private readonly HttpClient _client;

    public OssDownloadExecutor(HttpClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

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
            return new DownloadResult(asset, null, 0, sw.Elapsed, 0, false, ex.Message);
        }
    }
}
