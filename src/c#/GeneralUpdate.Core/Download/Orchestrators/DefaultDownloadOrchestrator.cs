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
/// 默认下载编排器，支持并行执行、并发限制、SHA256 校验、断点续传和进度报告。
/// </summary>
/// <remarks>
/// <para>
/// 该编排器承载了批量下载的核心业务流程，其工作流程如下：
/// </para>
/// <list type="number">
///   <item>
///     <description>接收 <see cref="DownloadPlan"/>，其中包含待下载的资源清单及并发设置。</description>
///   </item>
///   <item>
///     <description>使用 <see cref="SemaphoreSlim"/> 控制最大并发数，防止资源耗尽。</description>
///   </item>
///   <item>
///     <description>对每个资源并行执行：创建执行器（<see cref="HttpDownloadExecutor"/> 或自定义 <see cref="IDownloadExecutor"/>），
///     创建下载管道（默认执行 SHA256 哈希校验），并包装在重试策略（<see cref="IDownloadPolicy"/>）中。</description>
///   </item>
///   <item>
///     <description>每个资源的内部流程：下载文件 -> SHA256 校验 -> 报告进度。</description>
///   </item>
///   <item>
///     <description>所有资源完成后，返回 <see cref="DownloadReport"/>，包含每个资源的结果、总下载字节数、总耗时等信息。</description>
///   </item>
/// </list>
/// <para>
/// 所有可配置的行为均由 <see cref="DownloadOrchestratorOptions"/> 驱动，
/// 该选项映射自引导层定义的 <see cref="UpdateOptions"/>。
/// </para>
/// <para>
/// 注意：自定义执行器（<see cref="IDownloadExecutor"/>）在多个并行下载任务间共享单一实例，
/// 因此实现必须保证线程安全。基于 HttpClient 的执行器满足此要求，因为 HttpClient 设计为支持并发使用。
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
    /// 初始化 <see cref="DefaultDownloadOrchestrator"/> 的新实例。
    /// </summary>
    /// <param name="httpClient">用于 HTTP 下载的 <see cref="HttpClient"/> 实例。不能为 null。</param>
    /// <param name="options">
    /// 下载编排器选项，包含并发数、超时、校验、断点续传等设置。
    /// 为 null 时将使用 <see cref="DownloadOrchestratorOptions"/> 的默认值。
    /// </param>
    /// <param name="policy">
    /// 重试策略。为 null 时将使用 <see cref="DefaultRetryPolicy"/>，重试次数和间隔取自 <paramref name="options"/>。
    /// </param>
    /// <param name="executor">
    /// 自定义下载执行器。当需要非 HTTP 的下载方式（如 FTP、本地文件复制）时传入。
    /// 为 null 时将根据 <paramref name="httpClient"/> 创建 <see cref="HttpDownloadExecutor"/>。
    /// <para>
    /// 警告：该实例会在所有并行下载任务间共享，实现必须保证线程安全。
    /// </para>
    /// </param>
    /// <param name="pipelineFactory">
    /// 自定义下载管道的工厂委托，接收资源的 SHA256 值作为参数，返回 <see cref="IDownloadPipeline"/>。
    /// 为 null 时将使用 <see cref="DefaultDownloadPipeline"/> 执行 SHA256 校验。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 为 null 时抛出。</exception>
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
    /// 执行下载计划中的所有资源，支持并行下载、并发控制、断点续传和 SHA256 校验。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 整体执行流程如下：
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>验证 <paramref name="plan"/> 是否包含有效资源；若为空或无资源，直接返回空报告。</description>
    ///   </item>
    ///   <item>
    ///     <description>创建目标目录 <paramref name="destDir"/>（如不存则自动创建）。</description>
    ///   </item>
    ///   <item>
    ///     <description>确定有效并发数：若 <see cref="DownloadOrchestratorOptions.DiffMode"/> 为 <see cref="DiffMode.Serial"/> 则强制串行（并发数为 1）；
    ///     否则取 <paramref name="maxConcurrency"/> 与选项配置值的较大者。</description>
    ///   </item>
    ///   <item>
    ///     <description>使用 <see cref="SemaphoreSlim"/> 限制同时进行的下载任务数量。</description>
    ///   </item>
    ///   <item>
    ///     <description>对每个资源并行执行以下步骤：</description>
    ///   </item>
    ///   <item>
    ///     <description>解析文件名（参见 <see cref="GetFileName"/>）。</description>
    ///   </item>
    ///   <item>
    ///     <description>创建执行器：优先使用自定义执行器，否则创建 <see cref="HttpDownloadExecutor"/>（支持断点续传）。</description>
    ///   </item>
    ///   <item>
    ///     <description>创建下载管道：优先使用工厂委托，否则创建 <see cref="DefaultDownloadPipeline"/> 执行 SHA256 哈希校验。</description>
    ///   </item>
    ///   <item>
    ///     <description>通过重试策略执行：下载 -> 条件性 SHA256 校验（当 <see cref="DownloadOrchestratorOptions.VerifyChecksum"/> 为 true 时）。</description>
    ///   </item>
    ///   <item>
    ///     <description>每个步骤均通过 <paramref name="progress"/> 报告器上报以资源名为维度的进度信息。</description>
    ///   </item>
    ///   <item>
    ///     <description>所有资源完成后，触发一次性完成事件，并汇总返回 <see cref="DownloadReport"/>。</description>
    ///   </item>
    /// </list>
    /// <para>
    /// 关于断点续传：当 <see cref="DownloadOrchestratorOptions.EnableResume"/> 为 true 时，
    /// <see cref="HttpDownloadExecutor"/> 会在 HTTP 请求中附加 Range 头，从上次中断处继续下载。
    /// </para>
    /// <para>
    /// 关于 SHA256 校验：当 <see cref="DownloadOrchestratorOptions.VerifyChecksum"/> 为 false 时，
    /// 校验步骤将被跳过以提升性能。建议在生产环境中始终保持校验开启。
    /// </para>
    /// </remarks>
    /// <param name="plan">下载计划，包含待下载的资源列表。</param>
    /// <param name="destDir">文件下载到的目标目录路径。</param>
    /// <param name="maxConcurrency">
    /// 最大并发下载数。默认值为 3。当值小于等于 0 时将回退到 <see cref="DownloadOrchestratorOptions.MaxConcurrency"/>。
    /// </param>
    /// <param name="progress">进度报告器，用于接收每个资源的下载进度。</param>
    /// <param name="token">用于取消操作的 <see cref="CancellationToken"/>。</param>
    /// <returns>
    /// <see cref="DownloadReport"/>，包含：
    /// <list type="bullet">
    ///   <item><description>每个资源的详细结果（<see cref="DownloadResult"/>）。</description></item>
    ///   <item><description>所有成功下载的总字节数。</description></item>
    ///   <item><description>总耗时。</description></item>
    ///   <item><description>成功和失败的数量。</description></item>
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
    /// 根据资源信息解析最终的文件名。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 文件名解析优先级如下：
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>优先使用 <see cref="DownloadAsset.Name"/>，并追加 <see cref="DownloadOrchestratorOptions.Format"/> 对应的扩展名
    ///     （若名称尚未以该扩展名结尾）。</description>
    ///   </item>
    ///   <item>
    ///     <description>若名称为空，尝试从 <see cref="DownloadAsset.Url"/> 的 URI 路径中提取文件名。</description>
    ///   </item>
    ///   <item>
    ///     <description>若上述均失败，返回格式为 "{<c>Name</c>}.{<c>Version</c>}" 的回退文件名。</description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="asset">下载资源信息，包含名称、URL 和版本号。</param>
    /// <returns>解析后的目标文件名。</returns>
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
