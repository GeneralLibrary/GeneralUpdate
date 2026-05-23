using System;

namespace GeneralUpdate.Core.Download
{
    public class MultiDownloadCompletedEventArgs(object version, bool isComplated) : EventArgs
    {
        public object Version { get; private set; } = version;

        public bool IsComplated { get; private set; } = isComplated;
    }
}