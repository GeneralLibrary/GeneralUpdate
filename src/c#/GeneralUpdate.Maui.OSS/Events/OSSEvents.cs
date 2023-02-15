namespace GeneralUpdate.Maui.OSS.Events
{
    public class OSSEvents
    {
        public delegate void DownloadEventHandler(object sender, OSSDownloadArgs e);

        /// <summary>
        /// Download the progress notification event.
        /// </summary>
        public event DownloadEventHandler Download;

        public delegate void UnZipCompletedEventHandler(object sender, Zip.Events.BaseCompleteEventArgs e);

        /// <summary>
        /// The compressed package is decompressed.
        /// </summary>
        public event UnZipCompletedEventHandler UnZipCompleted;

        public delegate void UnZipProgressEventHandler(object sender, Zip.Events.BaseUnZipProgressEventArgs e);

        /// <summary>
        /// Decompression progress of the package.
        /// </summary>
        public event UnZipProgressEventHandler UnZipProgress;
    }
}
