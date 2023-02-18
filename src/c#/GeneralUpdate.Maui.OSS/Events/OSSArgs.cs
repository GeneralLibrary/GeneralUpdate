namespace GeneralUpdate.Maui.OSS.Events
{
    public class OSSDownloadArgs : EventArgs
    {
        public OSSDownloadArgs(long currentByte, long totalByte)
        {
            CurrentByte = currentByte;
            TotalByte = totalByte;
        }

        /// <summary>
        /// Size of the file that has been downloaded.
        /// </summary>
        public long CurrentByte { get; set; }

        /// <summary>
        /// Total file size.
        /// </summary>
        public long TotalByte { get; set; }
    }
}