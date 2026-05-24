namespace GeneralUpdate.Core.Configuration;

/// <summary>Object Storage Service provider enumeration.</summary>
public enum OssProvider
{
    /// <summary>Aliyun OSS (Alibaba Cloud).</summary>
    AliYun = 1,

    /// <summary>Amazon Web Services S3.</summary>
    AWS = 2,

    /// <summary>MinIO (self-hosted S3-compatible).</summary>
    MinIO = 3,

    /// <summary>Tencent Cloud COS.</summary>
    Tencent = 4
}
