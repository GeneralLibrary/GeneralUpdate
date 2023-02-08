namespace GeneralUpdate.OSS.Events
{
    public class OSSDownloadArgs : EventArgs
    {
        public OSSDownloadArgs(long currentByte,long totalByte) 
        {
            CurrentByte = currentByte;
            TotalByte = totalByte;
        }

        public long CurrentByte { get; set; }

        public long TotalByte { get; set; }
    }
}
