using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download;

/// <summary>
/// 构建下载计划（<see cref="DownloadPlan"/>），从下载资产列表中筛选出需要下载的资产。
/// 处理冻结包过滤、强制更新标记和 MinClientVersion 兼容性检查。
/// </summary>
/// <remarks>
/// <para>
/// 此类为静态工具类，负责根据当前客户端版本从服务器返回的资产列表中构建合理的下载计划。
/// 构建过程遵循以下规则：
/// </para>
/// <list type="bullet">
///   <item><term>冻结包过滤</term><description>跳过被标记为冻结（<c>IsFreeze = true</c>）的包，
///        这些包不参与更新下载。</description></item>
///   <item><term>强制更新检测</term><description>检测资产列表中是否存在强制更新（<c>IsForcibly = true</c>）标记，
///        如果存在，整个更新计划被标记为强制更新。</description></item>
///   <item><term>版本过滤和排序</term><description>只保留版本高于当前客户端版本的包，
///        并按版本号升序排列（从低到高依次安装）。</description></item>
///   <item><term>兼容性检查</term><description>检查每个包的 <c>MinClientVersion</c> 要求，
///        如果当前客户端版本低于要求的最小版本，则跳过该包。</description></item>
/// </list>
/// <para>
/// 注意：此构建器不区分跨版本更新和版本链更新，每个包携带自己的 <c>IsCrossVersion</c> 元数据供下游处理。
/// </para>
/// </remarks>
public static class DownloadPlanBuilder
{
    /// <summary>
    /// 从下载资产列表中构建下载计划。
    /// </summary>
    /// <param name="assets">从下载源获取的资产列表。</param>
    /// <param name="currentVersion">当前客户端版本字符串。</param>
    /// <returns>包含有序资产的 <see cref="DownloadPlan"/>。如果不需要更新，则返回 <see cref="DownloadPlan.Empty"/>。</returns>
    /// <remarks>
    /// <para>
    /// 构建流程：
    /// </para>
    /// <list type="number">
    ///   <item>验证输入：如果资产列表为 null 或当前版本无法解析，返回空计划。</item>
    ///   <item>过滤冻结包：移除 <c>IsFreeze = true</c> 的资产。</item>
    ///   <item>检查强制更新：如果任一资产标记为强制更新，整个计划标记为强制。</item>
    ///   <item>版本过滤：只保留版本高于当前客户端版本的资产。</item>
    ///   <item>兼容性检查：确保每个资产的 <c>MinClientVersion</c> 与当前版本兼容。</item>
    ///   <item>升序排序：按版本号从小到大排序。</item>
    /// </list>
    /// <para>
    /// 如果没有符合条件的资产，返回 <c>DownloadPlan.Empty</c>。
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
    /// 检查 MinClientVersion 是否与当前客户端版本兼容。
    /// 如果某个包的 MinClientVersion 高于当前版本，则该包不适用于当前客户端。
    /// </summary>
    /// <param name="minClientVersion">包要求的最低客户端版本。如果为 null 或空，则视为兼容。</param>
    /// <param name="currentVersion">当前客户端版本字符串。</param>
    /// <returns>如果当前版本达到或超过最低要求则返回 true，否则返回 false。</returns>
    internal static bool IsCompatible(string? minClientVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(minClientVersion)) return true;
        var min = ParseVersion(minClientVersion);
        var cur = ParseVersion(currentVersion);
        if (min == null || cur == null) return true;
        return cur >= min;
    }

    /// <summary>解析版本字符串，如果无法解析则返回 null。</summary>
    /// <param name="version">要解析的版本字符串。</param>
    /// <returns>解析后的 <see cref="Version"/> 对象，如果解析失败则返回 null。</returns>
    internal static Version? ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        return Version.TryParse(version, out var v) ? v : null;
    }
}
