using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents the metadata for an extension.
    /// This is a universal structure that can describe various types of extensions.
    /// </summary>
    public class ExtensionMetadata
    {
        /// <summary>
        /// Unique identifier for the extension.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the extension.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Version of the extension.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the extension does.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Author or publisher of the extension.
        /// </summary>
        [JsonPropertyName("author")]
        public string? Author { get; set; }

        /// <summary>
        /// License information for the extension.
        /// </summary>
        [JsonPropertyName("license")]
        public string? License { get; set; }

        /// <summary>
        /// Platforms supported by this extension.
        /// </summary>
        [JsonPropertyName("supportedPlatforms")]
        public ExtensionPlatform SupportedPlatforms { get; set; } = ExtensionPlatform.All;

        /// <summary>
        /// Type of content this extension provides.
        /// </summary>
        [JsonPropertyName("contentType")]
        public ExtensionContentType ContentType { get; set; } = ExtensionContentType.Other;

        /// <summary>
        /// Version compatibility information.
        /// </summary>
        [JsonPropertyName("compatibility")]
        public VersionCompatibility Compatibility { get; set; } = new VersionCompatibility();

        /// <summary>
        /// Download URL for the extension package.
        /// </summary>
        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Hash value for verifying the extension package integrity.
        /// </summary>
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        /// <summary>
        /// Size of the extension package in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        /// Release date of this extension version.
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// Dependencies on other extensions (extension IDs).
        /// </summary>
        [JsonPropertyName("dependencies")]
        public List<string>? Dependencies { get; set; }

        /// <summary>
        /// Additional custom properties for extension-specific data.
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; set; }

        /// <summary>
        /// Gets the version as a Version object.
        /// </summary>
        /// <returns>Parsed Version object or null if parsing fails.</returns>
        public Version? GetVersion()
        {
            return System.Version.TryParse(Version, out var version) ? version : null;
        }
    }
}
