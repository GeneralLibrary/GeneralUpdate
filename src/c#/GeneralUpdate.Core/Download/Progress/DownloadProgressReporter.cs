using System;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Progress;

/// <summary>Bridges IProgress<DownloadProgress> to event-based listeners.</summary>
public class DownloadProgressReporter : IProgress<DownloadProgress>
{
    private readonly Action<DownloadProgress>? _onProgress;
    private readonly Action<DownloadResult>? _onCompleted;
    private readonly Action<DownloadResult, Exception>? _onError;

    public DownloadProgressReporter(
        Action<DownloadProgress>? onProgress = null,
        Action<DownloadResult>? onCompleted = null,
        Action<DownloadResult, Exception>? onError = null)
    {
        _onProgress = onProgress;
        _onCompleted = onCompleted;
        _onError = onError;
    }

    public void Report(DownloadProgress value)
    {
        _onProgress?.Invoke(value);
        if (value.Status == DownloadStatus.Completed)
            _onCompleted?.Invoke(null!); // result will be set by caller
    }
}
