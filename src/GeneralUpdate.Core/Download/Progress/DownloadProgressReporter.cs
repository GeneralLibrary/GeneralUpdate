using System;
using System.Collections.Generic;
using GeneralUpdate.Core.Event;
using IProgress = System.IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>;

namespace GeneralUpdate.Core.Download.Progress;

/// <summary>
/// Download progress reporter that bridges <see cref="IProgress{T}"/> progress events
/// to the <see cref="EventManager"/> event system, providing backwards-compatible
/// subscription for legacy event listeners.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IProgress{T}"/> (where T is <c>DownloadProgress</c>)
/// and triggers the following events when reporting download progress:
/// </para>
/// <list type="bullet">
///   <item><term><c>ProgressEventArgs</c></term><description>Fired on every progress report,
///         containing the download percentage, bytes downloaded, and other information.</description></item>
///   <item><term><c>MultiDownloadCompletedEventArgs</c></term><description>Fired when the download status is <c>Completed</c>.</description></item>
///   <item><term><c>MultiDownloadErrorEventArgs</c></term><description>Fired when the download status is <c>Failed</c>.</description></item>
///   <item><term><c>MultiAllDownloadCompletedEventArgs</c></term><description>Fired via the static <c>DispatchAllCompleted</c> method when all download tasks are finished.</description></item>
/// </list>
/// <para>
/// This class also supports direct injection of <c>onProgress</c> and <c>onCompleted</c>
/// callback delegates as an alternative notification channel alongside EventManager.
/// </para>
/// </remarks>
public class DownloadProgressReporter : IProgress
{
    private readonly Action<Models.DownloadProgress>? _onProgress;
    private readonly Action? _onCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadProgressReporter"/> class
    /// with optional progress and completion callbacks.
    /// </summary>
    /// <param name="onProgress">A callback delegate invoked on each progress report.</param>
    /// <param name="onCompleted">A callback delegate invoked when the download completes.</param>
    public DownloadProgressReporter(
        Action<Models.DownloadProgress>? onProgress = null,
        Action? onCompleted = null)
    {
        _onProgress = onProgress;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// Reports download progress. Invokes the progress callback, fires EventManager events,
    /// and triggers completion or failure events based on the download status.
    /// </summary>
    /// <param name="value">A <see cref="Models.DownloadProgress"/> instance containing the current download progress information.</param>
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
    /// Dispatches the all-downloads-completed event. This method should be called once
    /// after all download tasks are finished, not after each individual asset completes.
    /// Typically called by the download orchestrator after all downloads have completed.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="success">Whether all downloads completed successfully.</param>
    /// <param name="details">The list of details for each downloaded asset, containing the asset object and file name.</param>
    public static void DispatchAllCompleted(object sender, bool success, List<(object, string)> details)
    {
        EventManager.Instance.Dispatch(sender,
            new MultiAllDownloadCompletedEventArgs(success, details ?? new List<(object, string)>()));
    }

    /// <summary>
    /// Creates an <see cref="IProgress{T}"/> instance that dispatches progress events
    /// to the EventManager.
    /// </summary>
    /// <returns>A new <see cref="DownloadProgressReporter"/> instance bridging progress reporting to the event system.</returns>
    /// <remarks>
    /// This factory method creates a reporter without custom callbacks, dispatching events
    /// exclusively through EventManager. Suitable for scenarios where only EventManager
    /// subscriptions are needed without direct callbacks.
    /// </remarks>
    public static IProgress CreateEventBridge()
        => new DownloadProgressReporter();
}
