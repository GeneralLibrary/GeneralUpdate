using System;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Models;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// Event arguments for progress updates, encapsulating a snapshot of
/// download progress or differential patch progress.
/// </summary>
/// <remarks>
/// <para>
/// ProgressEventArgs carries one of two possible progress data types:
/// <list type="bullet">
///   <item><description><see cref="DownloadProgress"/>: Progress information during file download
///   (download speed, bytes completed, total bytes, etc.).</description></item>
///   <item><description><see cref="DiffProgress"/>: Progress information during differential patch
///   generation or application.</description></item>
/// </list>
/// </para>
/// <para>
/// Event receivers should check which of <see cref="Progress"/> and
/// <see cref="DiffProgress"/> is not <c>null</c> to determine the current
/// progress event type. Both properties will never be set simultaneously.
/// </para>
/// </remarks>
public class ProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets the download progress snapshot object.
    /// </summary>
    /// <value>A <see cref="DownloadProgress"/> instance if this event is a download progress update;
    /// otherwise <c>null</c>.</value>
    public DownloadProgress? Progress { get; }

    /// <summary>
    /// Gets the differential patch progress snapshot object.
    /// </summary>
    /// <value>A <see cref="DiffProgress"/> instance if this event is a differential patch progress update;
    /// otherwise <c>null</c>.</value>
    public DiffProgress? DiffProgress { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressEventArgs"/> class
    /// with download progress data.
    /// </summary>
    /// <param name="progress">The download progress snapshot.</param>
    public ProgressEventArgs(DownloadProgress progress)
    {
        Progress = progress;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressEventArgs"/> class
    /// with differential patch progress data.
    /// </summary>
    /// <param name="diffProgress">The differential patch progress snapshot.</param>
    public ProgressEventArgs(DiffProgress diffProgress)
    {
        DiffProgress = diffProgress;
    }
}
