using GeneralUpdate.Core.Events.CommonArgs;
using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiAllDownloadCompletedEventArgs : BaseEventArgs
    {
        public MutiAllDownloadCompletedEventArgs() { }

        public MutiAllDownloadCompletedEventArgs(bool isAllDownloadCompleted, IList<(object, string)> failedVersions)
        {
            IsAllDownloadCompleted = isAllDownloadCompleted;
            FailedVersions = failedVersions;
        }

        public bool IsAllDownloadCompleted { get; set; }

        public IList<ValueTuple<object, string>> FailedVersions { get; set; }
    }
}