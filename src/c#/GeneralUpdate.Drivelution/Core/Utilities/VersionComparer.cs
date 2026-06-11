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

        if (!TryParseNumeric(match.Groups["major"].Value, out var major))
            throw new FormatException($"Version '{version}' has a 'major' component that exceeds the supported range.");
        if (!TryParseNumeric(match.Groups["minor"].Value, out var minor))
            throw new FormatException($"Version '{version}' has a 'minor' component that exceeds the supported range.");
        if (!TryParseNumeric(match.Groups["patch"].Value, out var patch))
            throw new FormatException($"Version '{version}' has a 'patch' component that exceeds the supported range.");

        return new SemVerInfo
        {
            Major = major,
            Minor = minor,
            Patch = patch,
            Prerelease = match.Groups["prerelease"].Value,
            BuildMetadata = match.Groups["buildmetadata"].Value
        };
    }

    private static bool TryParseNumeric(string value, out long result)
    {
        // SemVer numeric components are non-negative integers per spec.
        // long.MaxValue (≈9.2e18) is the largest we support.
        if (value.Length > 19) // any value > long.MaxValue has at least 19 digits
        {
            result = 0;
            return false;
        }
        return long.TryParse(value, out result);
    }

    private static int ComparePrerelease(string pre1, string pre2)
    {
        var parts1 = pre1.Split('.');
        var parts2 = pre2.Split('.');

        int minLength = Math.Min(parts1.Length, parts2.Length);

        for (int i = 0; i < minLength; i++)
        {
            // Test numeric identifiers — if both parse, compare numerically.
            // Per SemVer 2.0 §11, numeric prerelease identifiers are compared
            // as integers.  If an identifier exceeds long.MaxValue, fall back
            // to a digit-length + ordinal comparison so the ordering remains
            // integer-like even when the platform type can't hold the value.
            bool isNum1 = TryParseNumeric(parts1[i], out long num1);
            bool isNum2 = TryParseNumeric(parts2[i], out long num2);

            if (isNum1 && isNum2)
            {
                if (num1 != num2)
                    return num1.CompareTo(num2);
            }
            else if (IsNumericString(parts1[i]) && IsNumericString(parts2[i]))
            {
                // Both are purely numeric but exceed long.MaxValue.
                // Compare by digit length first, then ordinal.
                int cmp = parts1[i].Length.CompareTo(parts2[i].Length);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(parts1[i], parts2[i]);
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

    /// <summary>
    /// Returns true when <paramref name="s"/> is non-empty and every character
    /// is an ASCII digit — without attempting to parse into a numeric type.
    /// </summary>
    private static bool IsNumericString(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
            if (c < '0' || c > '9')
                return false;
        return true;
    }

    private class SemVerInfo
    {
        public long Major { get; set; }
        public long Minor { get; set; }
        public long Patch { get; set; }
        public string Prerelease { get; set; } = string.Empty;
        public string BuildMetadata { get; set; } = string.Empty;
    }
}
