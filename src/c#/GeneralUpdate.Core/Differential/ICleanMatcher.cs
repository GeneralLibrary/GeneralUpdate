using System.Collections.Generic;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// 定义差异生成（Clean）阶段的完整匹配策略接口。
/// 实现者负责目录比较、识别已删除的文件以及将新文件匹配到对应的旧文件。
/// </summary>
/// <remarks>
/// <para>
/// Clean 阶段（差异生成阶段）是差异更新的第一步，其核心目标是从新旧两个版本的目录中
/// 生成差异补丁所需的所有信息。此接口定义了三个关键操作：
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Compare"/>：对新旧目录进行递归比较，获取完整的差异信息。</description></item>
///   <item><description><see cref="Except"/>：识别在旧版本中存在但在新版本中被移除的文件。</description></item>
///   <item><description><see cref="Match"/>：将新版本中的文件与旧版本中对应的文件建立匹配关系，
///   以便后续通过差异算法生成二进制补丁。</description></item>
/// </list>
/// <para>
/// 默认实现请参考 <see cref="DefaultCleanMatcher"/>。
/// 如果需要自定义匹配逻辑（例如基于文件哈希值或自定义元数据的匹配），可以实现此接口。
/// </para>
/// </remarks>
public interface ICleanMatcher
{
    /// <summary>
    /// 比较源目录和目标目录，返回发生变更的文件集合。
    /// </summary>
    /// <param name="sourcePath">源（旧版本）目录路径。</param>
    /// <param name="targetPath">目标（新版本）目录路径。</param>
    /// <returns>包含左右两侧节点列表和差异节点列表的 <see cref="ComparisonResult"/>。</returns>
    /// <remarks>
    /// 此方法是差异生成的入口点。实现应递归遍历两个目录，
    /// 通过文件哈希值或元数据比较识别出所有新增、修改和删除的文件。
    /// </remarks>
    ComparisonResult Compare(string sourcePath, string targetPath);

    /// <summary>
    /// 返回仅存在于源目录中的文件（即在新版本中被删除的文件）。
    /// </summary>
    /// <param name="sourcePath">源（旧版本）目录路径。</param>
    /// <param name="targetPath">目标（新版本）目录路径。</param>
    /// <returns>仅存在于源目录中的 <see cref="FileNode"/> 可枚举集合；无差异时返回空集合。</returns>
    /// <remarks>
    /// 此方法的结果用于在更新包中生成"删除"指令，指示客户端在应用更新时移除这些文件。
    /// </remarks>
    IEnumerable<FileNode>? Except(string sourcePath, string targetPath);

    /// <summary>
    /// 尝试从左侧（源）节点集合中找到与指定新文件对应的旧文件节点。
    /// </summary>
    /// <param name="newFile">来自目标目录的新文件节点。</param>
    /// <param name="leftNodes">来自源目录的所有文件节点集合。</param>
    /// <returns>
    /// 匹配到的旧 <see cref="FileNode"/>；如果未找到匹配则返回 <c>null</c>
    /// （表示该文件为全新文件，应直接复制而非生成差异补丁）。
    /// </returns>
    /// <remarks>
    /// 对于成功匹配的文件，后续会使用差异算法（如 bsdiff）生成二进制补丁；
    /// 对于未匹配的新文件，将直接打包完整的文件内容到更新包中。
    /// 实现应同时校验文件名称和相对路径，确保匹配的准确性。
    /// </remarks>
    FileNode? Match(FileNode newFile, IEnumerable<FileNode> leftNodes);
}
