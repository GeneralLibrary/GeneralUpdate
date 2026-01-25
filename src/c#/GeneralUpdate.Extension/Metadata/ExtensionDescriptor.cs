using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Extension.Metadata
{
    /// <summary>
    /// Represents the comprehensive metadata descriptor for an extension package.
    /// Follows VS Code extension manifest structure (package.json) standards.
    /// Provides all necessary information for discovery, compatibility checking, and installation.
    /// </summary>
    public class ExtensionDescriptor
    {
        /// <summary>
        /// Gets or sets the unique extension identifier (lowercase, no spaces).
        /// This is the unique identifier used in the marketplace and follows VS Code naming convention.
        /// Example: "my-extension" or "publisher.extension-name"
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable display name of the extension.
        /// This is shown in the UI and can contain spaces and mixed case.
        /// Example: "My Extension" or "Awesome Extension Pack"
        /// </summary>
        [JsonPropertyName("displayName")]
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
        /// Gets or sets the publisher identifier (follows VS Code convention).
        /// The publisher is the organization or individual that published the extension.
        /// </summary>
        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }

        /// <summary>
        /// Gets or sets the license identifier (e.g., "MIT", "Apache-2.0").
        /// </summary>
        [JsonPropertyName("license")]
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the extension categories (follows VS Code convention).
        /// Examples: "Programming Languages", "Debuggers", "Formatters", "Linters", etc.
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string>? Categories { get; set; }

        /// <summary>
        /// Gets or sets the icon path for the extension (relative to package root).
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        /// <summary>
        /// Gets or sets the repository URL for the extension source code.
        /// </summary>
        [JsonPropertyName("repository")]
        public string? Repository { get; set; }

        /// <summary>
        /// Gets or sets the platforms supported by this extension.
        /// Uses flags to allow multiple platform targets.
        /// </summary>
        [JsonPropertyName("supportedPlatforms")]
        public TargetPlatform SupportedPlatforms { get; set; } = TargetPlatform.All;

        /// <summary>
        /// Gets or sets the version compatibility constraints for the host application.
        /// Similar to VS Code's "engines" field, specifies which host versions are supported.
        /// </summary>
        [JsonPropertyName("engines")]
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
