using System;
using GeneralUpdate.Core.Download.Reporting;

namespace GeneralUpdate.Core.Download.Models;

public enum DownloadStatus { Pending, Downloading, Completed, Failed, Retrying }

public enum DownloadPriority { Low = 0, Normal = 1, High = 2 }

public record DownloadProgress(
    string? AssetName,
    long BytesDownloaded,
    long? TotalBytes,
    double Percentage,
    DownloadStatus Status,
    object? VersionContext = null
);

public record DownloadResult(
    DownloadAsset Asset,
    string LocalPath,
    long DownloadedBytes,
    TimeSpan Duration,
    int RetryCount,
    bool Success,
    string? ErrorMessage
)
{
    /// <summary>The original local exception, when available. Never sent to the server.</summary>
    public Exception? Error { get; init; }

    /// <summary>The known failure category; null when the origin is unknown.</summary>
    public FailureCategory? FailureCategory { get; init; }
}
