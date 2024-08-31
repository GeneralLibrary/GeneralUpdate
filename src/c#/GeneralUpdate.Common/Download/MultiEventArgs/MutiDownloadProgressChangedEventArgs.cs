using System;

namespace GeneralUpdate.Common.Download
{
    public class MultiDownloadProgressChangedEventArgs : EventArgs
    {
        public MultiDownloadProgressChangedEventArgs(long bytesReceived, long totalBytesToReceive, float progressPercentage, object userState)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
            ProgressPercentage = progressPercentage;
            UserState = userState;
        }

        public MultiDownloadProgressChangedEventArgs(object version, long bytesReceived, long totalBytesToReceive, double progressPercentage, object userState, string message = null)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
            ProgressPercentage = progressPercentage;
            UserState = userState;
            Version = version;
            Message = message;
        }

        public MultiDownloadProgressChangedEventArgs(object version, string message)
        {
            Version = version;
            Message = message;
        }

        public MultiDownloadProgressChangedEventArgs(object version, ProgressType type, string message)
        {
            Version = version;
            Type = type;
            Message = message;
        }

        public ProgressType Type { get; set; }

        public long BytesReceived { get; private set; }

        public long TotalBytesToReceive { get; private set; }

        public double ProgressPercentage { get; private set; }

        public object UserState { get; set; }

        public object Version { get; private set; }

        public string Message { get; private set; }

        public double ProgressValue { get; set; }
    }
}