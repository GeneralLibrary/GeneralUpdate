using System;
using System.Collections.Generic;

namespace MyApp.Extensions.Security
{
    /// <summary>
    /// Represents an index for managing local offline packages.
    /// </summary>
    public class OfflinePackageIndex
    {
        /// <summary>
        /// Gets or sets the version of the index format.
        /// </summary>
        public string IndexVersion { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the index was created.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the index was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the list of packages in the index.
        /// </summary>
        public List<OfflinePackageEntry> Packages { get; set; }

        /// <summary>
        /// Gets or sets metadata about the index.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }
    }

    /// <summary>
    /// Represents an entry in an offline package index.
    /// </summary>
    public class OfflinePackageEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier of the extension.
        /// </summary>
        public string ExtensionId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the extension.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the version of the extension.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the author of the extension.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Gets or sets the description of the extension.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the relative path to the package file.
        /// </summary>
        public string PackagePath { get; set; }

        /// <summary>
        /// Gets or sets the size of the package in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the hash of the package.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the hash algorithm used.
        /// </summary>
        public string HashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the list of dependencies.
        /// </summary>
        public List<string> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the package was added to the index.
        /// </summary>
        public DateTime AddedDate { get; set; }
    }
}
