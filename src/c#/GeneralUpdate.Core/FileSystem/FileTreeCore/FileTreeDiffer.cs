using System;
using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 根据 <see cref="FileTreeDiff"/> 差异结果生成增量更新所需的数据包。
/// 用于 Pipeline 的 PatchMiddleware 进行差异补丁的构建和应用。
/// </summary>
/// <remarks>
/// <para>
/// FileTreeDiffer 将文件快照比较结果转换为实际的增量更新操作指令：
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ProduceDeltaPaths"/>：基于差异结果生成需要打包的文件路径对。</description></item>
///   <item><description><see cref="ProduceDeletes"/>：提取需要在客户端删除的文件列表。</description></item>
///   <item><description><see cref="ShouldUseDeltaPatching"/>：根据变更比例决策使用增量更新还是全量更新。</description></item>
/// </list>
/// <para>
/// 增量更新策略核心原则：变更比例低于阈值时使用增量补丁，高于阈值时建议使用全量包，
/// 以避免大量小补丁导致的性能开销。
/// </para>
/// </remarks>
public static class FileTreeDiffer
{
    /// <summary>
    /// 根据差异结果生成需要打包的文件路径对。
    /// </summary>
    /// <param name="diff">新旧快照的差异结果。</param>
    /// <param name="updatedRoot">新版本文件的根目录路径。</param>
    /// <returns>
    /// 路径对的只读列表，每对包含 <c>(源文件完整路径, 相对路径)</c>，
    /// 其中源文件路径指向新版本文件，相对路径用于在更新包中定位。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 生成逻辑：
    /// <list type="bullet">
    ///   <item><description>新增文件（Added）：直接包含到增量包中。</description></item>
    ///   <item><description>修改文件（Modified）：包含到增量包中。</description></item>
    ///   <item><description>删除文件（Deleted）：不在此处处理，由 <see cref="ProduceDeletes"/> 单独提取。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 对于每对路径，会检查源文件是否确实存在于磁盘上，不存在则跳过。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string SourcePath, string RelativePath)> ProduceDeltaPaths(
        FileTreeDiff diff, string updatedRoot)
    {
        var result = new List<(string, string)>();

        // Added files — can be bundled directly
        foreach (var entry in diff.Added)
        {
            var sourcePath = Path.Combine(updatedRoot, entry.RelativePath);
            if (File.Exists(sourcePath))
                result.Add((sourcePath, entry.RelativePath));
        }

        // Modified files — need patching
        foreach (var entry in diff.Modified)
        {
            var sourcePath = Path.Combine(updatedRoot, entry.RelativePath);
            if (File.Exists(sourcePath))
                result.Add((sourcePath, entry.RelativePath));
        }

        // Deleted files — skipped (handled by cleanup separately)

        return result.AsReadOnly();
    }

    /// <summary>
    /// 提取差异结果中需要删除的文件相对路径列表。
    /// </summary>
    /// <param name="diff">新旧快照的差异结果。</param>
    /// <returns>需要在客户端删除的文件相对路径只读列表。</returns>
    /// <remarks>
    /// 此方法直接返回 <see cref="FileTreeDiff.Deleted"/> 列表，该列表包含
    /// 在旧版本中存在但在新版本中已被移除的所有文件相对路径。
    /// </remarks>
    public static IReadOnlyList<string> ProduceDeletes(FileTreeDiff diff)
        => diff.Deleted;

    /// <summary>
    /// 根据变更比例决策最优更新模式：变更比例低时推荐增量更新（delta），比例高时推荐全量更新。
    /// </summary>
    /// <param name="diff">新旧快照的差异结果。</param>
    /// <param name="totalFileCount">应用程序的文件总数。</param>
    /// <param name="thresholdPercent">增量更新阈值百分比。当变更文件比例小于等于此值时推荐增量更新，默认为 0.5（50%）。</param>
    /// <returns>如果推荐使用增量更新（delta patching）则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// <para>
    /// 决策逻辑：变更比例 = (新增 + 修改 + 删除) / 文件总数。
    /// </para>
    /// <list type="bullet">
    ///   <item><description>变更比例 &lt;= thresholdPercent：推荐增量更新。少量变更使用补丁更高效。</description></item>
    ///   <item><description>变更比例 &gt; thresholdPercent：推荐全量更新。大量变更时增量补丁的优势丧失。</description></item>
    /// </list>
    /// <para>
    /// 当 <paramref name="totalFileCount"/> 为 0 时返回 <c>false</c>。
    /// </para>
    /// </remarks>
    public static bool ShouldUseDeltaPatching(FileTreeDiff diff, int totalFileCount, double thresholdPercent = 0.5)
    {
        if (totalFileCount == 0) return false;
        var changeRatio = (double)diff.TotalChanges / totalFileCount;
        return changeRatio <= thresholdPercent;
    }
}
