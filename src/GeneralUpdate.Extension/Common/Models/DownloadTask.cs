using GeneralUpdate.Extension.Common.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.Models;

/// <summary>
/// Download task information
/// </summary>
public class DownloadTask
{
    /// <summary>
    /// Extension metadata
    /// </summary>
    public ExtensionMetadata Extension { get; set; } = null!;

    /// <summary>
    /// Save path for downloaded file
    /// </summary>
    public string SavePath { get; set; } = string.Empty;

    /// <summary>
    /// Current status
    /// </summary>
    public ExtensionUpdateStatus Status { get; set; }

    /// <summary>
    /// Progress percentage
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Cancellation token source
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
}
