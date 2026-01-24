using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Extension.Metadata
{
    /// <summary>
    /// Represents the comprehensive metadata descriptor for an extension package.
    /// Provides all necessary information for discovery, compatibility checking, and installation.
    /// </summary>
    public class ExtensionDescriptor
    {
        /// <summary>
        /// Gets or sets the unique identifier for the extension.
        /// Must be unique across all extensions in the marketplace.
        /// </summary>
        [JsonPropertyName("id")]
        public string ExtensionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable display name of the extension.
        /// </summary>
        [JsonPropertyName("name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the semantic version string of the extension.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a brief description of the extension's functionality.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the author or publisher name of the extension.
        /// </summary>
        [JsonPropertyName("author")]
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the license identifier (e.g., "MIT", "Apache-2.0").
        /// </summary>
        [JsonPropertyName("license")]
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the platforms supported by this extension.
        /// Uses flags to allow multiple platform targets.
        /// </summary>
        [JsonPropertyName("supportedPlatforms")]
        public TargetPlatform SupportedPlatforms { get; set; } = TargetPlatform.All;

        /// <summary>
        /// Gets or sets the content type classification of the extension.
        /// Determines runtime requirements and execution model.
        /// </summary>
        [JsonPropertyName("contentType")]
        public ExtensionContentType ContentType { get; set; } = ExtensionContentType.Custom;

        /// <summary>
        /// Gets or sets the version compatibility constraints for the host application.
        /// </summary>
        [JsonPropertyName("compatibility")]
        public VersionCompatibility Compatibility { get; set; } = new VersionCompatibility();

        /// <summary>
        /// Gets or sets the download URL for the extension package.
        /// </summary>
        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the cryptographic hash for package integrity verification.
        /// </summary>
        [JsonPropertyName("hash")]
        public string? PackageHash { get; set; }

        /// <summary>
        /// Gets or sets the package size in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long PackageSize { get; set; }

        /// <summary>
        /// Gets or sets the release date and time for this version.
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the list of extension IDs that this extension depends on.
        /// </summary>
        [JsonPropertyName("dependencies")]
        public List<string>? Dependencies { get; set; }

        /// <summary>
        /// Gets or sets custom properties for extension-specific metadata.
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, string>? CustomProperties { get; set; }

        /// <summary>
        /// Parses the version string and returns a Version object.
        /// </summary>
        /// <returns>A Version object if parsing succeeds; otherwise, null.</returns>
        public Version? GetVersionObject()
        {
            return System.Version.TryParse(Version, out var version) ? version : null;
        }
    }
}
