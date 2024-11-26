using System;

namespace GeneralUpdate.Common.Download;

public class MultiDownloadStatisticsEventArgs(object version
    , TimeSpan remaining
    , string speed
    , long totalBytes
    , long bytesReceived
    , double progressPercentage) : EventArgs
{
    public object Version { get; private set; } = version;

    public TimeSpan Remaining { get; private set; } = remaining;

    public string Speed { get; private set; } = speed;

    public long TotalBytesToReceive { get; private set; } = totalBytes;

    public long BytesReceived { get; private set; } = bytesReceived;

    public double ProgressPercentage { get; private set; } = progressPercentage;
}