using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download;

/// <summary>
/// Builds a DownloadPlan from download assets.
/// Handles cross-version package selection, version chain building,
/// frozen package filtering, and forced update marking.
/// </summary>
public static class DownloadPlanBuilder
{
    /// <summary>
    /// Build a download plan from a list of download assets.
    /// </summary>
    /// <param name="assets">Assets from the download source.</param>
    /// <param name="currentVersion">Current client version string.</param>
    /// <returns>A DownloadPlan with ordered assets, or DownloadPlan.Empty if no update is needed.</returns>
    public static DownloadPlan Build(IEnumerable<DownloadAsset> assets, string currentVersion)
    {
        if (assets == null) return DownloadPlan.Empty;

        // 1. Filter out frozen packages
        var active = assets
            .Where(a => !a.IsFreeze)
            .ToList();

        if (active.Count == 0) return DownloadPlan.Empty;

        // 2. Check for forced update
        var isForcibly = active.Any(a => a.IsForcibly);

        // 3. Look for a cross-version package that matches our current version
        var crossVersion = active
            .Where(a => a.IsCrossVersion
                     && !string.IsNullOrEmpty(a.FromVersion)
                     && VersionEquals(a.FromVersion!, currentVersion))
            .OrderByDescending(a => ParseVersion(a.Version))
            .FirstOrDefault();

        if (crossVersion != null)
        {
            // Single download — jump directly to target version
            return new DownloadPlan(new[] { crossVersion }, isForcibly);
        }

        // 4. Build version chain from non-cross-version packages
        var chain = BuildVersionChain(active.Where(a => !a.IsCrossVersion), currentVersion);
        if (chain.Count == 0) return DownloadPlan.Empty;

        return new DownloadPlan(chain, isForcibly);
    }

    /// <summary>
    /// Build a version chain: keep versions higher than current,
    /// check MinClientVersion compatibility.
    /// </summary>
    private static List<DownloadAsset> BuildVersionChain(IEnumerable<DownloadAsset> assets, string currentVersion)
    {
        var current = ParseVersion(currentVersion);

        return assets
            .Where(a =>
            {
                var pv = ParseVersion(a.Version);
                if (pv == null) return false;
                return pv > current;
            })
            .Where(a => IsCompatible(a.MinClientVersion, currentVersion))
            .OrderBy(a => ParseVersion(a.Version))
            .ToList();
    }

    /// <summary>
    /// Check if MinClientVersion is compatible with the current version.
    /// A package with MinClientVersion higher than current is not applicable.
    /// </summary>
    private static bool IsCompatible(string? minClientVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(minClientVersion)) return true;
        var min = ParseVersion(minClientVersion);
        var cur = ParseVersion(currentVersion);
        if (min == null || cur == null) return true;
        return cur >= min;
    }

    /// <summary>Parse a version string, returning null on failure.</summary>
    private static Version? ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        return Version.TryParse(version, out var v) ? v : null;
    }

    /// <summary>Compare two version strings for equality.</summary>
    private static bool VersionEquals(string a, string b)
    {
        var va = ParseVersion(a);
        var vb = ParseVersion(b);
        return va != null && vb != null && va == vb;
    }
}
