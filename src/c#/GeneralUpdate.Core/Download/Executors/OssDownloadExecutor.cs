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
        string url, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            using var fs = new FileStream(destPath, FileMode.Create);
            using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            long downloaded = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read, token);
                downloaded += read;
                var pct = total > 0 ? (double)downloaded / total * 100 : -1;
                progress?.Report(new DownloadProgress(
                    Path.GetFileName(destPath), downloaded, total > 0 ? total : null, pct, DownloadStatus.Downloading));
            }
            sw.Stop();
            progress?.Report(new DownloadProgress(Path.GetFileName(destPath), downloaded, total > 0 ? total : null, 100, DownloadStatus.Completed));
            return new DownloadResult(url, destPath, downloaded, sw.Elapsed, 0, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new DownloadResult(url, null, 0, sw.Elapsed, 0, false, ex.Message);
        }
    }
}
