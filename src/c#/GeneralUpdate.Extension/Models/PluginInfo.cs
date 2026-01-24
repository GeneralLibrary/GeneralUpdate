using System;
using System.Collections.Generic;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents metadata for a plugin, including identification, versioning, and platform compatibility information.
    /// </summary>
    public class PluginInfo
    {
        /// <summary>
        /// Unique identifier for the plugin (e.g., "publisher.pluginname").
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name of the plugin.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Current version of the plugin (semantic versioning recommended).
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Publisher or author of the plugin.
        /// </summary>
        public string Publisher { get; set; }

        /// <summary>
        /// Brief description of the plugin's functionality.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Target platform(s) for the plugin (e.g., "win32-x64", "darwin-arm64", "linux-x64", "any").
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Target CPU architecture (e.g., "x64", "arm64", "x86", "any").
        /// </summary>
        public string Architecture { get; set; }

        /// <summary>
        /// Indicates if the plugin is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Indicates if the plugin is installed locally.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Indicates if an update is available for this plugin.
        /// </summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// The available version on the server (if an update is available).
        /// </summary>
        public string AvailableVersion { get; set; }

        /// <summary>
        /// Download URL for the plugin package.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Installation path for the plugin.
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Indicates if auto-update is enabled for this specific plugin.
        /// </summary>
        public bool AutoUpdateEnabled { get; set; }

        /// <summary>
        /// Additional metadata as key-value pairs.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// List of plugin IDs that this plugin depends on.
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// Minimum version of the host application required.
        /// </summary>
        public string MinHostVersion { get; set; }

        /// <summary>
        /// Maximum version of the host application supported.
        /// </summary>
        public string MaxHostVersion { get; set; }

        /// <summary>
        /// Timestamp of when the plugin was last updated.
        /// </summary>
        public DateTime? LastUpdated { get; set; }
    }
}
