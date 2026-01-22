using System;

namespace MyApp.Extensions
{
    /// <summary>
    /// Represents a semantic version following the SemVer 2.0 specification.
    /// </summary>
    public struct SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
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
        /// Gets or sets the pre-release label (e.g., "alpha", "beta", "rc.1").
        /// </summary>
        public string PreRelease { get; set; }

        /// <summary>
        /// Gets or sets the build metadata.
        /// </summary>
        public string BuildMetadata { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SemVersion"/> struct.
        /// </summary>
        /// <param name="major">The major version number.</param>
        /// <param name="minor">The minor version number.</param>
        /// <param name="patch">The patch version number.</param>
        /// <param name="preRelease">The pre-release label.</param>
        /// <param name="buildMetadata">The build metadata.</param>
        public SemVersion(int major, int minor, int patch, string preRelease = null, string buildMetadata = null)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease;
            BuildMetadata = buildMetadata;
        }

        /// <summary>
        /// Returns the string representation of the semantic version.
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
        /// Parses a version string into a SemVersion instance.
        /// </summary>
        /// <param name="versionString">The version string to parse.</param>
        /// <returns>A SemVersion instance.</returns>
        public static SemVersion Parse(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                throw new ArgumentException("Version string cannot be null or empty.", nameof(versionString));

            if (!TryParse(versionString, out var version))
                throw new FormatException($"Invalid version string: {versionString}");

            return version;
        }

        /// <summary>
        /// Tries to parse a version string into a SemVersion instance.
        /// </summary>
        /// <param name="versionString">The version string to parse.</param>
        /// <param name="version">The parsed SemVersion instance.</param>
        /// <returns>True if parsing was successful; otherwise, false.</returns>
        public static bool TryParse(string versionString, out SemVersion version)
        {
            version = default;
            
            if (string.IsNullOrWhiteSpace(versionString))
                return false;

            // Split on + for build metadata
            var parts = versionString.Split('+');
            var versionPart = parts[0];
            var buildMetadata = parts.Length > 1 ? parts[1] : null;

            // Split on - for pre-release
            var coreParts = versionPart.Split(new[] { '-' }, 2);
            var coreVersion = coreParts[0];
            var preRelease = coreParts.Length > 1 ? coreParts[1] : null;

            // Parse major.minor.patch
            var versionNumbers = coreVersion.Split('.');
            if (versionNumbers.Length != 3)
                return false;

            if (!int.TryParse(versionNumbers[0], out var major) || major < 0)
                return false;
            if (!int.TryParse(versionNumbers[1], out var minor) || minor < 0)
                return false;
            if (!int.TryParse(versionNumbers[2], out var patch) || patch < 0)
                return false;

            version = new SemVersion(major, minor, patch, preRelease, buildMetadata);
            return true;
        }

        /// <summary>
        /// Compares this instance to another SemVersion instance.
        /// </summary>
        /// <param name="other">The other SemVersion instance to compare.</param>
        /// <returns>A value indicating the relative order of the instances.</returns>
        public int CompareTo(SemVersion other)
        {
            // Compare major.minor.patch
            var majorCompare = Major.CompareTo(other.Major);
            if (majorCompare != 0) return majorCompare;

            var minorCompare = Minor.CompareTo(other.Minor);
            if (minorCompare != 0) return minorCompare;

            var patchCompare = Patch.CompareTo(other.Patch);
            if (patchCompare != 0) return patchCompare;

            // Pre-release versions have lower precedence than normal versions
            if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
                return 1;
            if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
                return -1;
            if (!string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
            {
                return string.CompareOrdinal(PreRelease, other.PreRelease);
            }

            // Build metadata does not affect version precedence
            return 0;
        }

        /// <summary>
        /// Determines whether this instance is equal to another SemVersion instance.
        /// </summary>
        /// <param name="other">The other SemVersion instance to compare.</param>
        /// <returns>True if the instances are equal; otherwise, false.</returns>
        public bool Equals(SemVersion other)
        {
            return Major == other.Major &&
                   Minor == other.Minor &&
                   Patch == other.Patch &&
                   PreRelease == other.PreRelease;
            // Note: Build metadata is not included in equality per SemVer 2.0 spec
        }

        /// <summary>
        /// Determines whether this instance is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the objects are equal; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return obj is SemVersion other && Equals(other);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Major.GetHashCode();
                hash = hash * 31 + Minor.GetHashCode();
                hash = hash * 31 + Patch.GetHashCode();
                hash = hash * 31 + (PreRelease?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two SemVersion instances are equal.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True if the instances are equal; otherwise, false.</returns>
        public static bool operator ==(SemVersion left, SemVersion right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two SemVersion instances are not equal.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True if the instances are not equal; otherwise, false.</returns>
        public static bool operator !=(SemVersion left, SemVersion right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether one SemVersion instance is less than another.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True if the first instance is less than the second; otherwise, false.</returns>
        public static bool operator <(SemVersion left, SemVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        /// <summary>
        /// Determines whether one SemVersion instance is greater than another.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True if the first instance is greater than the second; otherwise, false.</returns>
        public static bool operator >(SemVersion left, SemVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        /// <summary>
        /// Determines whether one SemVersion instance is less than or equal to another.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True if the first instance is less than or equal to the second; otherwise, false.</returns>
        public static bool operator <=(SemVersion left, SemVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        /// <summary>
        /// Determines whether one SemVersion instance is greater than or equal to another.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True if the first instance is greater than or equal to the second; otherwise, false.</returns>
        public static bool operator >=(SemVersion left, SemVersion right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
