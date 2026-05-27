using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download;

/// <summary>
/// Builds a DownloadPlan from download assets.
/// Handles frozen package filtering, forced update marking, and MinClientVersion compatibility.
/// Does not distinguish between cross-version and version-chain — each package carries its own metadata.
/// </summary>
public static class DownloadPlanBuilder
{
    /// <summary>
    /// Build a download plan from a list of download assets.
    /// Does not distinguish between cross-version and version-chain packages —
    /// the server decides what to return, and each package carries its own
    /// <see cref="DownloadAsset.IsCrossVersion"/> for downstream handling.
    /// </summary>
    /// <param name="assets">Assets from the download source.</param>
    /// <param name="currentVersion">Current client version string.</param>
    /// <returns>A DownloadPlan with ordered assets, or DownloadPlan.Empty if no update is needed.</returns>
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
}
