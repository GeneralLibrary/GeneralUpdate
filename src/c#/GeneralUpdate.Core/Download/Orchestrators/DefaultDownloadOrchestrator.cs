using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Executors;
using GeneralUpdate.Core.Download.Policy;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Orchestrators;

/// <summary>
/// Default download orchestrator with parallel execution and concurrency limit.
/// </summary>
public class DefaultDownloadOrchestrator : IDownloadOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly IDownloadPolicy _policy;

    public DefaultDownloadOrchestrator(HttpClient httpClient, IDownloadPolicy? policy = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _policy = policy ?? new DefaultRetryPolicy();
    }

    public async Task<DownloadReport> ExecuteAsync(
        IReadOnlyList<string> urls,
        string destDir,
        int maxConcurrency = 3,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<DownloadResult>();
        var sem = new SemaphoreSlim(maxConcurrency);
        long totalBytes = 0;

        var tasks = urls.Select(async (url, i) =>
        {
            await sem.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(fileName)) fileName = $"download_{i}";
                var destPath = Path.Combine(destDir, fileName);

                var executor = new HttpDownloadExecutor(_httpClient);
                var r = await _policy.ExecuteAsync(ct =>
                    executor.ExecuteAsync(url, destPath, progress, ct), token)
                    .ConfigureAwait(false);

                lock (results)
                {
                    results.Add(r);
                    if (r.Success) totalBytes += r.DownloadedBytes;
                }
            }
            finally { sem.Release(); }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        sw.Stop();

        return new DownloadReport(
            results,
            totalBytes,
            sw.Elapsed,
            results.Count(r => r.Success),
            results.Count(r => !r.Success));
    }
}
