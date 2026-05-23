using System;

namespace GeneralUpdate.Extension.Common.Models;

/// <summary>
/// Classifies the type of download failure for diagnostics.
/// </summary>
public enum DownloadErrorType
{
    /// <summary>No error; download succeeded.</summary>
    None = 0,

    /// <summary>Network connectivity issue.</summary>
    NetworkError = 1,

    /// <summary>HTTP 4xx client error (e.g., 404 Not Found).</summary>
    ClientError = 2,

    /// <summary>HTTP 5xx server error.</summary>
    ServerError = 3,

    /// <summary>Hash verification failed after download.</summary>
    HashMismatch = 4,

    /// <summary>File I/O error (disk full, permission denied).</summary>
    IoError = 5,

    /// <summary>Download was cancelled.</summary>
    Cancelled = 6,

    /// <summary>Unknown or unclassified error.</summary>
    Unknown = 99
}

/// <summary>
/// Structured result from a download operation, providing detailed error information.
/// </summary>
public class DownloadResult
{
    /// <summary>Whether the download succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error classification, or <see cref=\"DownloadErrorType.None\"/> on success.</summary>
    public DownloadErrorType ErrorType { get; set; }

    /// <summary>Human-readable error message (set on failure).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>HTTP status code, if applicable.</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Create a success result.
    /// </summary>
    public static DownloadResult Ok() => new() { Success = true, ErrorType = DownloadErrorType.None };

    /// <summary>
    /// Create a failure result.
    /// </summary>
    public static DownloadResult Fail(DownloadErrorType type, string message, int? httpStatus = null) =>
        new() { Success = false, ErrorType = type, ErrorMessage = message, HttpStatusCode = httpStatus };
}
