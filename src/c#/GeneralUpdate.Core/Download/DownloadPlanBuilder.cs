using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download;

/// <summary>
/// Builds a <see cref="DownloadPlan"/> by filtering and ordering download assets
/// based on version compatibility, frozen-package filtering, forced-update detection,
/// and MinClientVersion compatibility checks.
/// </summary>
/// <remarks>
/// <para>
/// This static utility class constructs a download plan from server-returned assets
/// based on the current client version. The build process follows these rules:
/// </para>
/// <list type="bullet">
///   <item><term>Frozen package filtering</term><description>Skips packages marked as frozen (<c>IsFreeze = true</c>),
///         as they should not participate in the update.</description></item>
///   <item><term>Forced update detection</term><description>Detects if any asset has the forced-update flag
///         (<c>IsForcibly = true</c>), in which case the entire plan is marked as forced.</description></item>
///   <item><term>Version filtering and sorting</term><description>Retains only packages with versions higher
///         than the current client version, sorted in ascending order (lowest to highest).</description></item>
///   <item><term>Compatibility check</term><description>Checks each package's <c>MinClientVersion</c> requirement;
///         if the current client version is below the minimum, the package is skipped.</description></item>
/// </list>
/// <para>
/// Note: This builder does not distinguish between cross-version and in-order updates;
/// each package carries its own <c>IsCrossVersion</c> metadata for downstream processing.
/// </para>
/// </remarks>
public static class DownloadPlanBuilder
{
    /// <summary>
    /// Builds a download plan from a list of download assets, filtering and ordering
    /// based on the current client version.
    /// </summary>
    /// <param name="assets">The list of assets retrieved from the download source.</param>
    /// <param name="currentVersion">The current client version string.</param>
    /// <returns>
    /// A <see cref="DownloadPlan"/> containing ordered assets suitable for download.
    /// Returns <see cref="DownloadPlan.Empty"/> if no update is needed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Build process:
    /// </para>
    /// <list type="number">
    ///   <item>Validates input: if assets is null or the current version cannot be parsed, returns an empty plan.</item>
    ///   <item>Filters out frozen packages: removes assets with <c>IsFreeze = true</c>.</item>
    ///   <item>Checks for forced updates: if any asset is marked as forced, the entire plan is marked as forced.</item>
    ///   <item>Version filtering: retains only assets with versions higher than the current version.</item>
    ///   <item>Compatibility check: ensures each asset's <c>MinClientVersion</c> is compatible with the current version.</item>
    ///   <item>Ascending sort: orders assets by version number from lowest to highest.</item>
    /// </list>
    /// <para>
    /// If no assets match the criteria, returns <c>DownloadPlan.Empty</c>.
    /// </para>
    /// </remarks>
    public static DownloadPlan Build(IEnumerable<DownloadAsset> assets, string currentVersion)
    {
        if (assets == null) return DownloadPlan.Empty;
        if (ParseVersion(currentVersion) == null) return DownloadPlan.Empty;

        // 1. Filter out frozen packages
        var active = assets
            .Where(a => !a.IsFreeze)
            .ToList();

        if (active.Count == 0) return DownloadPlan.Empty;

        // 2. Check for forced update
        var isForcibly = active.Any(a => a.IsForcibly);

        // 3. Filter and sort: keep only packages higher than current version,
        //    respecting MinClientVersion compatibility.
        var candidates = active
            .Where(a =>
            {
                var pv = ParseVersion(a.Version);
                if (pv == null) return false;
                return pv > ParseVersion(currentVersion);
            })
            .Where(a => IsCompatible(a.MinClientVersion, currentVersion))
            .OrderBy(a => ParseVersion(a.Version))
            .ToList();

        if (candidates.Count == 0) return DownloadPlan.Empty;

        return new DownloadPlan(candidates, isForcibly);
    }

    /// <summary>
    /// Checks whether the specified MinClientVersion is compatible with the current client version.
    /// If a package's MinClientVersion is higher than the current version, the package is not applicable.
    /// </summary>
    /// <param name="minClientVersion">The minimum client version required by the package. If null or empty, the package is considered compatible.</param>
    /// <param name="currentVersion">The current client version string.</param>
    /// <returns>True if the current version meets or exceeds the minimum requirement; otherwise false.</returns>
    internal static bool IsCompatible(string? minClientVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(minClientVersion)) return true;
        var min = ParseVersion(minClientVersion);
        var cur = ParseVersion(currentVersion);
        if (min == null || cur == null) return true;
        return cur >= min;
    }

    /// <summary>Parses a version string and returns null if the string cannot be parsed.</summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>A parsed <see cref="Version"/> object, or null if parsing fails.</returns>
    internal static Version? ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        return Version.TryParse(version, out var v) ? v : null;
    }
}
