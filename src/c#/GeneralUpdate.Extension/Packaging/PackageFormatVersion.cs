using System;
using MyApp.Extensions;

namespace MyApp.Extensions.Packaging
{
    /// <summary>
    /// Represents the standardized format version of a plugin package.
    /// </summary>
    public class PackageFormatVersion
    {
        /// <summary>
        /// Gets or sets the major version number.
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Gets or sets the minor version number.
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Gets or sets the patch version number.
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Gets or sets the pre-release label (e.g., "alpha", "beta", "rc").
        /// </summary>
        public string PreRelease { get; set; }

        /// <summary>
        /// Gets or sets the build metadata.
        /// </summary>
        public string BuildMetadata { get; set; }

        /// <summary>
        /// Returns the string representation of the package format version.
        /// </summary>
        /// <returns>A string in the format "major.minor.patch[-prerelease][+buildmetadata]".</returns>
        public override string ToString()
        {
            var version = $"{Major}.{Minor}.{Patch}";
            if (!string.IsNullOrEmpty(PreRelease))
            {
                version += $"-{PreRelease}";
            }
            if (!string.IsNullOrEmpty(BuildMetadata))
            {
                version += $"+{BuildMetadata}";
            }
            return version;
        }

        /// <summary>
        /// Parses a version string into a PackageFormatVersion instance.
        /// </summary>
        /// <param name="versionString">The version string to parse.</param>
        /// <returns>A PackageFormatVersion instance.</returns>
        public static PackageFormatVersion Parse(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                throw new ArgumentException("Version string cannot be null or empty.", nameof(versionString));

            // Reuse SemVersion parsing logic
            var semVer = SemVersion.Parse(versionString);
            
            return new PackageFormatVersion
            {
                Major = semVer.Major,
                Minor = semVer.Minor,
                Patch = semVer.Patch,
                PreRelease = semVer.PreRelease,
                BuildMetadata = semVer.BuildMetadata
            };
        }
    }
}
