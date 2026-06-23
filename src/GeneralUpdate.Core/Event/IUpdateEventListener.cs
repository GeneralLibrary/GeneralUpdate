using GeneralUpdate.Core.Download;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// Defines the update event listener interface, which establishes the contract
/// for bulk subscription to all event types in the update lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface and register it via
/// <c>new GeneralUpdateBootstrap().AddEventListener&lt;MyListener&gt;()</c>
/// to receive event notifications at each stage of the update process.
/// </para>
/// <para>
/// The event types cover the full update lifecycle:
/// <list type="bullet">
///   <item><description><see cref="OnUpdateInfo"/>: Fires when update version information is found.</description></item>
///   <item><description><see cref="OnDownloadStatistics"/> / <see cref="OnProgress"/>: Download statistics and progress information.</description></item>
///   <item><description><see cref="OnDownloadCompleted"/> / <see cref="OnAllDownloadCompleted"/>: Download completion notifications.</description></item>
///   <item><description><see cref="OnDownloadError"/>: Fires when a download error occurs.</description></item>
///   <item><description><see cref="OnException"/>: Fires when an exception occurs during the update flow.</description></item>
/// </list>
/// </para>
/// <para>
/// If you only need to handle a subset of events, consider inheriting from
/// <see cref="UpdateEventListenerBase"/> and overriding only the methods you care about.
/// </para>
/// </remarks>
public interface IUpdateEventListener
{
    /// <summary>
    /// Fires when all download tasks have completed.
    /// </summary>
    /// <param name="args">Event arguments containing the overall download completion status.</param>
    void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args);

    /// <summary>
    /// Fires when a single download task has completed.
    /// </summary>
    /// <param name="args">Event arguments containing the completion status of a single download.</param>
    void OnDownloadCompleted(MultiDownloadCompletedEventArgs args);

    /// <summary>
    /// Fires when an error occurs during a download.
    /// </summary>
    /// <param name="args">Event arguments containing download error information.</param>
    void OnDownloadError(MultiDownloadErrorEventArgs args);

    /// <summary>
    /// Fires when download statistics are updated.
    /// </summary>
    /// <param name="args">Event arguments containing download statistics (speed, progress, etc.).</param>
    void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args);

    /// <summary>
    /// Fires when update version information is retrieved.
    /// </summary>
    /// <param name="args">Event arguments containing the update version information.</param>
    void OnUpdateInfo(UpdateInfoEventArgs args);

    /// <summary>
    /// Fires when an exception occurs during the update flow.
    /// </summary>
    /// <param name="args">Event arguments containing exception information.</param>
    void OnException(ExceptionEventArgs args);

    /// <summary>
    /// Fires when real-time download progress is updated.
    /// </summary>
    /// <param name="args">Event arguments containing download progress data.</param>
    void OnProgress(ProgressEventArgs args);
}

/// <summary>
/// Base class for <see cref="IUpdateEventListener"/> providing empty default implementations
/// for all event methods.
/// Inherit from this class and override only the event methods you wish to handle,
/// avoiding the burden of implementing every method in the interface.
/// </summary>
/// <remarks>
/// Usage example:
/// <code>
/// public class MyListener : UpdateEventListenerBase
/// {
///     public override void OnProgress(ProgressEventArgs args)
///     {
///         Console.WriteLine($"Progress: {args.Progress?.ProgressValue}%");
///     }
/// }
/// </code>
/// </remarks>
public abstract class UpdateEventListenerBase : IUpdateEventListener
{
    /// <summary>
    /// Empty default implementation for the all-downloads-completed event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args) { }

    /// <summary>
    /// Empty default implementation for the single-download-completed event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnDownloadCompleted(MultiDownloadCompletedEventArgs args) { }

    /// <summary>
    /// Empty default implementation for the download error event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnDownloadError(MultiDownloadErrorEventArgs args) { }

    /// <summary>
    /// Empty default implementation for the download statistics update event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args) { }

    /// <summary>
    /// Empty default implementation for the update information available event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnUpdateInfo(UpdateInfoEventArgs args) { }

    /// <summary>
    /// Empty default implementation for the exception event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnException(ExceptionEventArgs args) { }

    /// <summary>
    /// Empty default implementation for the download progress update event.
    /// Override this method to handle the event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    public virtual void OnProgress(ProgressEventArgs args) { }
}
