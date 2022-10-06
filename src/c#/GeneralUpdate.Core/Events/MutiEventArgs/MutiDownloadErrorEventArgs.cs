using GeneralUpdate.Core.Events.CommonArgs;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadErrorEventArgs : BaseEventArgs
    {
        public Exception Exception { get; set; }

        public object Version { get; set; }
    }
}
