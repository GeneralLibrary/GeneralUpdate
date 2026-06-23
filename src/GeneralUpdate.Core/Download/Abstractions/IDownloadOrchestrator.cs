using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>
/// Orchestrates batch downloads with concurrency control, retry logic, and SHA256 verification.
/// Implementations manage the lifecycle of multiple concurrent download tasks
/// and aggregate their results into a single <see cref="DownloadReport"/>.
/// </summary>
/// <remarks>
/// <para>
/// The orchestrator is the top-level component in the download subsystem. It:
/// </para>
/// <list type="bullet">
///   <item><description>Accepts a <see cref="DownloadPlan"/> containing assets to download.</description></item>
///   <item><description>Controls maximum parallelism via a semaphore.</description></item>
///   <item><description>Creates executors (<see cref="IDownloadExecutor"/>) and pipelines (<see cref="IDownloadPipeline"/>) per asset.</description></item>
///   <item><description>Wraps each download in a retry policy (<see cref="IDownloadPolicy"/>).</description></item>
///   <item><description>Reports per-asset progress through <see cref="IProgress{T}"/>.</description></item>
///   <item><description>Returns a <see cref="DownloadReport"/> with per-result details and overall statistics.</description></item>
/// </list>
/// </remarks>
public interface IDownloadOrchestrator
{
    /// <summary>
    /// Executes the download plan for all assets, managing parallelism, retries,
    /// SHA256 verification, and progress reporting.
    /// </summary>
    /// <param name="plan">The download plan containing the list of assets to download. Must not be null.</param>
    /// <param name="destDir">The destination directory path where files will be saved.</param>
    /// <param name="maxConcurrency">
    /// The maximum number of concurrent downloads. Defaults to 3.
    /// When set to 0 or a negative value, falls back to the value configured in options.
    /// </param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> receiver for per-asset download progress notifications.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    /// A <see cref="DownloadReport"/> containing:
    /// <list type="bullet">
    ///   <item><description>A list of <see cref="DownloadResult"/> for each asset.</description></item>
    ///   <item><description>Total bytes downloaded across all successful assets.</description></item>
    ///   <item><description>Total elapsed duration.</description></item>
    ///   <item><description>Counts of successful and failed downloads.</description></item>
    /// </list>
    /// </returns>
    Task<DownloadReport> ExecuteAsync(
        DownloadPlan plan,
        string destDir,
        int maxConcurrency = 3,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default);
}

/// <summary>
/// Represents the aggregated result of a batch download operation executed by an <see cref="IDownloadOrchestrator"/>.
/// </summary>
/// <param name="Results">The list of per-asset download results.</param>
/// <param name="TotalBytes">The total number of bytes successfully downloaded across all assets.</param>
/// <param name="TotalDuration">The total elapsed time of the entire download operation.</param>
/// <param name="SuccessCount">The number of assets that were downloaded successfully.</param>
/// <param name="FailedCount">The number of assets that failed to download.</param>
public record DownloadReport(
    IReadOnlyList<DownloadResult> Results,
    long TotalBytes,
    TimeSpan TotalDuration,
    int SuccessCount,
    int FailedCount
);
