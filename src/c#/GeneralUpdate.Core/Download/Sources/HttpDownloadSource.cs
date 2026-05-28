using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Download.Sources;

/// <summary>
/// HTTP download source that retrieves update asset lists by calling a version-validation API.
/// Performs version validation for both Client and Upgrade app types,
/// then maps the server responses into <see cref="DownloadAsset"/> lists.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDownloadSource"/> and serves as the standard HTTP-based
/// download source for the update workflow.
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item>Calls <c>VersionService.Validate</c> for <c>AppType.Client</c> to validate the main application version.</item>
///   <item>Calls <c>VersionService.Validate</c> for <c>AppType.Upgrade</c> to validate the upgrade client version.
///         The upgrade client version uses <c>upgradeClientVersion</c> if provided; otherwise falls back to <c>clientVersion</c>.</item>
///   <item>Maps the version information returned by both validations into <see cref="DownloadAsset"/> objects.</item>
///   <item>Deduplicates assets by URL (both validation calls may return the same package).</item>
///   <item>Returns a <see cref="DownloadSourceResult"/> containing the asset list and update flags.</item>
/// </list>
/// </para>
/// <para>
/// The two-step validation design supports scenarios where the client's own update package
/// and the main application's update package come from the same version server but belong
/// to different application types.
/// </para>
/// </remarks>
public class HttpDownloadSource : Abstractions.IDownloadSource
{
    private readonly string _updateUrl;
    private readonly string _clientVersion;
    private readonly string? _upgradeClientVersion;
    private readonly string _appSecretKey;
    private readonly PlatformType _platform;
    private readonly string? _productId;
    private readonly string? _scheme;
    private readonly string? _token;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpDownloadSource"/> class
    /// with the specified update configuration parameters.
    /// </summary>
    /// <param name="updateUrl">The URL of the version-validation API.</param>
    /// <param name="clientVersion">The current client version string.</param>
    /// <param name="upgradeClientVersion">
    /// The current version string of the Upgrade application.
    /// If null or empty, <paramref name="clientVersion"/> is used instead.
    /// </param>
    /// <param name="appSecretKey">The application secret key used for API authentication.</param>
    /// <param name="platform">The target platform type (Windows, Linux, macOS, etc.).</param>
    /// <param name="productId">An optional product identifier.</param>
    /// <param name="scheme">An optional authentication scheme.</param>
    /// <param name="token">An optional authentication token.</param>
    public HttpDownloadSource(
        string updateUrl,
        string clientVersion,
        string? upgradeClientVersion,
        string appSecretKey,
        PlatformType platform,
        string? productId,
        string? scheme,
        string? token)
    {
        _updateUrl = updateUrl;
        _clientVersion = clientVersion;
        _upgradeClientVersion = upgradeClientVersion;
        _appSecretKey = appSecretKey;
        _platform = platform;
        _productId = productId;
        _scheme = scheme;
        _token = token;
    }

    /// <summary>
    /// Asynchronously retrieves the list of downloadable assets by performing version validation
    /// for both Client and Upgrade app types, merging the results, and deduplicating by URL.
    /// </summary>
    /// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    /// A <see cref="DownloadSourceResult"/> containing the merged and deduplicated list of
    /// <see cref="DownloadAsset"/> objects, along with flags indicating whether main and/or
    /// upgrade updates are available.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method calls <c>VersionService.Validate</c> twice:
    /// </para>
    /// <list type="number">
    ///   <item>First with <c>AppType.Client</c> to validate the main application update.</item>
    ///   <item>Second with <c>AppType.Upgrade</c> to validate the upgrade program's own update.</item>
    /// </list>
    /// <para>
    /// Both calls use the same <c>_updateUrl</c>, <c>_appSecretKey</c>, and <c>_platform</c>,
    /// but with different <c>AppType</c> values. This allows the server to return different
    /// update packages depending on the application type.
    /// The resulting asset list is deduplicated by URL, as both validation calls may return the same packages.
    /// </para>
    /// </remarks>
    public async Task<DownloadSourceResult> ListAsync(CancellationToken token = default)
    {
        var mainResp = await VersionService.Validate(
            _updateUrl, _clientVersion, AppType.Client,
            _appSecretKey, _platform, _productId,
            _scheme, _token, token);

        var upgradeResp = await VersionService.Validate(
            _updateUrl, _upgradeClientVersion ?? _clientVersion, AppType.Upgrade,
            _appSecretKey, _platform, _productId,
            _scheme, _token, token);

        var hasMainUpdate = mainResp?.Body?.Count > 0;
        var hasUpgradeUpdate = upgradeResp?.Body?.Count > 0;

        var assets = new List<DownloadAsset>();

        if (mainResp?.Body != null)
        {
            foreach (var v in mainResp.Body)
                assets.Add(MapVersionInfo(v));
        }

        if (upgradeResp?.Body != null)
        {
            foreach (var v in upgradeResp.Body)
                assets.Add(MapVersionInfo(v));
        }

        // Deduplicate by URL — both Validate calls may return the same packages
        return new DownloadSourceResult
        {
            Assets = assets.GroupBy(a => a.Url).Select(g => g.First()).ToList(),
            HasMainUpdate = hasMainUpdate,
            HasUpgradeUpdate = hasUpgradeUpdate
        };
    }

    /// <summary>
    /// Maps a server-returned <see cref="VersionInfo"/> object to a <see cref="DownloadAsset"/> object.
    /// </summary>
    /// <param name="v">The version information returned by the server.</param>
    /// <returns>
    /// A <see cref="DownloadAsset"/> instance populated with the name, URL, size, hash value,
    /// version number, forced-update flag, freeze flag, authentication information, and other metadata.
    /// </returns>
    private static DownloadAsset MapVersionInfo(VersionInfo v)
    {
        return new DownloadAsset(
            Name: v.Name ?? v.Version ?? "unknown",
            Url: v.Url ?? string.Empty,
            Size: v.Size ?? 0,
            SHA256: v.Hash,
            Version: v.Version ?? "0.0.0",
            IsForcibly: v.IsForcibly == true,
            IsFreeze: v.IsFreeze == true,
            RecordId: v.RecordId,
            UpgradeMode: v.UpgradeMode,
            AppType: v.AppType,
            IsCrossVersion: v.IsCrossVersion == true,
            FromVersion: v.FromVersion,
            TargetArchiveHash: v.Hash,
            AuthScheme: v.AuthScheme,
            AuthToken: v.AuthToken
        );
    }
}
