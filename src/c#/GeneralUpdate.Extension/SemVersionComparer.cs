using System;
using System.Text.RegularExpressions;

namespace MyApp.Extensions
{
    /// <summary>
    /// Default implementation of ISemVersionComparer for comparing semantic versions.
    /// </summary>
    public class SemVersionComparer : ISemVersionComparer
    {
        /// <summary>
        /// Compares two semantic versions.
        /// </summary>
        /// <param name="version1">The first version to compare.</param>
        /// <param name="version2">The second version to compare.</param>
        /// <returns>A value indicating the relative order of the versions.</returns>
        public int Compare(SemVersion version1, SemVersion version2)
        {
            return version1.CompareTo(version2);
        }

        /// <summary>
        /// Determines whether a version satisfies a version range.
        /// </summary>
        /// <param name="version">The version to check.</param>
        /// <param name="versionRange">The version range to check against.</param>
        /// <returns>True if the version satisfies the range; otherwise, false.</returns>
        public bool Satisfies(SemVersion version, string versionRange)
        {
            if (string.IsNullOrWhiteSpace(versionRange))
                return true;

            versionRange = versionRange.Trim();

            // Handle exact version
            if (!versionRange.StartsWith(">=") && !versionRange.StartsWith("<=") && 
                !versionRange.StartsWith(">") && !versionRange.StartsWith("<") &&
                !versionRange.StartsWith("^") && !versionRange.StartsWith("~"))
            {
                if (SemVersion.TryParse(versionRange, out var exactVersion))
                    return version.Equals(exactVersion);
                return false;
            }

            // Handle >= operator
            if (versionRange.StartsWith(">="))
            {
                var rangeVer = versionRange.Substring(2).Trim();
                if (SemVersion.TryParse(rangeVer, out var minVersion))
                    return version >= minVersion;
                return false;
            }

            // Handle > operator
            if (versionRange.StartsWith(">"))
            {
                var rangeVer = versionRange.Substring(1).Trim();
                if (SemVersion.TryParse(rangeVer, out var minVersion))
                    return version > minVersion;
                return false;
            }

            // Handle <= operator
            if (versionRange.StartsWith("<="))
            {
                var rangeVer = versionRange.Substring(2).Trim();
                if (SemVersion.TryParse(rangeVer, out var maxVersion))
                    return version <= maxVersion;
                return false;
            }

            // Handle < operator
            if (versionRange.StartsWith("<"))
            {
                var rangeVer = versionRange.Substring(1).Trim();
                if (SemVersion.TryParse(rangeVer, out var maxVersion))
                    return version < maxVersion;
                return false;
            }

            // Handle ^ (caret) - compatible with version (same major version for >=1.0.0)
            if (versionRange.StartsWith("^"))
            {
                var rangeVer = versionRange.Substring(1).Trim();
                if (SemVersion.TryParse(rangeVer, out var baseVersion))
                {
                    if (baseVersion.Major == 0)
                    {
                        // For 0.x.y, only minor and patch must match exactly or be greater
                        return version.Major == 0 && version.Minor == baseVersion.Minor && version >= baseVersion;
                    }
                    return version.Major == baseVersion.Major && version >= baseVersion;
                }
                return false;
            }

            // Handle ~ (tilde) - approximately equivalent (same major.minor version)
            if (versionRange.StartsWith("~"))
            {
                var rangeVer = versionRange.Substring(1).Trim();
                if (SemVersion.TryParse(rangeVer, out var baseVersion))
                {
                    return version.Major == baseVersion.Major && 
                           version.Minor == baseVersion.Minor && 
                           version >= baseVersion;
                }
                return false;
            }

            return false;
        }

        /// <summary>
        /// Determines whether two versions are equal.
        /// </summary>
        /// <param name="version1">The first version to compare.</param>
        /// <param name="version2">The second version to compare.</param>
        /// <returns>True if the versions are equal; otherwise, false.</returns>
        public bool Equals(SemVersion version1, SemVersion version2)
        {
            return version1.Equals(version2);
        }
    }
}
