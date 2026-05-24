using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Event;

namespace GeneralUpdate.Core.Event;

/// <summary>Batch event registration — implement once, register once.</summary>
public interface IUpdateEventListener
{
    void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args);
    void OnDownloadCompleted(MultiDownloadCompletedEventArgs args);
    void OnDownloadError(MultiDownloadErrorEventArgs args);
    void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args);
    void OnUpdateInfo(UpdateInfoEventArgs args);
    void OnException(ExceptionEventArgs args);
    void OnProgress(DownloadProgress progress);
}

/// <summary>Progress event args for AddListenerProgress.</summary>
public class ProgressEventArgs : EventArgs
{
    public DownloadProgress Progress { get; }
    public ProgressEventArgs(DownloadProgress progress) => Progress = progress;
}
