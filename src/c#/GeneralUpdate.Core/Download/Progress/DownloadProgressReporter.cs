using System;
using IProgress = System.IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>;

namespace GeneralUpdate.Core.Download.Progress;

/// <summary>Bridges IProgress to event-based callbacks for download status.</summary>
public class DownloadProgressReporter : IProgress
{
    private readonly Action<Download.Models.DownloadProgress>? _onProgress;
    private readonly Action? _onCompleted;
    private readonly Action? _onAllCompleted;

    public DownloadProgressReporter(
        Action<Download.Models.DownloadProgress>? onProgress = null,
        Action? onCompleted = null,
        Action? onAllCompleted = null)
    {
        _onProgress = onProgress;
        _onCompleted = onCompleted;
        _onAllCompleted = onAllCompleted;
    }

    public void Report(Download.Models.DownloadProgress value)
    {
        _onProgress?.Invoke(value);
        if (value.Status == Download.Models.DownloadStatus.Completed)
        {
            _onCompleted?.Invoke();
            if (value.Percentage >= 100)
                _onAllCompleted?.Invoke();
        }
    }
}
