using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace GeneralUpdate.Core.Bootstrap
{
    public class DownloadProgressChangedEventArgsEx : EventArgs
    {
        public long BytesReceived { get; private set; }

        public long TotalBytesToReceive { get; private set; }

        public float ProgressPercentage { get; private set; }

        public object UserState { get; set; }

        public DownloadProgressChangedEventArgsEx(long received, long toReceive, float progressPercentage, object userState)
        {
            BytesReceived = received;
            TotalBytesToReceive = toReceive;
            ProgressPercentage = progressPercentage;
            UserState = userState;
        }
    }

    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public ExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    #region Muti

    public class MutiDownloadStatisticsEventArgs : EventArgs
    {
        public MutiDownloadStatisticsEventArgs(object version,DateTime remaining,string speed) 
        {
            Version = version;
            Remaining = remaining;
            Speed = speed;
        }

        public object Version { get; set; }

        public DateTime Remaining { get; set; }

        public string Speed { get; set; }
    }

    public class MutiDownloadProgressChangedEventArgs : DownloadProgressChangedEventArgsEx
    {
        public ProgressType Type { get; set; }

        public object Version { get; set; }

        public string Message { get; set; }

        public double ProgressValue { get; set; }

        public MutiDownloadProgressChangedEventArgs(object version, ProgressType type, string message, long received = 0, long toReceive = 0, float progressPercentage = 0, object userState = null)
            : base(received, toReceive, progressPercentage, userState)
        {
            ProgressValue = progressPercentage;
            Version = version;
            Message = message;
            Type = type;
        }
    }

    public class MutiDownloadCompletedEventArgs : AsyncCompletedEventArgs
    {
        public object Version { get; set; }

        public MutiDownloadCompletedEventArgs(object version, Exception error, bool cancelled, object userState) : base(error, cancelled, userState)
        {
            Version = version;
        }
    }

    public class MutiAllDownloadCompletedEventArgs : EventArgs
    {
        public bool IsAllDownloadCompleted { get; set; }

        public IList<ValueTuple<object, string>> FailedVersions { get; set; }

        public MutiAllDownloadCompletedEventArgs(bool isAllDownloadCompleted, IList<ValueTuple<object, string>> failedVersions)
        {
            IsAllDownloadCompleted = isAllDownloadCompleted;
            FailedVersions = failedVersions;
        }
    }

    public class MutiDownloadErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public object Version { get; set; }

        public MutiDownloadErrorEventArgs(Exception exception, object updateVersion)
        {
            Exception = exception;
            Version = updateVersion;
        }
    }

    #endregion Muti
}