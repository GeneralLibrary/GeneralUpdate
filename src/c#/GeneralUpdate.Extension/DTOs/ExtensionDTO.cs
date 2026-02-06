using System;
using System.Collections.Generic;

namespace GeneralUpdate.Extension.DTOs
{
    /// <summary>
    /// Extension data transfer object
    /// </summary>
    public class ExtensionDTO
    {
        /// <summary>
        /// Extension unique identifier
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
        /// Extension status (false-Disabled, true-Enabled)
        /// </summary>
        public bool? Status { get; set; }

        /// <summary>
        /// Extension description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// File format/extension
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
        /// Extension categories
        /// </summary>
        public List<string>? Categories { get; set; }

        /// <summary>
        /// Supported platforms
        /// </summary>
        public Metadata.TargetPlatform SupportedPlatforms { get; set; } = Metadata.TargetPlatform.All;

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
        /// List of extension IDs (Guids) that this extension depends on
        /// </summary>
        public List<string>? Dependencies { get; set; }

        /// <summary>
        /// Pre-release flag
        /// </summary>
        public bool IsPreRelease { get; set; }

        /// <summary>
        /// Download URL for the extension package
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Custom properties for extension-specific metadata
        /// </summary>
        public Dictionary<string, string>? CustomProperties { get; set; }

        /// <summary>
        /// Indicates whether the extension is compatible with the requested host version.
        /// Null if no host version was specified in the query.
        /// True if compatible, False if incompatible.
        /// </summary>
        public bool? IsCompatible { get; set; }
    }
}
