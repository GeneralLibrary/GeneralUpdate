using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MutiEventArgs;
using System;
using System.ComponentModel;

namespace GeneralUpdate.Core.Events
{
    public class MutiEvent
    {
        public delegate void MutiAllDownloadCompletedEventHandler(object sender, MutiAllDownloadCompletedEventArgs e);

        public event MutiAllDownloadCompletedEventHandler MutiAllDownloadCompleted;

        public delegate void MutiDownloadProgressChangedEventHandler(object sender, MutiDownloadProgressChangedEventArgs e);

        public event MutiDownloadProgressChangedEventHandler MutiDownloadProgressChanged;

        public delegate void MutiDownloadCompletedEventHandler(object sender, MutiDownloadCompletedEventArgs e);

        public event MutiDownloadCompletedEventHandler MutiDownloadCompleted;

        public delegate void MutiDownloadErrorEventHandler(object sender, MutiDownloadErrorEventArgs e);

        public event MutiDownloadErrorEventHandler MutiDownloadError;

        public delegate void MutiDownloadStatisticsEventHandler(object sender, MutiDownloadStatisticsEventArgs e);

        public event MutiDownloadStatisticsEventHandler MutiDownloadStatistics;

        public delegate void ExceptionEventHandler(object sender, ExceptionEventArgs e);

        public event ExceptionEventHandler Exception;

        public delegate void MutiAsyncCompletedEventHandler(object sender, AsyncCompletedEventArgs e);

        public event MutiAsyncCompletedEventHandler DownloadFileCompleted;
    }

    public class CommonEvent
    {
        public delegate void GeneralExceptionEventHandler(object sender, Exception e);

        public event GeneralExceptionEventHandler GeneralException;
    }
}