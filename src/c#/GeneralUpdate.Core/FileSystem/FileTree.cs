using System.Collections.Generic;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.FileSystem;

    /// <summary>
    /// 简单的文件二叉排序树，以 <see cref="FileNode.Id"/> 为排序键组织文件节点。
    /// </summary>
    /// <remarks>
    /// <para>
    /// FileTree 封装了一个二叉排序树（Binary Search Tree），主要用途：
    /// </para>
    /// <list type="bullet">
    ///   <item><description>将扁平的文件节点列表组织为树形结构以支持高效的搜索和比较操作。</description></item>
    ///   <item><description>配合 <see cref="FileTree.Compare"/> 方法对两棵树进行递归比较，识别差异节点。</description></item>
    ///   <item><description>支持节点的添加、搜索、删除等标准二叉排序树操作。</description></item>
    /// </list>
    /// <para>
    /// 此树由 <see cref="StorageManager.Compare"/> 方法内部使用，用于将两个版本的
    /// 文件快照组织为树形结构后进行递归差异比较。
    /// </para>
    /// </remarks>
    public class FileTree
    {
        #region Private Members

        private FileNode _root;

        #endregion Private Members

        #region Constructors

        /// <summary>
        /// 初始化 <see cref="FileTree"/> 的新实例，树为空。
        /// </summary>
        public FileTree()
        { }

        /// <summary>
        /// 使用指定的节点集合初始化 <see cref="FileTree"/>，并将所有节点添加到树中。
        /// </summary>
        /// <param name="nodes">要添加到树中的文件节点集合。</param>
        public FileTree(IEnumerable<FileNode> nodes)
        {
            foreach (var node in nodes) Add(node);
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>
        /// 向树中添加一个文件节点。
        /// </summary>
        /// <param name="node">要添加的 <see cref="FileNode"/> 实例。</param>
        /// <remarks>
        /// 如果树为空（根节点为 <c>null</c>），则将 <paramref name="node"/> 设为根节点；
        /// 否则委托给根节点的 <see cref="FileNode.Add"/> 方法进行递归插入。
        /// </remarks>
        public void Add(FileNode node)
        {
            if (_root == null)
            {
                _root = node;
            }
            else
            {
                _root.Add(node);
            }
        }

        /// <summary>
        /// 在树中搜索指定 ID 的文件节点。
        /// </summary>
        /// <param name="id">要搜索的节点 ID。</param>
        /// <returns>找到的 <see cref="FileNode"/> 实例；如果未找到则返回 <c>null</c>。</returns>
        public FileNode Search(long id) => _root == null ? null : _root.Search(id);

        /// <summary>
        /// 在树中搜索指定 ID 节点的父节点。
        /// </summary>
        /// <param name="id">目标节点的 ID。</param>
        /// <returns>目标节点的父 <see cref="FileNode"/>；如果未找到则返回 <c>null</c>。</returns>
        public FileNode SearchParent(long id) => _root == null ? null : _root.SearchParent(id);

        /// <summary>
        /// 删除右子树中的最小节点（用于删除具有两个子节点的节点时的替换操作）。
        /// </summary>
        /// <param name="node">要从中查找并删除最小节点的右子树根节点。</param>
        /// <returns>被删除的最小节点的 ID。</returns>
        /// <remarks>
        /// 在二叉排序树的删除操作中，当待删除节点有两个子节点时，
        /// 需要找到右子树中的最小节点来替换待删除节点。此方法执行此操作并返回被删除的最小节点的 ID。
        /// </remarks>
        public long DelRightTreeMin(FileNode node)
        {
            FileNode target = node;
            while (target.Left != null)
            {
                target = target.Left;
            }
            DelNode(target.Id);
            return target.Id;
        }

        /// <summary>
        /// 从树中删除指定 ID 的节点。
        /// </summary>
        /// <param name="id">要删除的节点 ID。</param>
        /// <remarks>
        /// <para>
        /// 标准的二叉排序树删除操作，包含三种情况：
        /// <list type="bullet">
        ///   <item><description><b>叶子节点</b>：直接将其父节点的对应子引用置为 <c>null</c>。</description></item>
        ///   <item><description><b>只有一个子节点</b>：用子节点替换待删除节点。</description></item>
        ///   <item><description><b>有两个子节点</b>：找到右子树的最小节点替换待删除节点，然后删除该最小节点。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 如果树为空或节点不存在，不执行任何操作。
        /// </para>
        /// </remarks>
        public void DelNode(long id)
        {
            if (_root == null)
            {
                return;
            }
            else
            {
                FileNode targetNode = Search(id);
                if (targetNode == null)
                {
                    return;
                }
                if (_root.Left == null && _root.Right == null)
                {
                    _root = null;
                    return;
                }

                FileNode parent = SearchParent(id);
                if (targetNode.Left == null && targetNode.Right == null)
                {
                    if (parent.Left != null && parent.Left.Id == id)
                    {
                        parent.Left = null;
                    }
                    else if (parent.Right != null && parent.Right.Id == id)
                    {
                        parent.Right = null;
                    }
                }
                else if (targetNode.Left != null && targetNode.Right != null)
                {
                    long minVal = DelRightTreeMin(targetNode.Right);
                    targetNode.Id = minVal;
                }
                else
                {
                    if (targetNode.Left != null)
                    {
                        if (parent.Left.Id == id)
                        {
                            parent.Left = targetNode.Left;
                        }
                        else
                        {
                            parent.Right = targetNode.Left;
                        }
                    }
                    else
                    {
                        if (parent.Left.Id == id)
                        {
                            parent.Left = targetNode.Right;
                        }
                        else
                        {
                            parent.Right = targetNode.Right;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 从指定节点开始，递归比较两棵二叉排序树的对应子节点，收集差异节点。
        /// </summary>
        /// <param name="node">当前树（左侧/基准）的节点。</param>
        /// <param name="node0">目标树（右侧/新版本）的对应节点。</param>
        /// <param name="nodes">用于收集差异节点的列表引用。</param>
        /// <remarks>
        /// <para>
        /// 比较逻辑：
        /// <list type="bullet">
        ///   <item><description>如果左树节点存在且不为空，递归比较其左子节点。</description></item>
        ///   <item><description>如果左树节点为空但右树对应节点存在，将右树节点视为新增加入差异列表。</description></item>
        ///   <item><description>对于右子树执行对称的操作。</description></item>
        ///   <item><description>当发现对应节点不等价（通过 <see cref="FileNode.Equals"/> 比较 Hash 和 Name）时，将右树节点加入差异列表。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 此方法通过 <c>ref</c> 传递差异列表，避免在递归过程中频繁创建新对象。
        /// </para>
        /// </remarks>
        public void Compare(FileNode node, FileNode node0, ref List<FileNode> nodes)
        {
            if (node != null && node.Left != null)
            {
                if (!node.Equals(node0) && node0 != null) nodes.Add(node0);
                Compare(node.Left, node0.Left, ref nodes);
            }
            else if (node0 != null && node0.Left != null)
            {
                nodes.Add(node0);
                Compare(node.Left, node0.Left, ref nodes);
            }

            if (node != null && node.Right != null)
            {
                if (!node.Equals(node0) && node0 != null) nodes.Add(node0);
                Compare(node.Right, node0 == null ? null : node0.Right, ref nodes);
            }
            else if (node0 != null && node0.Right != null)
            {
                nodes.Add(node0);
                Compare(node == null ? null : node.Right, node0.Right, ref nodes);
            }
            else if (node0 != null)
            {
                nodes.Add(node0);
            }
        }

        /// <summary>
        /// 获取树的根节点。
        /// </summary>
        /// <returns>根 <see cref="FileNode"/>；如果树为空则返回 <c>null</c>。</returns>
        public FileNode GetRoot() => _root;

        #endregion Public Methods
    }