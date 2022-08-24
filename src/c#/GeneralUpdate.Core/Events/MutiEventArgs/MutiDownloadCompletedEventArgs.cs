using GeneralUpdate.Core.Events.CommonArgs;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadCompletedEventArgs : BaseEventArgs
    {
        public object Version { get; set; }
    }
}
