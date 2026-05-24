using System;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// Progress event args — wraps a DownloadProgress snapshot.
/// </summary>
public class ProgressEventArgs : EventArgs
{
    public DownloadProgress Progress { get; }

    public ProgressEventArgs(DownloadProgress progress)
    {
        Progress = progress;
    }
}
