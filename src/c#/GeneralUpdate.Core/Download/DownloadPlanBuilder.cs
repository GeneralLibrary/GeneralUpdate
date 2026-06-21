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
/// Chain vs full evaluation uses a count-first heuristic:
/// a full package is selected when the number of chain packages exceeds
/// <c>MaxChainBeforeFallback</c> (default 8), or when the combined download size
/// of all chain packages equals or exceeds the full package size.
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

    /// <summary>Pre-parses versions for a list of assets to avoid repeated Semver.TryParse calls.</summary>
    private static Dictionary<DownloadAsset, SemVersion?> PreParseVersions(IEnumerable<DownloadAsset> assets)
    {
        var map = new Dictionary<DownloadAsset, SemVersion?>();
        foreach (var a in assets)
        {
            // Custom IDownloadSource implementations could return duplicate records;
            // silently accept the first occurrence rather than throwing.
            if (!map.ContainsKey(a))
                map[a] = ParseVersion(a.Version);
        }
        return map;
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
    /// <param name="maxChainBeforeFallback">
    /// Maximum number of chain packages allowed before falling back to a single full package.
    /// Default is <c>8</c>. Set to <c>0</c> to disable the count-based fallback; set to
    /// a negative value to always prefer chain packages when their combined size is smaller
    /// than the full package.
    /// </param>
    public static DownloadPlan Build(
        IEnumerable<DownloadAsset> assets,
        string clientVersion,
        string? upgradeClientVersion,
        int maxChainBeforeFallback = 8)
    {
        if (assets == null) return DownloadPlan.Empty;
        var parsedClient = ParseVersion(clientVersion);
        if (parsedClient == null) return DownloadPlan.Empty;
        var cv = parsedClient.Value;

        var parsedUpgrade = ParseVersion(upgradeClientVersion) ?? parsedClient;
        var uv = parsedUpgrade.Value;

        // Pre-parse all asset versions to avoid repeated Semver.TryParse calls.
        var versionMap = PreParseVersions(assets);

        // Helper: safe lookup that matches netstandard2.0 (no GetValueOrDefault).
        SemVersion? Lookup(DownloadAsset a)
        {
            versionMap.TryGetValue(a, out var sv);
            return sv;
        }

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
                var pv = Lookup(a);
                if (pv == null) return false;

                var localVersion = (a.AppType == (int)AppType.Upgrade)
                    ? uv
                    : cv;

                return pv.Value > localVersion;
            })
            .Where(a => IsCompatible(a.MinClientVersion, clientVersion))
            .OrderBy(a => Lookup(a))
            .ToList();

        if (candidates.Count == 0) return DownloadPlan.Empty;

        // 4. Separate chain vs full packages.
        var chainCandidates = candidates
            .Where(a => a.PackageType == (int)Configuration.PackageType.Chain
                        || a.PackageType == (int)Configuration.PackageType.Unspecified)
            .ToList();

        var fullCandidates = candidates
            .Where(a => a.PackageType == (int)Configuration.PackageType.Full)
            .ToList();

        // ── Chain vs Full: count-first heuristic ──
        if (chainCandidates.Count > 0 && fullCandidates.Count > 0)
        {
            var bestFull = fullCandidates
                .OrderByDescending(a => Lookup(a))
                .First();

            long chainTotal = chainCandidates
                .Where(a => a.AppType == bestFull.AppType)
                .Sum(a => a.Size);
            int chainCount = chainCandidates.Count(a => a.AppType == bestFull.AppType);

            // Local helper: build a "switch to full" plan that replaces same-AppType
            // chain packages with bestFull, while keeping chains for other AppTypes
            // (and any same-AppType chains whose version exceeds the full's version).
            DownloadPlan SwitchToFull(string reason)
            {
                GeneralTracer.Info($"DownloadPlanBuilder: {reason}, switching to full package {bestFull.Name} (chain count {chainCount}, chain total {chainTotal}, full size {bestFull.Size})");
                var fullVersion = Lookup(bestFull);
                var planAssets = new List<DownloadAsset> { bestFull };
                planAssets.AddRange(chainCandidates
                    .Where(a => a.AppType != bestFull.AppType
                                || (Lookup(a) is { } av && fullVersion != null && av > fullVersion))
                    .OrderBy(a => Lookup(a)));
                return new DownloadPlan(planAssets, isForcibly);
            }

            if (maxChainBeforeFallback > 0 && chainCount > maxChainBeforeFallback)
                return SwitchToFull($"chain count {chainCount} exceeds MaxChainBeforeFallback {maxChainBeforeFallback}");

            if (chainTotal >= bestFull.Size)
                return SwitchToFull($"chain total {chainTotal} >= full size {bestFull.Size}");
        }

        // ── Chain plan with fallback fulls ──
        if (fullCandidates.Count > 0)
        {
            var fallbackFulls = new List<DownloadAsset>();

            var chainWithFallback = chainCandidates
                .Select(chain =>
                {
                    var match = fullCandidates
                        .Where(f => f.AppType == chain.AppType)
                        .OrderBy(f => Lookup(f))
                        .FirstOrDefault(f =>
                        {
                            var fv = Lookup(f);
                            var cv = Lookup(chain);
                            return fv != null && cv != null && fv.Value >= cv.Value;
                        });

                    if (match != null)
                    {
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
    /// </summary>
    internal static bool IsCompatible(string? minClientVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(minClientVersion)) return true;
        var min = ParseVersion(minClientVersion);
        var cur = ParseVersion(currentVersion);
        if (min == null || cur == null) return true;
        return cur.Value >= min.Value;
    }

    /// <summary>Parses a version string and returns null if the string cannot be parsed.</summary>
    internal static SemVersion? ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        return Semver.TryParse(version, out var v) ? v : null;
    }
}
