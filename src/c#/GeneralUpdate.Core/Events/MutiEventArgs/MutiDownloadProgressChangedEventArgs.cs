using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events.CommonArgs;

namespace GeneralUpdate.Core.Events.MutiEventArgs
{
    public class MutiDownloadProgressChangedEventArgs : BaseEventArgs
    {
        public MutiDownloadProgressChangedEventArgs(long bytesReceived, long totalBytesToReceive, float progressPercentage, object userState)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
            ProgressPercentage = progressPercentage;
            UserState = userState;
        }

        public MutiDownloadProgressChangedEventArgs(object version, long bytesReceived, long totalBytesToReceive, float progressPercentage, object userState, string message = null)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
            ProgressPercentage = progressPercentage;
            UserState = userState;
            Version = version;
            Message = message;
        }

        public ProgressType Type
        {
            get { return ProgressType.Download; }
        }

        public long BytesReceived { get; private set; }

        public long TotalBytesToReceive { get; private set; }

        public float ProgressPercentage { get; private set; }

        public object UserState { get; set; }

        public object Version { get; private set; }

        public string Message { get; private set; }

        public double ProgressValue { get; set; }
    }
}