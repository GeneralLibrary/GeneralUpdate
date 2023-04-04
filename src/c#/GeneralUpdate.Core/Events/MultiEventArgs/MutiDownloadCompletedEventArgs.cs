using System;
using System.ComponentModel;

namespace GeneralUpdate.Core.Events.MultiEventArgs
{
    public class MultiDownloadCompletedEventArgs : AsyncCompletedEventArgs
    {
        public MultiDownloadCompletedEventArgs(Exception error, bool cancelled, object userState) : base(error, cancelled, userState)
        {
        }

        public MultiDownloadCompletedEventArgs(object version, Exception error, bool cancelled, object userState) : base(error, cancelled, userState)
        {
            Version = version;
        }

        public object Version { get; set; }
    }
}