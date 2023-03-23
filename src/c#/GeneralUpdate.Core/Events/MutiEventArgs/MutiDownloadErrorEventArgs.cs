using GeneralUpdate.Core.Events.CommonArgs;
using System;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadErrorEventArgs : BaseEventArgs
    {
        public MutiDownloadErrorEventArgs(Exception exception, object version)
        {
            Exception = exception;
            Version = version;
        }

        public Exception Exception { get; set; }

        public object Version { get; set; }
    }
}