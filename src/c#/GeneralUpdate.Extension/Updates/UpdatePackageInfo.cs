using System;

namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Represents information about an update package, including full, delta, or differential package details.
    /// </summary>
    public class UpdatePackageInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the package.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// Gets or sets the version of the update package.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the type of package (e.g., "Full", "Delta", "Diff").
        /// </summary>
        public string PackageType { get; set; }

        /// <summary>
        /// Gets or sets the size of the package in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the hash of the package for integrity verification.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the hash algorithm used (e.g., "SHA256", "SHA512").
        /// </summary>
        public string HashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the download URL for the package.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the signature of the package for verification.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the package was created.
        /// </summary>
        public DateTime CreatedTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the baseline version required for delta/diff packages.
        /// </summary>
        public string BaselineVersion { get; set; }

        /// <summary>
        /// Gets or sets the target version that will be achieved after applying the package.
        /// </summary>
        public string TargetVersion { get; set; }
    }
}
