using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Executors;
using GeneralUpdate.Core.Download.Policy;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Pipeline;
using GeneralUpdate.Core.Download.Progress;

namespace GeneralUpdate.Core.Download.Orchestrators;

/// <summary>
/// Default download orchestrator with parallel execution, concurrency limit,
/// SHA256 verification, resume support, and progress reporting.
///
/// All configurable behaviour is driven by <see cref="DownloadOrchestratorOptions"/>,
/// which maps to the <see cref="UpdateOptions"/> defined in the bootstrap layer.
/// </summary>
public class DefaultDownloadOrchestrator : IDownloadOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly IDownloadPolicy _policy;
    private readonly DownloadOrchestratorOptions _options;
    private readonly IDownloadExecutor? _customExecutor;
    private readonly Func<string?, IDownloadPipeline>? _pipelineFactory;

    public DefaultDownloadOrchestrator(
        HttpClient httpClient,
        DownloadOrchestratorOptions? options = null,
        IDownloadPolicy? policy = null,
        IDownloadExecutor? executor = null,
        Func<string?, IDownloadPipeline>? pipelineFactory = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new DownloadOrchestratorOptions();
        _policy = policy ?? new DefaultRetryPolicy(_options.RetryCount, _options.RetryInterval);
        _customExecutor = executor;
        _pipelineFactory = pipelineFactory;
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

        // Resolve effective concurrency: Serial mode forces 1.
        // Uses _options.MaxConcurrency as primary value; the method parameter
        // maxConcurrency acts as an override (default 3).
        var baseConcurrency = maxConcurrency > 0 ? maxConcurrency : _options.MaxConcurrency;
        var effectiveConcurrency = _options.DiffMode == DiffMode.Serial
            ? 1
            : DownloadOrchestratorOptions.SanitizeMaxConcurrency(Math.Max(1, baseConcurrency));

        GeneralTracer.Info($"DefaultDownloadOrchestrator.ExecuteAsync: concurrency={effectiveConcurrency}, " +
            $"resume={_options.EnableResume}, verifyChecksum={_options.VerifyChecksum}, diffMode={_options.DiffMode}");

        var sw = Stopwatch.StartNew();
        var results = new List<DownloadResult>();
        using var sem = new SemaphoreSlim(effectiveConcurrency);
        long totalBytes = 0;

        var tasks = plan.Assets.Select(async asset =>
        {
            var acquired = await sem.WaitAsync(TimeSpan.FromMinutes(5), token).ConfigureAwait(false);
            if (!acquired)
            {
                GeneralTracer.Warn("DefaultDownloadOrchestrator: semaphore wait timed out for " + asset.Name + ", skipping.");
                lock (results)
                {
                    results.Add(new DownloadResult(asset, null, 0, TimeSpan.Zero, 0, false, "Semaphore wait timed out"));
                }
                return;
            }
            try
            {
                var fileName = GetFileName(asset);
                var destPath = Path.Combine(destDir, fileName);

                var executor = _customExecutor ?? new HttpDownloadExecutor(_httpClient, _options.DownloadTimeout, _options.EnableResume);
                var pipeline = _pipelineFactory?.Invoke(asset.SHA256) ?? new DefaultDownloadPipeline(asset.SHA256);

                var result = await _policy.ExecuteAsync(async ct =>
                {
                    // Download
                    var downloadResult = await executor.ExecuteAsync(
                        asset, destPath,
                        progress != null ? new AssetProgressReporter(progress, asset.Name) : null,
                        ct).ConfigureAwait(false);

                    if (!downloadResult.Success)
                        return downloadResult;

                    // Verify (SHA256) — conditionally skipped when VerifyChecksum is false
                    if (!_options.VerifyChecksum)
                    {
                        GeneralTracer.Info($"DefaultDownloadOrchestrator: checksum verification skipped for {asset.Name} (VerifyChecksum=false).");
                        return downloadResult;
                    }

                    try
                    {
                        await pipeline.ProcessAsync(destPath, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return new DownloadResult(asset, destPath,
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

        // Dispatch all-completed event ONCE after all assets finish (only failed results)
        var failedDetails = results.Where(r => !r.Success)
            .Select(r => ((object)r.Asset, r.ErrorMessage ?? "failed")).ToList();
        DownloadProgressReporter.DispatchAllCompleted(
            this,
            results.All(r => r.Success),
            failedDetails);

        return new DownloadReport(
            results,
            totalBytes,
            sw.Elapsed,
            results.Count(r => r.Success),
            results.Count(r => !r.Success));
    }

    private string GetFileName(DownloadAsset asset)
    {
        if (!string.IsNullOrEmpty(asset.Name))
        {
            var name = asset.Name;
            var ext = _options.Format.ToExtension();
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                name += ext;
            return name;
        }

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
