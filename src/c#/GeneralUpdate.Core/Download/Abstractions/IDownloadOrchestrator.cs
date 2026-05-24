using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>Orchestrates batch downloads with concurrency control.</summary>
public interface IDownloadOrchestrator
{
    /// <summary>
    /// Execute downloads for all assets in the plan.
    /// Handles parallelism, retry, and SHA256 verification.
    /// </summary>
    Task<DownloadReport> ExecuteAsync(
        DownloadPlan plan,
        string destDir,
        int maxConcurrency = 3,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default);
}

public record DownloadReport(
    IReadOnlyList<DownloadResult> Results,
    long TotalBytes,
    TimeSpan TotalDuration,
    int SuccessCount,
    int FailedCount
);
