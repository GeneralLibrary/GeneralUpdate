using System;

namespace GeneralUpdate.Common.Internal;

public class OSSDownloadArgs : EventArgs
{
    public long ReadLength { get; set; }

    public long TotalLength { get; set; }

    public OSSDownloadArgs(long readLength, long totalLength) 
    {
        ReadLength = readLength;
        TotalLength = totalLength;
    }
}