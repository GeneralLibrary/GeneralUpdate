using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>Orchestrates batch downloads with concurrency control.</summary>
public interface IDownloadOrchestrator
{
    Task<DownloadReport> ExecuteAsync(
        IReadOnlyList<string> urls,
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
