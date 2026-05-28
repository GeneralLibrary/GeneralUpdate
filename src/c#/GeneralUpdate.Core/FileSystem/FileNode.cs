using System;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 文件节点类，表示文件树（二叉排序树）中的一个节点。
/// 同时作为文件快照的基本数据单元，存储文件的元数据信息和树形结构引用。
/// </summary>
/// <remarks>
/// <para>
/// FileNode 在 GeneralUpdate 中有两个核心用途：
/// </para>
/// <list type="bullet">
///   <item><description><b>文件元数据容器</b>：记录文件的名称、完整路径、相对路径、SHA-256 哈希值等。</description></item>
///   <item><description><b>二叉排序树节点</b>：通过 <see cref="Id"/> 作为排序键构建二叉排序树，
///   支持 <see cref="Add"/>、<see cref="Search"/> 和 <see cref="SearchParent"/> 等树操作。</description></item>
/// </list>
/// <para>
/// <see cref="Equals"/> 方法基于 <see cref="Hash"/> 和 <see cref="Name"/> 进行不区分大小写的比较，
/// 用于 <see cref="FileTree.Compare"/> 算法中判断两个节点是否代表同一个文件。
/// </para>
/// </remarks>
public class FileNode
{
    #region Public Properties

    /// <summary>
    /// 文件节点的唯一标识符（二叉排序树的排序键）。
    /// </summary>
    /// <remarks>由 <see cref="StorageManager.GetId"/> 方法以线程安全的方式自增分配。</remarks>
    public long Id { get; set; }

    /// <summary>
    /// 文件名称（不含路径部分）。
    /// </summary>
    /// <example><c>example.dll</c></example>
    public string Name { get; set; }

    /// <summary>
    /// 文件的完整绝对路径。
    /// </summary>
    /// <example><c>C:\App\bin\example.dll</c></example>
    public string FullName { get; set; }

    /// <summary>
    /// 文件所在目录的路径。
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 文件的 SHA-256 哈希值（十六进制字符串形式）。
    /// </summary>
    /// <remarks>
    /// 用于文件内容比较和完整性验证。由 <see cref="Sha256HashAlgorithm"/> 计算。
    /// </remarks>
    public string Hash { get; set; }

    /// <summary>
    /// 二叉排序树的左子节点引用（存储 Id 小于当前节点的节点）。
    /// </summary>
    public FileNode Left { get; set; }

    /// <summary>
    /// 二叉排序树的右子节点引用（存储 Id 大于等于当前节点的节点）。
    /// </summary>
    public FileNode Right { get; set; }

    /// <summary>
    /// 左侧树中节点的类型标记（保留字段，用于差异分析中的分类标记）。
    /// </summary>
    public int LeftType { get; set; }

    /// <summary>
    /// 右侧树中节点的类型标记（保留字段，用于差异分析中的分类标记）。
    /// </summary>
    public int RightType { get; set; }

    /// <summary>
    /// 文件相对于根目录的相对路径。
    /// </summary>
    /// <example><c>bin/example.dll</c></example>
    /// <remarks>
    /// 使用 URI 相对路径格式（使用正斜杠 <c>/</c> 作为目录分隔符），确保跨平台兼容性。
    /// 此属性在 <see cref="StorageManager.ReadFileNode"/> 中通过 <c>Uri.MakeRelativeUri</c> 计算。
    /// </remarks>
    public string RelativePath { get; set; }

    #endregion Public Properties

    #region Constructors

    /// <summary>
    /// 初始化 <see cref="FileNode"/> 的新实例，所有属性使用默认值。
    /// </summary>
    public FileNode()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FileNode"/> 的新实例并设置节点 ID。
    /// </summary>
    /// <param name="id">文件节点的唯一标识符。</param>
    public FileNode(int id)
    {
        Id = id;
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// 向以当前节点为根的二叉排序树中添加一个新节点。
    /// </summary>
    /// <param name="node">要添加的 <see cref="FileNode"/> 实例。</param>
    /// <remarks>
    /// <para>
    /// 添加规则：
    /// <list type="bullet">
    ///   <item><description>如果新节点的 <c>Id</c> 小于当前节点的 <c>Id</c>，递归插入到左子树。</description></item>
    ///   <item><description>如果新节点的 <c>Id</c> 大于等于当前节点的 <c>Id</c>，递归插入到右子树。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 如果 <paramref name="node"/> 为 <c>null</c>，不执行任何操作。
    /// </para>
    /// </remarks>
    public void Add(FileNode node)
    {
        if (node == null) return;

        if (node.Id < Id)
        {
            if (Left == null)
            {
                Left = node;
            }
            else
            {
                Left.Add(node);
            }
        }
        else
        {
            if (Right == null)
            {
                Right = node;
            }
            else
            {
                Right.Add(node);
            }
        }
    }

    /// <summary>
    /// 在以当前节点为根的二叉排序树中搜索指定 ID 的节点。
    /// </summary>
    /// <param name="id">要搜索的节点 ID。</param>
    /// <returns>
    /// 如果找到则返回对应的 <see cref="FileNode"/> 实例；否则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 利用二叉排序树的特性进行二分查找，平均时间复杂度为 O(log n)：
    /// <list type="bullet">
    ///   <item><description>如果 <paramref name="id"/> 等于当前节点 ID，返回当前节点。</description></item>
    ///   <item><description>如果 <paramref name="id"/> 小于当前节点 ID，递归搜索左子树。</description></item>
    ///   <item><description>如果 <paramref name="id"/> 大于当前节点 ID，递归搜索右子树。</description></item>
    /// </list>
    /// </remarks>
    public FileNode Search(long id)
    {
        if (id == Id)
        {
            return this;
        }
        else if (id < Id)
        {
            if (Left == null) return null;
            return Left.Search(id);
        }
        else
        {
            if (Right == null) return null;
            return Right.Search(id);
        }
    }

    /// <summary>
    /// 在二叉排序树中查找指定 ID 节点的父节点（用于删除操作）。
    /// </summary>
    /// <param name="id">目标节点的 ID。</param>
    /// <returns>
    /// 如果找到则返回目标节点的父 <see cref="FileNode"/>；否则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 此方法在 <see cref="FileTree.DelNode"/> 中用于定位待删除节点的父节点，
    /// 从而更新父节点的左/右子引用。判断逻辑：
    /// <list type="bullet">
    ///   <item><description>如果当前节点的左子或右子的 ID 等于目标 ID，则当前节点即为父节点。</description></item>
    ///   <item><description>否则，根据 ID 大小关系递归搜索左子树或右子树。</description></item>
    /// </list>
    /// </remarks>
    public FileNode SearchParent(long id)
    {
        if (Left != null && Left.Id == id || Right != null && Right.Id == id)
        {
            return this;
        }
        else
        {
            if (id < Id && Left != null)
            {
                return Left.SearchParent(id);
            }
            else if (id >= Id && Right != null)
            {
                return Right.SearchParent(id);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Compare tree nodes equally by Hash and file names.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        var tempNode = obj as FileNode;
        if (tempNode == null) throw new ArgumentException(nameof(tempNode));
        return string.Equals(Hash, tempNode.Hash, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Name, tempNode.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() => (Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0) ^ (Hash != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Hash) : 0);

    #endregion Public Methods
}
