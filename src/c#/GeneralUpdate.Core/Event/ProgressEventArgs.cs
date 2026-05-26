using System;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Models;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// Progress event args — wraps a DownloadProgress or DiffProgress snapshot.
/// </summary>
public class ProgressEventArgs : EventArgs
{
    public DownloadProgress? Progress { get; }

    public DiffProgress? DiffProgress { get; }

    public ProgressEventArgs(DownloadProgress progress)
    {
        Progress = progress;
    }

    public ProgressEventArgs(DiffProgress diffProgress)
    {
        DiffProgress = diffProgress;
    }
}
