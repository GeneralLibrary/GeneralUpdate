using System;

namespace GeneralUpdate.Common.Download
{
    public class MultiDownloadStatisticsEventArgs : EventArgs
    {
        public object Version { get; set; }

        public DateTime Remaining { get; set; }

        public string Speed { get; set; }

        public MultiDownloadStatisticsEventArgs(object version, DateTime remaining, string speed)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Remaining = remaining;
            Speed = speed ?? throw new ArgumentNullException(nameof(speed));
        }
    }
}