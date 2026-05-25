using System;

namespace GeneralUpdate.Core.Download
{
    public class MultiDownloadCompletedEventArgs(object version, bool isCompleted) : EventArgs
    {
        public object Version { get; private set; } = version;

        public bool isCompleted { get; private set; } = isCompleted;
    }
}