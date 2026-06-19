using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GeneralUpdate.Extension.Utilities;

/// <summary>
/// SemVer 2.0 utility — validation, parsing, comparison, normalization, and equality.
/// Aligned with GeneralUpdate.Infrastructure.Common/Utilitys/Semver.cs.
/// </summary>
public static class Semver
{
    private static readonly Regex SemverRegex = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)" +
        @"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?" +
        @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Legacy4PartRegex = new(
        @"^(\d+)\.(\d+)\.(\d+)\.(\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns true when <paramref name="version"/> is a valid SemVer 2.0 string
    /// (or a legacy 4-part version that can be normalized to SemVer).
    /// </summary>
    public static bool IsValid(string? version)
    {
        if (version == null) return false;
        var trimmed = version.Trim();
        if (trimmed.Length == 0) return false;
        if (SysNumOverflow(trimmed)) return false;
        if (Legacy4PartRegex.IsMatch(trimmed)) return true;
        if (!SemverRegex.IsMatch(trimmed)) return false;
        return !HasLeadingZeroPreRelease(trimmed);
    }

    /// <summary>
    /// Tries to parse <paramref name="version"/> into a <see cref="SemVersion"/>.
    /// Legacy 4-part versions (e.g. "1.0.0.0") are normalized on input.
    /// </summary>
    public static bool TryParse(string? version, out SemVersion result)
    {
        result = default;
        if (version == null) return false;
        var trimmed = version.Trim();
        if (trimmed.Length == 0) return false;
        if (SysNumOverflow(trimmed)) return false;

        if (Legacy4PartRegex.IsMatch(trimmed))
        {
            var normalized = Normalize(trimmed);
            if (normalized == null) return false;
            return TryParseCore(normalized, out result);
        }

        if (!SemverRegex.IsMatch(trimmed)) return false;
        if (HasLeadingZeroPreRelease(trimmed)) return false;

        return TryParseCore(trimmed, out result);
    }

    /// <summary>
    /// Compares two version strings per SemVer 2.0 ordering rules.
    /// Null/empty/invalid strings compare as -1 or 1 as appropriate.
    /// </summary>
    public static int Compare(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return 0;
        if (string.IsNullOrWhiteSpace(a)) return -1;
        if (string.IsNullOrWhiteSpace(b)) return 1;

        if (!TryParse(a, out var parsedA) || !TryParse(b, out var parsedB)) return 0;

        if (parsedA.Major != parsedB.Major) return parsedA.Major.CompareTo(parsedB.Major);
        if (parsedA.Minor != parsedB.Minor) return parsedA.Minor.CompareTo(parsedB.Minor);
        if (parsedA.Patch != parsedB.Patch) return parsedA.Patch.CompareTo(parsedB.Patch);

        var aHasPre = !string.IsNullOrEmpty(parsedA.PreRelease);
        var bHasPre = !string.IsNullOrEmpty(parsedB.PreRelease);
        if (aHasPre != bHasPre) return aHasPre ? -1 : 1;
        if (!aHasPre) return 0;

        return ComparePreReleaseIdentifiers(parsedA.PreRelease, parsedB.PreRelease);
    }

    /// <summary>
    /// Equality comparison ignoring build metadata (per SemVer 2.0 spec).
    /// Returns false when either input is not a valid version.
    /// </summary>
    public static bool Equals(string? a, string? b)
    {
        if (!IsValid(a) || !IsValid(b)) return false;
        return Compare(a, b) == 0;
    }

    /// <summary>
    /// Normalize a version string to canonical SemVer 2.0 format.
    /// - "1.0.0.0" -> "1.0.0"
    /// - "1.0.0-alpha+build" -> "1.0.0-alpha"
    /// - Unparseable/whitespace -> null
    /// </summary>
    public static string? Normalize(string? version)
    {
        if (version == null) return null;
        var trimmed = version.Trim();
        if (trimmed.Length == 0) return null;
        if (SysNumOverflow(trimmed)) return null;

        var match4 = Legacy4PartRegex.Match(trimmed);
        if (match4.Success)
        {
            if (!int.TryParse(match4.Groups[1].Value, out var m1)) return null;
            if (!int.TryParse(match4.Groups[2].Value, out var m2)) return null;
            if (!int.TryParse(match4.Groups[3].Value, out var m3)) return null;
            return $"{m1}.{m2}.{m3}";
        }

        if (!SemverRegex.IsMatch(trimmed)) return null;
        if (HasLeadingZeroPreRelease(trimmed)) return null;

        var match = SemverRegex.Match(trimmed);
        if (!match.Success) return null;

        if (!int.TryParse(match.Groups[1].Value, out var major)) return null;
        if (!int.TryParse(match.Groups[2].Value, out var minor)) return null;
        if (!int.TryParse(match.Groups[3].Value, out var patch)) return null;
        var preRelease = match.Groups[4].Success ? match.Groups[4].Value : string.Empty;

        return $"{major}.{minor}.{patch}{preRelease}";
    }

    private static bool TryParseCore(string version, out SemVersion result)
    {
        result = default;
        var match = SemverRegex.Match(version);
        if (!match.Success) return false;

        if (!int.TryParse(match.Groups[1].Value, out var major)) return false;
        if (!int.TryParse(match.Groups[2].Value, out var minor)) return false;
        if (!int.TryParse(match.Groups[3].Value, out var patch)) return false;
        var preRelease = match.Groups[4].Success ? match.Groups[4].Value.TrimStart('-') : string.Empty;
        var build = match.Groups[5].Success ? match.Groups[5].Value.TrimStart('+') : string.Empty;

        result = new SemVersion(major, minor, patch, preRelease, build);
        return true;
    }

    private static bool HasLeadingZeroPreRelease(string input)
    {
        var dashIdx = input.IndexOf('-');
        if (dashIdx < 0) return false;

        var pre = dashIdx + 1 < input.Length ? input[(dashIdx + 1)..] : string.Empty;
        var plusIdx = pre.IndexOf('+');
        if (plusIdx >= 0) pre = pre[..plusIdx];

        if (pre.Length == 0) return false;

        var parts = pre.Split('.');
        foreach (var part in parts)
        {
            if (part.Length > 1 && part[0] == '0' && part.All(c => c >= '0' && c <= '9'))
                return true;
        }
        return false;
    }

    private static bool SysNumOverflow(string version)
    {
        var tokens = version.Split('.', '-', '+');
        foreach (var token in tokens)
        {
            if (token.Length == 0) continue;
            if (token.All(c => c >= '0' && c <= '9'))
            {
                if (token.Length > 10) return true;
                if (token.Length == 10 && token.CompareTo("2147483647") > 0) return true;
            }
        }
        return false;
    }

    private static int ComparePreReleaseIdentifiers(string preA, string preB)
    {
        var idsA = preA.Split('.');
        var idsB = preB.Split('.');
        var minLen = Math.Min(idsA.Length, idsB.Length);

        for (var i = 0; i < minLen; i++)
        {
            var result = ComparePreReleaseId(idsA[i], idsB[i]);
            if (result != 0) return result;
        }

        return idsA.Length.CompareTo(idsB.Length);
    }

    private static int ComparePreReleaseId(string idA, string idB)
    {
        var aIsNumeric = long.TryParse(idA, out var numA);
        var bIsNumeric = long.TryParse(idB, out var numB);

        if (aIsNumeric && bIsNumeric) return numA.CompareTo(numB);
        if (aIsNumeric) return -1;
        if (bIsNumeric) return 1;
        return string.Compare(idA, idB, StringComparison.Ordinal);
    }
}

/// <summary>
/// Represents a parsed SemVer 2.0 version with value equality and comparison operators.
/// Immutable value type — safe for use as a sorting/comparison key.
/// </summary>
public readonly struct SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
{
    /// <summary>Major version component (MAJOR in MAJOR.MINOR.PATCH).</summary>
    public int Major { get; }

    /// <summary>Minor version component (MINOR in MAJOR.MINOR.PATCH).</summary>
    public int Minor { get; }

    /// <summary>Patch version component (PATCH in MAJOR.MINOR.PATCH).</summary>
    public int Patch { get; }

    /// <summary>Pre-release identifier (e.g., "beta.1"), or <see cref="string.Empty"/>.</summary>
    public string PreRelease { get; }

    /// <summary>Build metadata identifier (e.g., "sha.abc"), or <see cref="string.Empty"/>.</summary>
    public string Build { get; }

    /// <summary>
    /// Initializes a new <see cref="SemVersion"/>.
    /// All parameters should be validated before construction — use <see cref="Semver.TryParse"/>.
    /// </summary>
    internal SemVersion(int major, int minor, int patch, string preRelease, string build)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease ?? string.Empty;
        Build = build ?? string.Empty;
    }

    /// <summary>
    /// Returns the canonical SemVer 2.0 string representation (e.g., "1.0.0", "1.0.0-beta.1").
    /// Build metadata is not included by default (per spec).
    /// </summary>
    public override string ToString()
    {
        var pre = string.IsNullOrEmpty(PreRelease) ? string.Empty : $"-{PreRelease}";
        return $"{Major}.{Minor}.{Patch}{pre}";
    }

    #region IComparable

    public int CompareTo(SemVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        if (Patch != other.Patch) return Patch.CompareTo(other.Patch);

        var aHasPre = !string.IsNullOrEmpty(PreRelease);
        var bHasPre = !string.IsNullOrEmpty(other.PreRelease);
        if (aHasPre != bHasPre) return aHasPre ? -1 : 1;
        if (!aHasPre) return 0;

        return ComparePreReleaseIdentifiers(PreRelease, other.PreRelease);
    }

    #endregion

    #region IEquatable

    public bool Equals(SemVersion other)
    {
        return Major == other.Major
            && Minor == other.Minor
            && Patch == other.Patch
            && string.Equals(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is SemVersion other && Equals(other);
    }

    public override int GetHashCode()
    {
        // Manual hash (avoids HashCode.Combine for netstandard2.0 compatibility).
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

    #endregion

    #region Operators

    public static bool operator >(SemVersion left, SemVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(SemVersion left, SemVersion right) => left.CompareTo(right) < 0;
    public static bool operator >=(SemVersion left, SemVersion right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SemVersion left, SemVersion right) => left.CompareTo(right) <= 0;
    public static bool operator ==(SemVersion left, SemVersion right) => left.Equals(right);
    public static bool operator !=(SemVersion left, SemVersion right) => !left.Equals(right);

    #endregion

    private static int ComparePreReleaseIdentifiers(string preA, string preB)
    {
        var idsA = preA.Split('.');
        var idsB = preB.Split('.');
        var minLen = Math.Min(idsA.Length, idsB.Length);

        for (var i = 0; i < minLen; i++)
        {
            var result = ComparePreReleaseId(idsA[i], idsB[i]);
            if (result != 0) return result;
        }

        return idsA.Length.CompareTo(idsB.Length);
    }

    private static int ComparePreReleaseId(string idA, string idB)
    {
        var aIsNumeric = long.TryParse(idA, out var numA);
        var bIsNumeric = long.TryParse(idB, out var numB);

        if (aIsNumeric && bIsNumeric) return numA.CompareTo(numB);
        if (aIsNumeric) return -1;
        if (bIsNumeric) return 1;
        return string.Compare(idA, idB, StringComparison.Ordinal);
    }
}

/// <summary>
/// <see cref="IComparer{T}"/> for sorting SemVer strings, delegates to <see cref="Semver.Compare"/>.
/// </summary>
public sealed class SemverComparer : IComparer<string?>
{
    public static readonly SemverComparer Instance = new();

    public int Compare(string? x, string? y) => Semver.Compare(x, y);
}
