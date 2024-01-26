using System;

namespace GeneralUpdate.Core.Events.OSSArgs
{
    public class OSSDownloadArgs : EventArgs
    {
        /// <summary>
        /// The number of file bytes read when the file was downloaded.
        /// </summary>
        public long ReadLength { get; set; }

        /// <summary>
        /// The total number of bytes of the file that needs to be downloaded.
        /// </summary>
        public long TotalLength { get; set; }

        public OSSDownloadArgs(long readLength, long totalLength)
        {
            ReadLength = readLength;
            TotalLength = totalLength;
        }
    }
}