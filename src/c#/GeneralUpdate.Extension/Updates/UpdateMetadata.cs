using System;
using System.Collections.Generic;

namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Represents metadata for available updates, including version lists and compatibility information.
    /// </summary>
    public class UpdateMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier of the extension.
        /// </summary>
        public string ExtensionId { get; set; }

        /// <summary>
        /// Gets or sets the current version of the extension.
        /// </summary>
        public string CurrentVersion { get; set; }

        /// <summary>
        /// Gets or sets the latest available version.
        /// </summary>
        public string LatestVersion { get; set; }

        /// <summary>
        /// Gets or sets the list of available versions.
        /// </summary>
        public List<string> AvailableVersions { get; set; }

        /// <summary>
        /// Gets or sets the update channel.
        /// </summary>
        public UpdateChannel Channel { get; set; }

        /// <summary>
        /// Gets or sets the last update check timestamp.
        /// </summary>
        public DateTime LastChecked { get; set; }

        /// <summary>
        /// Gets or sets the release date of the latest version.
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the minimum host version required for the update.
        /// </summary>
        public string MinimumHostVersion { get; set; }

        /// <summary>
        /// Gets or sets the maximum host version compatible with the update.
        /// </summary>
        public string MaximumHostVersion { get; set; }

        /// <summary>
        /// Gets or sets the changelog or release notes for the update.
        /// </summary>
        public string Changelog { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the update is mandatory.
        /// </summary>
        public bool IsMandatory { get; set; }
    }
}
