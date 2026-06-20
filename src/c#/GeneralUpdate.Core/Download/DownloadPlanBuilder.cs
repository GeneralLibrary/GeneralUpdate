using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Utilities;

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
/// All packages are treated uniformly; the builder evaluates chain vs full packages
/// based on total download size.
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
            .Select(v => v!.Value)
            .ToList();

        if (serverVersions.Count == 0)
            return false;

        // If the local version cannot be read or parsed, we can't prove we're
        // up to date — err on the side of updating rather than silently skipping.
        if (string.IsNullOrWhiteSpace(localVersion)
            || !Semver.TryParse(localVersion, out var local))
            return true;

        // Compare: max server version > local version?
        return serverVersions.Max() > local;
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
        var cv = parsedClient.Value;

        var parsedUpgrade = ParseVersion(upgradeClientVersion) ?? parsedClient;
        var uv = parsedUpgrade.Value;

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
                if (!Semver.TryParse(a.Version, out var pv)) return false;

                var localVersion = (a.AppType == (int)AppType.Upgrade)
                    ? uv
                    : cv;

                return pv > localVersion;
            })
            .Where(a => IsCompatible(a.MinClientVersion, clientVersion))
            .OrderBy(a => { Semver.TryParse(a.Version, out var sv); return sv; })
            .ToList();

        if (candidates.Count == 0) return DownloadPlan.Empty;

        // Separate chain vs full packages
        var chainCandidates = candidates
            .Where(a => a.PackageType == (int)Configuration.PackageType.Chain)
            .ToList();

        var fullCandidates = candidates
            .Where(a => a.PackageType == (int)Configuration.PackageType.Full)
            .ToList();

        // ── Chain vs Full size-based decision ──
        // If a full replacement package is available and the total chain download
        // size approaches or exceeds the full package size, skip chain and use full.
        if (chainCandidates.Count > 0 && fullCandidates.Count > 0)
        {
            // Pick the latest full package (highest version) across all AppTypes
            var bestFull = fullCandidates
                .OrderByDescending(a => { Semver.TryParse(a.Version, out var sv); return sv; })
                .First();

            long chainTotal = chainCandidates.Sum(a => a.Size);
            var threshold = (long)(bestFull.Size * 0.8);

            if (chainTotal >= threshold)
            {
                // Chain is too expensive — use full package instead.
                // Supplement with chain packages for other AppTypes not covered by full.
                GeneralTracer.Info($"DownloadPlanBuilder: chain total {chainTotal} >= 80% of full size {bestFull.Size}, switching to full package {bestFull.Name}");
                var planAssets = new List<DownloadAsset> { bestFull };
                planAssets.AddRange(chainCandidates
                    .Where(a => a.AppType != bestFull.AppType
                                || (Semver.TryParse(a.Version, out var av)
                                    && Semver.TryParse(bestFull.Version, out var fv)
                                    && av > fv))
                    .OrderBy(a => { Semver.TryParse(a.Version, out var sv); return sv; }));
                return new DownloadPlan(planAssets, isForcibly);
            }
        }

        // ── Chain plan with fallback fulls ──
        // Use chain packages normally. Attach FallbackFull* info to each chain entry
        // so that if a chain patch fails, AbstractStrategy can fall back to full.
        if (fullCandidates.Count > 0)
        {
            var fallbackFulls = new List<DownloadAsset>();

            var chainWithFallback = chainCandidates
                .Select(chain =>
                {
                    // Find a matching full: same AppType + same Version (or closest)
                    var match = fullCandidates
                        .Where(f => f.AppType == chain.AppType)
                        .OrderBy(f => { Semver.TryParse(f.Version, out var sv); return sv; })
                        .FirstOrDefault(f =>
                        {
                            if (!Semver.TryParse(f.Version, out var fv)) return false;
                            if (!Semver.TryParse(chain.Version, out var cv)) return false;
                            return fv >= cv;
                        });

                    if (match != null)
                    {
                        // Add matching full to the fallback list once
                        if (!fallbackFulls.Any(f => f.Url == match.Url))
                            fallbackFulls.Add(match);

                        return chain with
                        {
                            FallbackFullName = match.Name,
                            FallbackFullUrl = match.Url,
                            FallbackFullHash = match.SHA256,
                            FallbackFullVersion = match.Version
                        };
                    }
                    return chain;
                })
                .ToList();

            return new DownloadPlan(chainWithFallback, isForcibly)
            {
                FallbackFulls = fallbackFulls
            };
        }

        // No full packages at all: return chain packages as-is
        return new DownloadPlan(chainCandidates, isForcibly);
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
        return cur.Value >= min.Value;
    }

    /// <summary>Parses a version string and returns null if the string cannot be parsed.</summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>A parsed <see cref="SemVersion"/> value, or null if parsing fails.</returns>
    internal static SemVersion? ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        return Semver.TryParse(version, out var v) ? v : null;
    }
}
