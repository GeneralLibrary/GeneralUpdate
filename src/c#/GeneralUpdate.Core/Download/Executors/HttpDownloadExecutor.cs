using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Executors;

/// <summary>
/// HTTP-based download executor with optional Range/resume support.
/// Uses the shared HttpClient from VersionService for consistent SSL/auth handling.
/// </summary>
public class HttpDownloadExecutor : IDownloadExecutor
{
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly bool _enableResume;

    public HttpDownloadExecutor(HttpClient client, TimeSpan? timeout = null, bool enableResume = true)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _enableResume = enableResume;
    }

    public async Task<DownloadResult> ExecuteAsync(
        string url, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        int retries = 0;
        long totalBytes = -1;
        long existingBytes = 0;

        // Check for existing partial file (resume support; skip when disabled)
        if (_enableResume && File.Exists(destPath))
        {
            existingBytes = new FileInfo(destPath).Length;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Request resume from existing position (skip when resume is disabled)
            if (_enableResume && existingBytes > 0)
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(_timeout);

            using var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            // If server doesn't support Range, discard partial file
            if (_enableResume && existingBytes > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                existingBytes = 0;
                File.Delete(destPath);
            }

            response.EnsureSuccessStatusCode();
            totalBytes = response.Content.Headers.ContentLength ?? -1;

            // Append or create file
            var mode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
            using var fs = new FileStream(destPath, mode, FileAccess.Write, FileShare.None);
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            var buffer = new byte[8192];
            long downloaded = existingBytes;
            int read;
            long lastReport = 0;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                downloaded += read;

                // Report progress every ~250ms
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
            progress?.Report(new DownloadProgress(
                Path.GetFileName(destPath), downloaded,
                totalBytes > 0 ? totalBytes + existingBytes : null,
                100, DownloadStatus.Completed));

            return new DownloadResult(url, destPath, downloaded, sw.Elapsed, retries, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new DownloadResult(url, null, existingBytes, sw.Elapsed, retries, false, ex.Message);
        }
    }
}
