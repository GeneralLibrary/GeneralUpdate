namespace GeneralUpdate.OSS.Events
{
    public class OSSEvents
    {
        public delegate void DownloadEventHandler(object sender, OSSDownloadArgs e);

        public event DownloadEventHandler Download;

        public delegate void UnZipCompletedEventHandler(object sender, Zip.Events.BaseCompleteEventArgs e);

        public event UnZipCompletedEventHandler UnZipCompleted;

        public delegate void UnZipProgressEventHandler(object sender, Zip.Events.BaseUnZipProgressEventArgs e);

        public event UnZipProgressEventHandler UnZipProgress;
    }
}
