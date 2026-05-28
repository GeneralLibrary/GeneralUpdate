using System.Collections.Generic;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 两个目录比较操作的结果，包含左侧（基准）和右侧（目标）的完整节点列表以及差异节点列表。
/// </summary>
/// <remarks>
/// <para>
/// ComparisonResult 由 <see cref="StorageManager.Compare"/> 方法生成，
/// 它将比较结果组织为三个维度：
/// </para>
/// <list type="bullet">
///   <item><description><see cref="LeftNodes"/>：左侧目录中所有文件的节点列表。</description></item>
///   <item><description><see cref="RightNodes"/>：右侧目录中所有文件的节点列表。</description></item>
///   <item><description><see cref="DifferentNodes"/>：经 <see cref="FileTree.Compare"/> 算法识别出的哈希值
///   或名称存在差异的节点集合（包含新增、修改和删除的文件）。</description></item>
/// </list>
/// </remarks>
public class ComparisonResult
{
    private List<FileNode> _leftNodes;
    private List<FileNode> _rightNodes;
    private List<FileNode> _differentNodes;

    /// <summary>
    /// 初始化 <see cref="ComparisonResult"/> 的新实例，所有内部列表为空。
    /// </summary>
    public ComparisonResult()
    {
        _leftNodes = new List<FileNode>();
        _rightNodes = new List<FileNode>();
        _differentNodes = new List<FileNode>();
    }

    /// <summary>
    /// 获取左侧目录（基准版本）中所有文件的只读列表。
    /// </summary>
    /// <value>左侧文件的 <see cref="FileNode"/> 只读集合。</value>
    public IReadOnlyList<FileNode> LeftNodes => _leftNodes.AsReadOnly();

    /// <summary>
    /// 获取右侧目录（目标版本）中所有文件的只读列表。
    /// </summary>
    /// <value>右侧文件的 <see cref="FileNode"/> 只读集合。</value>
    public IReadOnlyList<FileNode> RightNodes => _rightNodes.AsReadOnly();

    /// <summary>
    /// 获取左右两侧目录之间存在差异的文件的只读列表。
    /// </summary>
    /// <value>差异文件的 <see cref="FileNode"/> 只读集合。</value>
    /// <remarks>
    /// 差异节点是通过对左右两棵 <see cref="FileTree"/> 进行递归比较得到的，
    /// 包括哈希值不同或仅在某一侧存在的文件。
    /// </remarks>
    public IReadOnlyList<FileNode> DifferentNodes => _differentNodes.AsReadOnly();

    /// <summary>
    /// 向左侧节点列表中添加文件节点。
    /// </summary>
    /// <param name="files">要添加的文件节点集合。</param>
    public void AddToLeft(IEnumerable<FileNode> files) => _leftNodes.AddRange(files);

    /// <summary>
    /// 向右侧节点列表中添加文件节点。
    /// </summary>
    /// <param name="files">要添加的文件节点集合。</param>
    public void AddToRight(IEnumerable<FileNode> files) => _rightNodes.AddRange(files);

    /// <summary>
    /// 向差异节点列表中添加文件节点。
    /// </summary>
    /// <param name="files">要添加的差异文件节点集合。</param>
    public void AddDifferent(IEnumerable<FileNode> files) => _differentNodes.AddRange(files);
}