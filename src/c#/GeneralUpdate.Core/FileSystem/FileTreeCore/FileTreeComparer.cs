using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 两个文件树快照的比较结果，包含新增、修改和删除的文件列表。
/// </summary>
/// <param name="Added">在新快照中新增的文件条目列表。</param>
/// <param name="Modified">在旧快照和新快照之间发生变化的文件条目列表（大小或修改时间不同）。</param>
/// <param name="Deleted">在旧快照中存在但在新快照中被删除的相对路径列表。</param>
/// <remarks>
/// <para>
/// FileTreeDiff 由 <see cref="FileTreeComparer.Compare"/> 方法生成，
/// 为差异更新提供三组清晰的变更集合：
/// </para>
/// <list type="bullet">
///   <item><description><b>Added</b>：新增的文件需要直接打包到更新包中。</description></item>
///   <item><description><b>Modified</b>：修改的文件需要通过差异补丁或完整替换的方式更新。</description></item>
///   <item><description><b>Deleted</b>：删除的文件需要在更新时从客户端清除。</description></item>
/// </list>
/// <para>
/// 使用 <see cref="HasChanges"/> 快速检查是否有任何变更，
/// 使用 <see cref="TotalChanges"/> 获取变更总数。
/// </para>
/// </remarks>
public readonly record struct FileTreeDiff(
    IReadOnlyList<FileEntry> Added,
    IReadOnlyList<FileEntry> Modified,
    IReadOnlyList<string> Deleted
)
{
    /// <summary>
    /// 获取一个值，指示是否存在任何变更（新增、修改或删除）。
    /// </summary>
    public bool HasChanges => Added.Count > 0 || Modified.Count > 0 || Deleted.Count > 0;

    /// <summary>
    /// 获取变更总数（新增 + 修改 + 删除）。
    /// </summary>
    public int TotalChanges => Added.Count + Modified.Count + Deleted.Count;

    /// <summary>
    /// 获取一个空的 <see cref="FileTreeDiff"/> 实例，表示无任何变更。
    /// </summary>
    public static FileTreeDiff Empty { get; } = new(
        Array.Empty<FileEntry>(), Array.Empty<FileEntry>(), Array.Empty<string>());
}

/// <summary>
/// 比较两个 <see cref="FileTreeSnapshot"/> 实例，生成 <see cref="FileTreeDiff"/> 差异结果。
/// 识别出两个文件快照之间新增、修改和删除的文件。
/// </summary>
/// <remarks>
/// <para>
/// FileTreeComparer 是差异更新流程的关键组件，提供了两种比较方式：
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Compare"/>：完整比较，返回所有新增、修改和删除的文件列表。</description></item>
///   <item><description><see cref="HasChanges(FileTreeSnapshot, FileTreeSnapshot)"/>：快速检查，
///   一旦发现第一个差异立即返回 <c>true</c>，适用于只需要判断"是否有更新"的场景。</description></item>
/// </list>
/// <para>
/// 比较算法基于文件相对路径的字典映射，使用 <c>OrdinalIgnoreCase</c> 字符串比较确保跨平台兼容性。
/// 文件是否修改通过比较 <see cref="FileEntry.Size"/> 和 <see cref="FileEntry.LastWriteTimeUtc"/> 判定。
/// </para>
/// </remarks>
public static class FileTreeComparer
{
    /// <summary>
    /// 比较两个文件系统快照，返回完整的差异结果。
    /// </summary>
    /// <param name="old">旧版本（基准）的文件系统快照。</param>
    /// <param name="updated">新版本（目标）的文件系统快照。</param>
    /// <returns>包含新增、修改和删除文件的 <see cref="FileTreeDiff"/> 结构。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="old"/> 或 <paramref name="updated"/> 为 <c>null</c> 时抛出。</exception>
    /// <remarks>
    /// <para>
    /// 比较步骤：
    /// <list type="number">
    ///   <item><description>将新旧快照分别转换为以 <c>RelativePath</c> 为键的字典。</description></item>
    ///   <item><description>遍历新快照：如果路径在旧快照中不存在，标记为"新增"；如果存在但大小或时间不同，标记为"修改"。</description></item>
    ///   <item><description>遍历旧快照：如果路径在新快照中不存在，标记为"删除"。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static FileTreeDiff Compare(FileTreeSnapshot old, FileTreeSnapshot updated)
    {
        if (old == null) throw new ArgumentNullException(nameof(old));
        if (updated == null) throw new ArgumentNullException(nameof(updated));

        var oldMap = old.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);
        var newMap = updated.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);

        var added = new List<FileEntry>();
        var modified = new List<FileEntry>();
        var deleted = new List<string>();

        // Files present in updated but not in old → Added
        // Files present in updated and old with different size or time → Modified
        foreach (var kv in newMap)
        {
            var path = kv.Key;
            var entry = kv.Value;
            if (!oldMap.TryGetValue(path, out var oldEntry))
            {
                added.Add(entry);
            }
            else if (oldEntry.Size != entry.Size || oldEntry.LastWriteTimeUtc != entry.LastWriteTimeUtc)
            {
                modified.Add(entry);
            }
        }

        // Files present in old but not in updated → Deleted
        foreach (var path in oldMap.Keys)
        {
            if (!newMap.ContainsKey(path))
                deleted.Add(path);
        }

        return new FileTreeDiff(added.AsReadOnly(), modified.AsReadOnly(), deleted.AsReadOnly());
    }

    /// <summary>
    /// 快速检查两个文件系统快照之间是否存在任何变化（短路模式）。
    /// </summary>
    /// <param name="old">旧版本（基准）的文件系统快照。</param>
    /// <param name="updated">新版本（目标）的文件系统快照。</param>
    /// <returns>如果存在任何文件变化则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 与 <see cref="Compare"/> 不同，此方法在发现第一个差异时立即返回 <c>true</c>，
    /// 不会继续遍历其余文件，因此在仅需判断"是否有更新"的场景下性能更优。
    /// 首先通过比较条目总数进行快速预判，然后再逐项检查。
    /// </remarks>
    public static bool HasChanges(FileTreeSnapshot old, FileTreeSnapshot updated)
    {
        if (old.Entries.Count != updated.Entries.Count) return true;

        var oldMap = old.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);
        var newDict = updated.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in newDict)
        {
            if (!oldMap.TryGetValue(kv.Key, out var oldEntry)) return true;
            if (oldEntry.Size != kv.Value.Size || oldEntry.LastWriteTimeUtc != kv.Value.LastWriteTimeUtc) return true;
        }
        return false;
    }
}
