using System;
using System.Collections.Generic;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using IProgress = System.IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>;

namespace GeneralUpdate.Core.Download.Progress;

/// <summary>Bridges IProgress to EventManager for backward-compatible event listeners.</summary>
public class DownloadProgressReporter : IProgress
{
    private readonly Action<Models.DownloadProgress>? _onProgress;
    private readonly Action? _onCompleted;
    private readonly Action? _onAllCompleted;

    public DownloadProgressReporter(
        Action<Models.DownloadProgress>? onProgress = null,
        Action? onCompleted = null,
        Action? onAllCompleted = null)
    {
        _onProgress = onProgress;
        _onCompleted = onCompleted;
        _onAllCompleted = onAllCompleted;
    }

    public void Report(Models.DownloadProgress value)
    {
        _onProgress?.Invoke(value);

        // Fire progress event via EventManager
        EventManager.Instance.Dispatch(this, new ProgressEventArgs(value));

        if (value.Status == Models.DownloadStatus.Completed)
        {
            _onCompleted?.Invoke();
            EventManager.Instance.Dispatch(this,
                new MultiDownloadCompletedEventArgs(value.AssetName ?? "unknown", true));
        }

        if (value.Status == Models.DownloadStatus.Failed)
        {
            EventManager.Instance.Dispatch(this,
                new MultiDownloadErrorEventArgs(new Exception("Download failed"), value.AssetName ?? "unknown"));
        }

        if (value.Percentage >= 100)
        {
            _onAllCompleted?.Invoke();
            EventManager.Instance.Dispatch(this,
                new MultiAllDownloadCompletedEventArgs(true, new List<(object, string)>()));
        }
    }

    /// <summary>
    /// Create an IProgress that dispatches progress events to EventManager.
    /// </summary>
    public static IProgress CreateEventBridge()
        => new DownloadProgressReporter();
}
