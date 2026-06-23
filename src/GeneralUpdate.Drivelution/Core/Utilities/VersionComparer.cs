using System;

namespace GeneralUpdate.Drivelution.Core.Utilities;

/// <summary>
/// Version comparison utility (follows SemVer 2.0 specification).
/// Delegates to <see cref="Semver"/> for all operations.
/// </summary>
[Obsolete("Use GeneralUpdate.Drivelution.Core.Utilities.Semver directly instead.")]
public static class VersionComparer
{
    /// <summary>
    /// Compares two version numbers per SemVer 2.0.
    /// Returns 1 if v1 > v2, 0 if equal, -1 if v1 < v2.
    /// </summary>
    public static int Compare(string version1, string version2)
        => Semver.Compare(version1, version2);

    /// <summary>Returns true if version1 &gt; version2.</summary>
    public static bool IsGreaterThan(string version1, string version2)
        => Semver.Compare(version1, version2) > 0;

    /// <summary>Returns true if version1 &lt; version2.</summary>
    public static bool IsLessThan(string version1, string version2)
        => Semver.Compare(version1, version2) < 0;

    /// <summary>Returns true if version1 == version2.</summary>
    public static bool IsEqual(string version1, string version2)
        => Semver.Compare(version1, version2) == 0;

    /// <summary>
    /// Validates whether <paramref name="version"/> follows SemVer 2.0.
    /// Note: unlike the original implementation, this also accepts legacy
    /// 4-part versions (e.g. "1.0.0.0") for broader compatibility.
    /// </summary>
    public static bool IsValidSemVer(string version)
        => Semver.IsValid(version);
}
