using System;
using System.ComponentModel;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadCompletedEventArgs : AsyncCompletedEventArgs
    {
        public MutiDownloadCompletedEventArgs(Exception error, bool cancelled, object userState) : base(error, cancelled, userState)
        {
        }

        public MutiDownloadCompletedEventArgs(object version, Exception error, bool cancelled, object userState) : base(error, cancelled, userState)
        {
            Version = version;
        }

        public object Version { get; set; }
    }
}