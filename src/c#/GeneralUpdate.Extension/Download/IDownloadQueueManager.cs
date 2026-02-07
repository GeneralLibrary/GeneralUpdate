using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Download;

/// <summary>
/// Interface for download queue management
/// </summary>
public interface IDownloadQueueManager : IDisposable
{
    /// <summary>
    /// Event fired when download status changes
    /// </summary>
    event EventHandler<DownloadTaskEventArgs>? DownloadStatusChanged;

    /// <summary>
    /// Add download task to queue
    /// </summary>
    /// <param name="task">Download task</param>
    void Enqueue(DownloadTask task);

    /// <summary>
    /// Get task status by extension ID
    /// </summary>
    /// <param name="extensionId">Extension ID</param>
    /// <returns>Download task or null</returns>
    DownloadTask? GetTask(string extensionId);

    /// <summary>
    /// Cancel download task
    /// </summary>
    /// <param name="extensionId">Extension ID</param>
    void CancelTask(string extensionId);

    /// <summary>
    /// Get all active tasks
    /// </summary>
    /// <returns>List of active tasks</returns>
    List<DownloadTask> GetActiveTasks();
}

/// <summary>
/// Event arguments for download task status changes
/// </summary>
public class DownloadTaskEventArgs : EventArgs
{
    /// <summary>
    /// The download task
    /// </summary>
    public DownloadTask Task { get; set; } = null!;
}
