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
using GeneralUpdate.Core.Download.Pipeline;

namespace GeneralUpdate.Core.Download.Orchestrators;

/// <summary>
/// Default download orchestrator with parallel execution, concurrency limit,
/// SHA256 verification, and progress reporting.
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

    /// <summary>Execute downloads for all assets in the plan.</summary>
    public async Task<DownloadReport> ExecuteAsync(
        DownloadPlan plan,
        string destDir,
        int maxConcurrency = 3,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        if (plan == null || !plan.HasAssets)
            return new DownloadReport(Array.Empty<DownloadResult>(), 0, TimeSpan.Zero, 0, 0);

        Directory.CreateDirectory(destDir);

        var sw = Stopwatch.StartNew();
        var results = new List<DownloadResult>();
        using var sem = new SemaphoreSlim(maxConcurrency);
        long totalBytes = 0;

        var tasks = plan.Assets.Select(async asset =>
        {
            await sem.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var fileName = GetFileName(asset);
                var destPath = Path.Combine(destDir, fileName);

                var executor = new HttpDownloadExecutor(_httpClient);
                var pipeline = new DefaultDownloadPipeline(asset.SHA256);

                var result = await _policy.ExecuteAsync(async ct =>
                {
                    // Download
                    var downloadResult = await executor.ExecuteAsync(
                        asset.Url, destPath,
                        progress != null ? new AssetProgressReporter(progress, asset.Name) : null,
                        ct).ConfigureAwait(false);

                    if (!downloadResult.Success)
                        return downloadResult;

                    // Verify (SHA256)
                    try
                    {
                        await pipeline.ProcessAsync(destPath, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return new DownloadResult(asset.Url, destPath,
                            downloadResult.DownloadedBytes, downloadResult.Duration,
                            downloadResult.RetryCount, false, $"SHA256 verification failed: {ex.Message}");
                    }

                    return downloadResult;
                }, token).ConfigureAwait(false);

                lock (results)
                {
                    results.Add(result);
                    if (result.Success) totalBytes += result.DownloadedBytes;
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

    private static string GetFileName(DownloadAsset asset)
    {
        try
        {
            var name = Path.GetFileName(new Uri(asset.Url).AbsolutePath);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        catch { }
        return $"{asset.Name}.{asset.Version}";
    }

    /// <summary>Wraps progress reporting to include the asset name.</summary>
    private sealed class AssetProgressReporter : IProgress<DownloadProgress>
    {
        private readonly IProgress<DownloadProgress> _inner;
        private readonly string _assetName;
        public AssetProgressReporter(IProgress<DownloadProgress> inner, string assetName)
        { _inner = inner; _assetName = assetName; }
        public void Report(DownloadProgress value)
        {
            _inner.Report(value with { AssetName = _assetName });
        }
    }
}
