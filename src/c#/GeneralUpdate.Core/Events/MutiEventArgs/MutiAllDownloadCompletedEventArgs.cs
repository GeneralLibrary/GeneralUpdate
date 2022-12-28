using GeneralUpdate.Core.Events.CommonArgs;
using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiAllDownloadCompletedEventArgs : BaseEventArgs
    {
        public bool IsAllDownloadCompleted { get; set; }

        public IList<ValueTuple<object, string>> FailedVersions { get; set; }
    }
}