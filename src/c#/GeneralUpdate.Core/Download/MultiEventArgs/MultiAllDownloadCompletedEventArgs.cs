using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Download
{
    public class MultiAllDownloadCompletedEventArgs : EventArgs
    {
        public MultiAllDownloadCompletedEventArgs(bool isAllDownloadCompleted, IList<(object, string)> failedVersions)
        {
            IsAllDownloadCompleted = isAllDownloadCompleted;
            FailedVersions = failedVersions;
        }

        public bool IsAllDownloadCompleted { get; set; }

        public IList<ValueTuple<object, string>> FailedVersions { get; set; }
    }
}