using GeneralUpdate.Core.Events.CommonArgs;
using System;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadErrorEventArgs : BaseEventArgs
    {
        public Exception Exception { get; set; }

        public object Version { get; set; }
    }
}