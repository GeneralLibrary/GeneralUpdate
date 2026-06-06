using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Core.Configuration;
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
    /// Determines whether an update is needed for the specified <see cref="AppType"/>.
    /// Takes the maximum server-side version for the given AppType (excluding frozen
    /// packages) and compares it once against the local manifest version.
    /// </summary>
    /// <param name="assets">All assets returned by the download source.</param>
    /// <param name="appType">The AppType to check (<see cref="AppType.Client"/> or <see cref="AppType.Upgrade"/>).</param>
    /// <param name="localVersion">
    /// The local version from <c>generalupdate.manifest.json</c>:
    /// <see cref="UpdateConfiguration.ClientVersion"/> for Client,
    /// <see cref="UpdateConfiguration.UpgradeClientVersion"/> for Upgrade.
    /// When null or unparseable, returns <c>true</c> (safe: can't prove up-to-date → proceed with update).
    /// </param>
    /// <returns><c>true</c> when the max server version is strictly greater than the local version,
    /// or when the local version cannot be determined.</returns>
    public static bool HasUpdate(
        IEnumerable<DownloadAsset> assets,
        AppType appType,
        string? localVersion)
    {
        if (assets == null)
            return false;

        // Fast path: empty collection — no need to enumerate
        if (assets is ICollection<DownloadAsset> { Count: 0 })
            return false;

        // Collect server versions for the target AppType (exclude frozen packages)
        var serverVersions = assets
            .Where(a => !a.IsFreeze)
            .Where(a => (a.AppType ?? (int)AppType.Client) == (int)appType)
            .Select(a => ParseVersion(a.Version))
            .Where(v => v != null)
            .ToList();

        if (serverVersions.Count == 0)
            return false;

        // If the local version cannot be read or parsed, we can't prove we're
        // up to date — err on the side of updating rather than silently skipping.
        if (string.IsNullOrWhiteSpace(localVersion)
            || !Version.TryParse(localVersion, out var local))
            return true;

        // Compare: max server version > local version?
        return serverVersions.Max()! > local;
    }

    /// <summary>
    /// Builds a download plan with AppType-aware version filtering.
    /// Client-type assets are compared against <paramref name="clientVersion"/>.
    /// Upgrade-type assets are compared against <paramref name="upgradeClientVersion"/>
    /// (falling back to <paramref name="clientVersion"/> when null/unparseable).
    /// </summary>
    /// <param name="assets">The list of assets retrieved from the download source.</param>
    /// <param name="clientVersion">The current client (main app) version.</param>
    /// <param name="upgradeClientVersion">
    /// The current upgrade (updater) version, or null to fall back to <paramref name="clientVersion"/>.
    /// </param>
    public static DownloadPlan Build(
        IEnumerable<DownloadAsset> assets,
        string clientVersion,
        string? upgradeClientVersion)
    {
        if (assets == null) return DownloadPlan.Empty;
        var parsedClient = ParseVersion(clientVersion);
        if (parsedClient == null) return DownloadPlan.Empty;

        var parsedUpgrade = ParseVersion(upgradeClientVersion) ?? parsedClient;

        // 1. Filter out frozen packages
        var active = assets
            .Where(a => !a.IsFreeze)
            .ToList();

        if (active.Count == 0) return DownloadPlan.Empty;

        // 2. Check for forced update
        var isForcibly = active.Any(a => a.IsForcibly);

        // 3. AppType-aware version filtering: keep only packages whose version
        //    is strictly greater than the local version for that AppType.
        var candidates = active
            .Where(a =>
            {
                var pv = ParseVersion(a.Version);
                if (pv == null) return false;

                var localVersion = (a.AppType == (int)AppType.Upgrade)
                    ? parsedUpgrade
                    : parsedClient;

                return pv > localVersion;
            })
            .Where(a => IsCompatible(a.MinClientVersion, clientVersion))
            .OrderBy(a => ParseVersion(a.Version))
            .ToList();

        if (candidates.Count == 0) return DownloadPlan.Empty;

        // ── CVP-first selection ──
        // If a matching cross-version package (CVP) exists whose FromVersion
        // equals the client's current version, prefer it over chain packages.
        // This gives the client a single-package shortcut from old → latest.
        var matchingCvp = candidates
            .Where(a => a.IsCrossVersion)
            .FirstOrDefault(a =>
            {
                var fromVer = ParseVersion(a.FromVersion);
                return fromVer != null && fromVer == parsedClient;
            });

        if (matchingCvp != null)
        {
            // CVP covers one AppType in a single hop. Still need chain packages
            // for other AppTypes, and for the same AppType beyond the CVP's target.
            var cvpAppType = matchingCvp.AppType;
            var cvpVersion = ParseVersion(matchingCvp.Version);
            var planAssets = new List<DownloadAsset> { matchingCvp };
            planAssets.AddRange(candidates
                .Where(a => !a.IsCrossVersion)
                .Where(a => a.AppType != cvpAppType
                            || (cvpVersion != null && ParseVersion(a.Version) > cvpVersion))
                .OrderBy(a => ParseVersion(a.Version)));
            return new DownloadPlan(planAssets, isForcibly);
        }

        // No matching CVP: return all chain packages sorted by version (ascending)
        return new DownloadPlan(candidates, isForcibly);
    }

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
    public static DownloadPlan Build(IEnumerable<DownloadAsset> assets, string currentVersion)
        => Build(assets, currentVersion, upgradeClientVersion: null);

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
