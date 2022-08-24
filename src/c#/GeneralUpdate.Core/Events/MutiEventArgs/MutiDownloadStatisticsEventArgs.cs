using GeneralUpdate.Core.Events.CommonArgs;
using System;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadStatisticsEventArgs : BaseEventArgs
    {
        public object Version { get; set; }

        public DateTime Remaining { get; set; }

        public string Speed { get; set; }

        public MutiDownloadStatisticsEventArgs(object version, DateTime remaining, string speed)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Remaining = remaining;
            Speed = speed ?? throw new ArgumentNullException(nameof(speed));
        }
    }
}
