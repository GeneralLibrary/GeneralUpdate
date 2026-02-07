using GeneralUpdate.Extension.Common.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.Models;

/// <summary>
/// Core metadata for extensions
/// </summary>
public class ExtensionMetadata
{
    /// <summary>
    /// Extension unique identifier (GUID)
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Extension name (unique identifier, lowercase, no spaces)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Human-readable display name of the extension
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Extension version
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Upload timestamp
    /// </summary>
    public DateTime? UploadTime { get; set; }

    /// <summary>
    /// Extension status (0-Disabled, 1-Enabled)
    /// </summary>
    public bool? Status { get; set; }

    /// <summary>
    /// Extension description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// File format/extension (e.g., .dll, .zip)
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// File hash (SHA256)
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Publisher identifier
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// License identifier (e.g., "MIT", "Apache-2.0")
    /// </summary>
    public string? License { get; set; }

    /// <summary>
    /// Extension categories (comma-separated)
    /// </summary>
    public string? Categories { get; set; }

    /// <summary>
    /// Supported platforms (stored as integer flags)
    /// </summary>
    public TargetPlatform SupportedPlatforms { get; set; } = TargetPlatform.All;

    /// <summary>
    /// Minimum host application version required
    /// </summary>
    public string? MinHostVersion { get; set; }

    /// <summary>
    /// Maximum host application version supported
    /// </summary>
    public string? MaxHostVersion { get; set; }

    /// <summary>
    /// Release date and time for this version
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// List of extension IDs (Guids) this extension depends on (comma-separated)
    /// </summary>
    public string? Dependencies { get; set; }

    /// <summary>
    /// Pre-release flag (0-Stable, 1-PreRelease)
    /// </summary>
    public bool IsPreRelease { get; set; }

    /// <summary>
    /// Download URL for the extension package
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Custom properties for extension-specific metadata (JSON string)
    /// </summary>
    public string? CustomProperties { get; set; }
}
