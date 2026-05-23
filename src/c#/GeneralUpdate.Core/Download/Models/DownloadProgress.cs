using System;

namespace GeneralUpdate.Core.Download.Models;

public enum DownloadStatus { Pending, Downloading, Completed, Failed, Retrying }

public enum DownloadPriority { Low = 0, Normal = 1, High = 2 }

public record DownloadProgress(
    string? AssetName,
    long BytesDownloaded,
    long? TotalBytes,
    double Percentage,
    DownloadStatus Status
);

public record DownloadResult(
    string? Url,
    string? LocalPath,
    long DownloadedBytes,
    TimeSpan Duration,
    int RetryCount,
    bool Success,
    string? ErrorMessage
);
