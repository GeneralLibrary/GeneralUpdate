using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Download.Sources;

/// <summary>
/// HTTP 下载源，通过调用版本验证 API 获取更新资产列表。
/// 分别对客户端（Client）和升级端（Upgrade）执行版本验证，
/// 并将服务器响应转换为 <see cref="DownloadAsset"/> 列表。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IDownloadSource"/> 接口，是标准 HTTP 更新流程的下载源实现。
/// </para>
/// <para>
/// 工作流程：
/// <list type="number">
///   <item>调用 <c>VersionService.Validate</c> 分别对 <c>AppType.Client</c> 和 <c>AppType.Upgrade</c>
///        进行版本验证。升级端版本优先使用 <c>upgradeClientVersion</c>，如果未配置则使用 <c>clientVersion</c>。</item>
///   <item>将两次验证返回的版本信息映射为 <see cref="DownloadAsset"/> 对象。</item>
///   <item>按 URL 对资产进行去重（两个验证调用可能返回相同的包）。</item>
///   <item>返回包含资产列表和更新标志的 <see cref="DownloadSourceResult"/>。</item>
/// </list>
/// </para>
/// <para>
/// 两次验证调用的设计是为了支持客户端自身更新和主应用程序更新的场景：
/// 客户端更新包和应用程序更新包可能来自同一个版本服务器，但属于不同的应用类型。
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
    /// 使用指定的更新配置初始化 HTTP 下载源。
    /// </summary>
    /// <param name="updateUrl">版本验证 API 的 URL。</param>
    /// <param name="clientVersion">当前客户端版本号。</param>
    /// <param name="upgradeClientVersion">升级端（Upgrade 应用）的当前版本号。
    /// 如果为 null 或为空，则使用 <paramref name="clientVersion"/>。</param>
    /// <param name="appSecretKey">应用密钥，用于 API 身份验证。</param>
    /// <param name="platform">目标平台类型（Windows、Linux、macOS 等）。</param>
    /// <param name="productId">可选的产品 ID。</param>
    /// <param name="scheme">可选的认证方案。</param>
    /// <param name="token">可选的认证令牌。</param>
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
    /// 异步获取下载资产列表，通过对客户端和升级端分别执行版本验证，
    /// 合并结果并按 URL 去重。
    /// </summary>
    /// <param name="token">可选的取消令牌。</param>
    /// <returns>包含资产列表和更新标志（HasMainUpdate、HasUpgradeUpdate）的
    /// <see cref="DownloadSourceResult"/>。</returns>
    /// <remarks>
    /// <para>
    /// 此方法会调用 <c>VersionService.Validate</c> 两次：
    /// </para>
    /// <list type="number">
    ///   <item>第一次使用 <c>AppType.Client</c> 验证主应用的更新。</item>
    ///   <item>第二次使用 <c>AppType.Upgrade</c> 验证升级程序自身的更新。</item>
    /// </list>
    /// <para>
    /// 两次调用使用相同的 <c>_updateUrl</c>、<c>_appSecretKey</c> 和 <c>_platform</c>，
    /// 但使用不同的 <c>AppType</c>。这允许服务器根据应用类型返回不同的更新包。
    /// 结果中的资产列表会按 URL 去重，因为两个验证调用可能返回相同的包。
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
    /// 将服务器返回的 <see cref="VersionInfo"/> 对象映射为 <see cref="DownloadAsset"/> 对象。
    /// </summary>
    /// <param name="v">服务器返回的版本信息。</param>
    /// <returns>转换后的 <see cref="DownloadAsset"/> 实例，包含名称、URL、大小、哈希值、
    /// 版本号、强制更新标志、冻结标志、认证信息等属性。</returns>
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
