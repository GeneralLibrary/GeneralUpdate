using GeneralUpdate.Core.Events.CommonArgs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadCompletedEventArgs : AsyncCompletedEventArgs
    {
        public MutiDownloadCompletedEventArgs(object version, Exception error, bool cancelled, object userState) : base(error, cancelled, userState)
        {
            Version = version;
        }

        public object Version { get; set; }
    }
}
