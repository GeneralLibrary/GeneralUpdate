using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Download.Sources;

/// <summary>
/// HTTP download source — calls the version validation API
/// and converts the server response to a list of DownloadAssets.
/// </summary>
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

    /// <summary>Call version API and return download assets.</summary>
    public async Task<IReadOnlyList<DownloadAsset>> ListAsync(CancellationToken token = default)
    {
        var mainResp = await VersionService.Validate(
            _updateUrl, _clientVersion, AppType.Client,
            _appSecretKey, _platform, _productId,
            _scheme, _token, token);

        var upgradeResp = await VersionService.Validate(
            _updateUrl, _upgradeClientVersion ?? _clientVersion, AppType.Upgrade,
            _appSecretKey, _platform, _productId,
            _scheme, _token, token);

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

        return assets;
    }

    private static DownloadAsset MapVersionInfo(VersionInfo v)
    {
        return new DownloadAsset(
            Name: v.Name ?? v.Version ?? "unknown",
            Url: v.Url ?? string.Empty,
            Size: v.Size ?? 0,
            SHA256: v.Hash,
            Version: v.Version ?? "0.0.0",
            IsForcibly: v.IsForcibly == true
        );
    }
}
