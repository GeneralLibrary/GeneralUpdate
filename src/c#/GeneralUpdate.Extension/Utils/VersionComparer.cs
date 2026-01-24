using System;
using System.Text.RegularExpressions;

namespace GeneralUpdate.Extension.Utils
{
    /// <summary>
    /// Provides semantic version comparison utilities for plugin versioning.
    /// Supports semantic versioning format (MAJOR.MINOR.PATCH[-prerelease][+metadata]).
    /// </summary>
    public static class VersionComparer
    {
        /// <summary>
        /// Compares two version strings using semantic versioning rules.
        /// </summary>
        /// <param name="version1">First version string.</param>
        /// <param name="version2">Second version string.</param>
        /// <returns>
        /// Less than 0 if version1 &lt; version2,
        /// 0 if version1 == version2,
        /// Greater than 0 if version1 &gt; version2.
        /// </returns>
        public static int Compare(string version1, string version2)
        {
            if (string.IsNullOrWhiteSpace(version1))
                throw new ArgumentException("Version1 cannot be null or empty.", nameof(version1));
            if (string.IsNullOrWhiteSpace(version2))
                throw new ArgumentException("Version2 cannot be null or empty.", nameof(version2));

            var v1 = ParseVersion(version1);
            var v2 = ParseVersion(version2);

            // Compare major version
            int majorCompare = v1.Major.CompareTo(v2.Major);
            if (majorCompare != 0) return majorCompare;

            // Compare minor version
            int minorCompare = v1.Minor.CompareTo(v2.Minor);
            if (minorCompare != 0) return minorCompare;

            // Compare patch version
            int patchCompare = v1.Patch.CompareTo(v2.Patch);
            if (patchCompare != 0) return patchCompare;

            // If one has prerelease and other doesn't, stable version is greater
            if (string.IsNullOrEmpty(v1.Prerelease) && !string.IsNullOrEmpty(v2.Prerelease))
                return 1;
            if (!string.IsNullOrEmpty(v1.Prerelease) && string.IsNullOrEmpty(v2.Prerelease))
                return -1;

            // Compare prerelease identifiers
            if (!string.IsNullOrEmpty(v1.Prerelease) && !string.IsNullOrEmpty(v2.Prerelease))
            {
                return string.Compare(v1.Prerelease, v2.Prerelease, StringComparison.OrdinalIgnoreCase);
            }

            return 0;
        }

        /// <summary>
        /// Checks if version1 is greater than version2.
        /// </summary>
        public static bool IsGreaterThan(string version1, string version2)
        {
            return Compare(version1, version2) > 0;
        }

        /// <summary>
        /// Checks if version1 is less than version2.
        /// </summary>
        public static bool IsLessThan(string version1, string version2)
        {
            return Compare(version1, version2) < 0;
        }

        /// <summary>
        /// Checks if version1 equals version2.
        /// </summary>
        public static bool IsEqual(string version1, string version2)
        {
            return Compare(version1, version2) == 0;
        }

        /// <summary>
        /// Checks if an update is available (newVersion > currentVersion).
        /// </summary>
        public static bool IsUpdateAvailable(string currentVersion, string newVersion)
        {
            try
            {
                return IsGreaterThan(newVersion, currentVersion);
            }
            catch
            {
                return false;
            }
        }

        private static SemanticVersion ParseVersion(string version)
        {
            // Remove leading 'v' if present
            version = version.TrimStart('v', 'V');

            // Regex pattern for semantic versioning
            var pattern = @"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-\.]+))?(?:\+([0-9A-Za-z-\.]+))?$";
            var match = Regex.Match(version, pattern);

            if (!match.Success)
            {
                // Try simplified version format (e.g., "1.0" or "1.2.3.4")
                var parts = version.Split('.');
                if (parts.Length >= 2)
                {
                    if (int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                    {
                        int patch = 0;
                        if (parts.Length >= 3)
                        {
                            int.TryParse(parts[2], out patch);
                        }
                        return new SemanticVersion(major, minor, patch, null, null);
                    }
                }
                throw new FormatException($"Invalid version format: {version}");
            }

            return new SemanticVersion(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                match.Groups[4].Success ? match.Groups[4].Value : null,
                match.Groups[5].Success ? match.Groups[5].Value : null
            );
        }

        private class SemanticVersion
        {
            public int Major { get; }
            public int Minor { get; }
            public int Patch { get; }
            public string Prerelease { get; }
            public string Metadata { get; }

            public SemanticVersion(int major, int minor, int patch, string prerelease, string metadata)
            {
                Major = major;
                Minor = minor;
                Patch = patch;
                Prerelease = prerelease;
                Metadata = metadata;
            }
        }
    }
}
