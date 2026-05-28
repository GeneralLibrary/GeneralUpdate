using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 不可变的文件条目记录，表示目录树中的一个文件快照。
/// 记录文件的相对路径、大小和最后修改时间，用于文件差异比较。
/// </summary>
/// <param name="RelativePath">文件相对于根目录的路径。</param>
/// <param name="Size">文件大小（字节数）。</param>
/// <param name="LastWriteTimeUtc">文件最后写入时间的 UTC 表示。</param>
/// <remarks>
/// FileEntry 是 <see cref="FileTreeSnapshot"/> 的基本数据单元，
/// 通过比较 <see cref="Size"/> 和 <see cref="LastWriteTimeUtc"/> 来判断文件是否被修改。
/// 此记录类型为只读结构，保证了快照的不可变性。
/// </remarks>
public readonly record struct FileEntry(
    string RelativePath,
    long Size,
    DateTime LastWriteTimeUtc
);

/// <summary>
/// 不可变的目录树快照，记录某一时刻目录中所有文件的元数据状态。
/// </summary>
/// <remarks>
/// <para>
/// FileTreeSnapshot 由 <see cref="FileTreeEnumerator"/> 配合 <see cref="IBlackListMatcher"/>
/// 创建，文件遍历过程中自动跳过黑名单中的文件和目录。
/// </para>
/// <para>
/// 典型使用流程：
/// <list type="number">
///   <item><description>使用 <see cref="FromEnumerator"/> 方法从根目录创建快照。</description></item>
///   <item><description>将快照通过 JSON 序列化后储存或传输。</description></item>
///   <item><description>在差异更新时，使用 <see cref="FileTreeComparer.Compare"/> 对比新旧快照。</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class FileTreeSnapshot
{
    /// <summary>
    /// 获取快照创建时的 UTC 时间戳。
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// 获取被快照的根目录路径。
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// 获取快照中包含的所有文件条目只读列表。
    /// </summary>
    public IReadOnlyList<FileEntry> Entries { get; }

    /// <summary>
    /// 使用指定的根目录路径和文件条目初始化 <see cref="FileTreeSnapshot"/> 的新实例。
    /// </summary>
    /// <param name="rootPath">被快照的根目录路径。</param>
    /// <param name="entries">文件条目集合。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="rootPath"/> 为 <c>null</c> 时抛出。</exception>
    public FileTreeSnapshot(string rootPath, IEnumerable<FileEntry> entries)
    {
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        Entries = (entries ?? Array.Empty<FileEntry>()).ToList();
    }

    /// <summary>
    /// 使用 <see cref="FileTreeEnumerator"/> 遍历指定根目录，创建文件系统快照。
    /// </summary>
    /// <param name="rootPath">要创建快照的根目录路径。</param>
    /// <param name="enumerator">配置了黑名单规则的 <see cref="FileTreeEnumerator"/> 实例。</param>
    /// <returns>包含目录中所有（未被黑名单过滤的）文件的新快照。</returns>
    /// <remarks>
    /// 此方法会枚举根目录下的所有文件（跳过黑名单中的文件和目录），
    /// 为每个文件创建 <see cref="FileEntry"/> 记录（含大小和最后修改时间），
    /// 然后封装为 <see cref="FileTreeSnapshot"/> 返回。
    /// 快照创建时会记录当前 UTC 时间作为 <see cref="CreatedAt"/>。
    /// </remarks>
    public static FileTreeSnapshot FromEnumerator(string rootPath, FileTreeEnumerator enumerator)
    {
        var entries = new List<FileEntry>();
        var normalizedRoot = rootPath.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())
            ? rootPath
            : rootPath + System.IO.Path.DirectorySeparatorChar;

        foreach (var filePath in enumerator.EnumerateFiles(rootPath))
        {
            var fi = new System.IO.FileInfo(filePath);
            // Manual relative path (netstandard2.0 compatible)
            var relative = filePath.StartsWith(normalizedRoot)
                ? filePath.Substring(normalizedRoot.Length)
                : filePath;
            entries.Add(new FileEntry(relative, fi.Length, fi.LastWriteTimeUtc));
        }
        return new FileTreeSnapshot(rootPath, entries);
    }

    /// <summary>
    /// 创建一个空的文件系统快照（不含任何文件条目），用于表示空目录或占位。
    /// </summary>
    /// <param name="rootPath">根目录路径。</param>
    /// <returns>空的 <see cref="FileTreeSnapshot"/> 实例。</returns>
    public static FileTreeSnapshot Empty(string rootPath) => new(rootPath, Array.Empty<FileEntry>());
}
