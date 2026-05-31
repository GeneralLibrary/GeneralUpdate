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
/// Default download orchestrator supporting parallel execution, concurrency limiting,
/// SHA256 verification, resumable downloads, and progress reporting.
/// </summary>
/// <remarks>
/// <para>
/// This orchestrator implements the core batch download workflow:
/// </para>
/// <list type="number">
///   <item>
///     <description>Receives a <see cref="DownloadPlan"/> containing the asset manifest and concurrency settings.</description>
///   </item>
///   <item>
///     <description>Uses a <see cref="SemaphoreSlim"/> to control maximum concurrency and prevent resource exhaustion.</description>
///   </item>
///   <item>
///     <description>For each asset, executes in parallel: creates an executor (<see cref="HttpDownloadExecutor"/> or custom <see cref="IDownloadExecutor"/>),
///     creates a download pipeline (default performs SHA256 hash verification), and wraps it in a retry policy (<see cref="IDownloadPolicy"/>).</description>
///   </item>
///   <item>
///     <description>Internal flow per asset: Download file -> SHA256 verification (optional) -> Report progress.</description>
///   </item>
///   <item>
///     <description>After all assets complete, returns a <see cref="DownloadReport"/> containing per-asset results,
///     total bytes downloaded, total duration, and success/failure counts.</description>
///   </item>
/// </list>
/// <para>
/// All configurable behaviors are driven by <see cref="DownloadOrchestratorOptions"/>,
/// which maps from the <see cref="Option"/> defined in the bootstrap layer.
/// </para>
/// <para>
/// Note: The custom executor (<see cref="IDownloadExecutor"/>) is shared as a single instance
/// across parallel download tasks, so implementations must be thread-safe.
/// HttpClient-based executors satisfy this requirement because HttpClient is designed for concurrent use.
/// </para>
/// </remarks>
public class DefaultDownloadOrchestrator : IDownloadOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly IDownloadPolicy _policy;
    private readonly DownloadOrchestratorOptions _options;
    private readonly IDownloadExecutor? _customExecutor;
    private readonly Func<string?, IDownloadPipeline>? _pipelineFactory;
    // Note: _customExecutor is a single shared instance used across parallel asset downloads.
    // Implementations of IDownloadExecutor must be thread-safe. HttpClient-based executors
    // satisfy this as HttpClient is designed for concurrent use.

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDownloadOrchestrator"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> instance used for HTTP downloads. Must not be null.</param>
    /// <param name="options">
    /// Download orchestrator options including concurrency, timeout, verification, and resume settings.
    /// If null, default <see cref="DownloadOrchestratorOptions"/> values will be used.
    /// </param>
    /// <param name="policy">
    /// The retry policy. If null, a <see cref="DefaultRetryPolicy"/> will be used with retry count
    /// and interval taken from <paramref name="options"/>.
    /// </param>
    /// <param name="executor">
    /// A custom download executor for non-HTTP download methods (e.g., FTP, local file copy).
    /// If null, an <see cref="HttpDownloadExecutor"/> will be created from <paramref name="httpClient"/>.
    /// <para>
    /// WARNING: This instance is shared across all parallel download tasks; implementations must be thread-safe.
    /// </para>
    /// </param>
    /// <param name="pipelineFactory">
    /// A factory delegate for creating custom download pipelines. Receives the asset's SHA256 value
    /// and returns an <see cref="IDownloadPipeline"/>. If null, <see cref="DefaultDownloadPipeline"/>
    /// is used for SHA256 verification.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> is null.</exception>
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

    /// <summary>
    /// Executes all assets in the download plan, supporting parallel downloads, concurrency control,
    /// resumable downloads, and SHA256 verification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Overall execution flow:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>Validates <paramref name="plan"/> contains valid assets; returns an empty report if null or empty.</description>
    ///   </item>
    ///   <item>
    ///     <description>Creates the destination directory <paramref name="destDir"/> (auto-creates if it does not exist).</description>
    ///   </item>
    ///   <item>
    ///     <description>Determines effective concurrency: if <see cref="DownloadOrchestratorOptions.DiffMode"/> is
    ///     <see cref="DiffMode.Serial"/>, forces serial execution (concurrency = 1); otherwise uses the greater of
    ///     <paramref name="maxConcurrency"/> and the configured value.</description>
    ///   </item>
    ///   <item>
    ///     <description>Uses <see cref="SemaphoreSlim"/> to limit the number of simultaneous download tasks.</description>
    ///   </item>
    ///   <item>
    ///     <description>For each asset, executes the following steps in parallel:</description>
    ///   </item>
    ///   <item>
    ///     <description>Resolves the file name (see <see cref="GetFileName"/>).</description>
    ///   </item>
    ///   <item>
    ///     <description>Creates the executor: uses the custom executor if provided, otherwise creates
    ///     an <see cref="HttpDownloadExecutor"/> (with resume support).</description>
    ///   </item>
    ///   <item>
    ///     <description>Creates the download pipeline: uses the factory delegate if provided, otherwise creates
    ///     a <see cref="DefaultDownloadPipeline"/> for SHA256 hash verification.</description>
    ///   </item>
    ///   <item>
    ///     <description>Executes through the retry policy: Download -> Conditional SHA256 verification
    ///     (when <see cref="DownloadOrchestratorOptions.VerifyChecksum"/> is true).</description>
    ///   </item>
    ///   <item>
    ///     <description>Each step reports progress via <paramref name="progress"/> using the asset name as the dimension.</description>
    ///   </item>
    ///   <item>
    ///     <description>After all assets complete, fires a one-time completion event and returns
    ///     the aggregated <see cref="DownloadReport"/>.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// About resumable downloads: When <see cref="DownloadOrchestratorOptions.EnableResume"/> is true,
    /// <see cref="HttpDownloadExecutor"/> attaches a Range header to the HTTP request to continue
    /// downloading from where it was interrupted.
    /// </para>
    /// <para>
    /// About SHA256 verification: When <see cref="DownloadOrchestratorOptions.VerifyChecksum"/> is false,
    /// the verification step is skipped for improved performance. It is recommended to keep verification
    /// enabled in production environments.
    /// </para>
    /// </remarks>
    /// <param name="plan">The download plan containing the list of assets to download.</param>
    /// <param name="destDir">The destination directory path where files will be saved.</param>
    /// <param name="maxConcurrency">
    /// Maximum number of concurrent downloads. Defaults to 3. When set to 0 or a negative value,
    /// falls back to <see cref="DownloadOrchestratorOptions.MaxConcurrency"/>.
    /// </param>
    /// <param name="progress">A progress reporter for receiving per-asset download progress.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    /// A <see cref="DownloadReport"/> containing:
    /// <list type="bullet">
    ///   <item><description>Detailed results for each asset (<see cref="DownloadResult"/>).</description></item>
    ///   <item><description>Total bytes successfully downloaded.</description></item>
    ///   <item><description>Total elapsed duration.</description></item>
    ///   <item><description>Counts of successful and failed downloads.</description></item>
    /// </list>
    /// </returns>
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

    /// <summary>
    /// Resolves the final file name from the asset information.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file name resolution priority is as follows:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>First uses <see cref="DownloadAsset.Name"/> and appends the extension corresponding to
    ///     <see cref="DownloadOrchestratorOptions.Format"/> (if the name does not already end with that extension).</description>
    ///   </item>
    ///   <item>
    ///     <description>If the name is empty, attempts to extract the file name from the URI path of <see cref="DownloadAsset.Url"/>.</description>
    ///   </item>
    ///   <item>
    ///     <description>If all the above fail, returns a fallback file name in the format "{<c>Name</c>}.{<c>Version</c>}".</description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="asset">The download asset information containing name, URL, and version.</param>
    /// <returns>The resolved destination file name.</returns>
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
