using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// <see cref="ICleanMatcher"/> 的默认实现，保留 Clean 阶段（差异生成）的原始行为。
/// </summary>
/// <remarks>
/// <para>
/// DefaultCleanMatcher 实现了差异生成阶段的三种核心匹配操作：
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Compare"/>：委托给 <see cref="StorageManager.Compare"/>，
///   对新旧两个目录进行完整的递归比较。</description></item>
///   <item><description><see cref="Except"/>：委托给 <see cref="StorageManager.Except"/>，
///   以左侧为基准找出右侧不存在的文件（即待删除的文件）。</description></item>
///   <item><description><see cref="Match"/>：通过文件名称和相对路径的不区分大小写匹配，
///   在新文件中查找对应的旧版本文件。匹配同时要求两个文件都存在在磁盘上才视为有效匹配。</description></item>
/// </list>
/// <para>
/// 此实现的设计目标是保持与早期版本的向后兼容性。
/// 如需自定义匹配逻辑（如基于文件哈希或元数据的匹配），可实现 <see cref="ICleanMatcher"/> 接口。
/// </para>
/// </remarks>
public class DefaultCleanMatcher : ICleanMatcher
{
    private readonly StorageManager _storageManager = new StorageManager();

    /// <inheritdoc/>
    public ComparisonResult Compare(string sourcePath, string targetPath)
        => _storageManager.Compare(sourcePath, targetPath);

    /// <inheritdoc/>
    public IEnumerable<FileNode>? Except(string sourcePath, string targetPath)
        => _storageManager.Except(sourcePath, targetPath);

    /// <inheritdoc/>
    public FileNode? Match(FileNode newFile, IEnumerable<FileNode> leftNodes)
    {
        var oldFile = leftNodes.FirstOrDefault(i =>
            string.Equals(i.Name, newFile.Name, System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.RelativePath, newFile.RelativePath, System.StringComparison.OrdinalIgnoreCase));
        if (oldFile is null) return null;
        if (!File.Exists(oldFile.FullName)) return null;
        if (!File.Exists(newFile.FullName)) return null;
        return oldFile;
    }
}
