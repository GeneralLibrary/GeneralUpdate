using System;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     Shared version identity fields common to <see cref="PushPayload"/>,
///     <see cref="VersionEntry"/>, and <see cref="OssVersionRecord"/>.
///     Eliminates duplicate field declarations across version DTOs and
///     enables unified <see cref="Download.Models.DownloadAsset"/> construction.
/// </summary>
public abstract class VersionIdentity
{
    /// <summary>The version name or label (e.g., "v1.0.1").</summary>
    public virtual string? Name { get; set; }

    /// <summary>SHA256 hash of the update package for integrity verification.</summary>
    public virtual string? Hash { get; set; }

    /// <summary>Release date of this version.</summary>
    public virtual DateTime? ReleaseDate { get; set; }

    /// <summary>Download URL of the update package.</summary>
    public virtual string? Url { get; set; }

    /// <summary>Version number string (e.g., "1.0.0.1").</summary>
    public virtual string? Version { get; set; }

    /// <summary>Application type identifier.</summary>
    public virtual int? AppType { get; set; }

    /// <summary>Target platform identifier.</summary>
    public virtual int? Platform { get; set; }

    /// <summary>Product identifier for version filtering.</summary>
    public virtual string? ProductId { get; set; }
}
