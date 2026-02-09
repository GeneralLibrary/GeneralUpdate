using System.Text.RegularExpressions;

namespace GeneralUpdate.Drivelution.Core.Utilities;

/// <summary>
/// 版本比较工具类（遵循SemVer 2.0规范）
/// Version comparison utility (follows SemVer 2.0 specification)
/// </summary>
public static class VersionComparer
{
    private static readonly Regex SemVerRegex = new(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
        RegexOptions.Compiled);

    /// <summary>
    /// 比较两个版本号
    /// Compares two version numbers
    /// </summary>
    /// <param name="version1">版本1 / Version 1</param>
    /// <param name="version2">版本2 / Version 2</param>
    /// <returns>
    /// 如果version1 > version2，返回1；
    /// 如果version1 = version2，返回0；
    /// 如果version1 < version2，返回-1
    /// Returns 1 if version1 > version2, 0 if equal, -1 if version1 < version2
    /// </returns>
    public static int Compare(string version1, string version2)
    {
        if (string.IsNullOrWhiteSpace(version1) || string.IsNullOrWhiteSpace(version2))
        {
            throw new ArgumentException("Version strings cannot be null or empty");
        }

        var v1 = ParseVersion(version1);
        var v2 = ParseVersion(version2);

        // Compare major, minor, patch
        if (v1.Major != v2.Major)
            return v1.Major.CompareTo(v2.Major);
        if (v1.Minor != v2.Minor)
            return v1.Minor.CompareTo(v2.Minor);
        if (v1.Patch != v2.Patch)
            return v1.Patch.CompareTo(v2.Patch);

        // If both have no prerelease, they are equal
        if (string.IsNullOrEmpty(v1.Prerelease) && string.IsNullOrEmpty(v2.Prerelease))
            return 0;

        // Version without prerelease is greater than version with prerelease
        if (string.IsNullOrEmpty(v1.Prerelease))
            return 1;
        if (string.IsNullOrEmpty(v2.Prerelease))
            return -1;

        // Compare prerelease identifiers
        return ComparePrerelease(v1.Prerelease, v2.Prerelease);
    }

    /// <summary>
    /// 判断version1是否大于version2
    /// Checks if version1 is greater than version2
    /// </summary>
    public static bool IsGreaterThan(string version1, string version2)
    {
        return Compare(version1, version2) > 0;
    }

    /// <summary>
    /// 判断version1是否小于version2
    /// Checks if version1 is less than version2
    /// </summary>
    public static bool IsLessThan(string version1, string version2)
    {
        return Compare(version1, version2) < 0;
    }

    /// <summary>
    /// 判断version1是否等于version2
    /// Checks if version1 equals version2
    /// </summary>
    public static bool IsEqual(string version1, string version2)
    {
        return Compare(version1, version2) == 0;
    }

    /// <summary>
    /// 验证版本号是否符合SemVer 2.0规范
    /// Validates if version string follows SemVer 2.0 specification
    /// </summary>
    public static bool IsValidSemVer(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        return SemVerRegex.IsMatch(version);
    }

    private static SemVerInfo ParseVersion(string version)
    {
        var match = SemVerRegex.Match(version);
        if (!match.Success)
        {
            throw new FormatException($"Version '{version}' does not follow SemVer 2.0 format");
        }

        return new SemVerInfo
        {
            Major = int.Parse(match.Groups["major"].Value),
            Minor = int.Parse(match.Groups["minor"].Value),
            Patch = int.Parse(match.Groups["patch"].Value),
            Prerelease = match.Groups["prerelease"].Value,
            BuildMetadata = match.Groups["buildmetadata"].Value
        };
    }

    private static int ComparePrerelease(string pre1, string pre2)
    {
        var parts1 = pre1.Split('.');
        var parts2 = pre2.Split('.');

        int minLength = Math.Min(parts1.Length, parts2.Length);

        for (int i = 0; i < minLength; i++)
        {
            var isNum1 = int.TryParse(parts1[i], out int num1);
            var isNum2 = int.TryParse(parts2[i], out int num2);

            if (isNum1 && isNum2)
            {
                if (num1 != num2)
                    return num1.CompareTo(num2);
            }
            else if (isNum1)
            {
                return -1; // Numeric identifier is less than alphanumeric
            }
            else if (isNum2)
            {
                return 1; // Alphanumeric is greater than numeric
            }
            else
            {
                int stringCompare = string.CompareOrdinal(parts1[i], parts2[i]);
                if (stringCompare != 0)
                    return stringCompare;
            }
        }

        // Longer prerelease is greater
        return parts1.Length.CompareTo(parts2.Length);
    }

    private class SemVerInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string Prerelease { get; set; } = string.Empty;
        public string BuildMetadata { get; set; } = string.Empty;
    }
}
