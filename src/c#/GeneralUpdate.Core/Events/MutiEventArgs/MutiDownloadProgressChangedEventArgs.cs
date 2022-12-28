using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events.CommonArgs;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadProgressChangedEventArgs : BaseEventArgs
    {
        public ProgressType Type { get; set; }

        public object Version { get; set; }

        public string Message { get; set; }

        public double ProgressValue { get; set; }
    }
}