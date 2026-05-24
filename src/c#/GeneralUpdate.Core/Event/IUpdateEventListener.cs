using GeneralUpdate.Core.Download;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// Batch registration interface for all update event types.
/// Implement this interface and register via
/// <c>new GeneralUpdateBootstrap().AddEventListener&lt;MyListener&gt;()</c>.
/// </summary>
public interface IUpdateEventListener
{
    /// <summary>All downloads completed.</summary>
    void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args);

    /// <summary>Single download completed.</summary>
    void OnDownloadCompleted(MultiDownloadCompletedEventArgs args);

    /// <summary>Download error.</summary>
    void OnDownloadError(MultiDownloadErrorEventArgs args);

    /// <summary>Download statistics updated.</summary>
    void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args);

    /// <summary>Update information available.</summary>
    void OnUpdateInfo(UpdateInfoEventArgs args);

    /// <summary>Exception occurred.</summary>
    void OnException(ExceptionEventArgs args);

    /// <summary>Real-time download progress.</summary>
    void OnProgress(ProgressEventArgs args);
}

/// <summary>
/// Base class that implements IUpdateEventListener with no-op methods.
/// Inherit from this and override only the events you need.
/// </summary>
public abstract class UpdateEventListenerBase : IUpdateEventListener
{
    public virtual void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args) { }
    public virtual void OnDownloadCompleted(MultiDownloadCompletedEventArgs args) { }
    public virtual void OnDownloadError(MultiDownloadErrorEventArgs args) { }
    public virtual void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args) { }
    public virtual void OnUpdateInfo(UpdateInfoEventArgs args) { }
    public virtual void OnException(ExceptionEventArgs args) { }
    public virtual void OnProgress(ProgressEventArgs args) { }
}
