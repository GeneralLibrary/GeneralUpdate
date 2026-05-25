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

    public DownloadProgressReporter(
        Action<Models.DownloadProgress>? onProgress = null,
        Action? onCompleted = null)
    {
        _onProgress = onProgress;
        _onCompleted = onCompleted;
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
    }

    /// <summary>
    /// Fires the all-completed event. Should be called once after all downloads finish,
    /// not per-asset. Called from the download orchestrator.
    /// </summary>
    public static void DispatchAllCompleted(bool success, List<(object, string)> details)
    {
        EventManager.Instance.Dispatch(null!,
            new MultiAllDownloadCompletedEventArgs(success, details ?? new List<(object, string)>()));
    }

    /// <summary>
    /// Create an IProgress that dispatches progress events to EventManager.
    /// </summary>
    public static IProgress CreateEventBridge()
        => new DownloadProgressReporter();
}
