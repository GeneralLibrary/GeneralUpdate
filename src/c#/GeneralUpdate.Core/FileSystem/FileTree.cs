using System.Collections.Generic;

namespace GeneralUpdate.Core.FileSystem;

    /// <summary>
    /// A simple binary search tree for files, organized by <see cref="FileNode.Id"/> as the sort key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FileTree encapsulates a Binary Search Tree (BST) with the following primary uses:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Organizes a flat list of file nodes into a tree structure to support efficient search and comparison operations.</description></item>
    ///   <item><description>Works with the <see cref="FileTree.Compare"/> method to recursively compare two trees and identify differing nodes.</description></item>
    ///   <item><description>Supports standard BST operations including node addition, search, and deletion.</description></item>
    /// </list>
    /// <para>
    /// This tree is used internally by the <see cref="StorageManager.Compare"/> method to organize file snapshots
    /// of two versions into tree structures before performing recursive difference comparison.
    /// </para>
    /// </remarks>
    public class FileTree
    {
        #region Private Members

        private FileNode _root;

        #endregion Private Members

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTree"/> class with an empty tree.
        /// </summary>
        public FileTree()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTree"/> class with the specified node collection and adds all nodes to the tree.
        /// </summary>
        /// <param name="nodes">The collection of file nodes to add to the tree.</param>
        public FileTree(IEnumerable<FileNode> nodes)
        {
            foreach (var node in nodes) Add(node);
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>
        /// Adds a file node to the tree.
        /// </summary>
        /// <param name="node">The <see cref="FileNode"/> instance to add.</param>
        /// <remarks>
        /// If the tree is empty (root is <c>null</c>), <paramref name="node"/> is set as the root;
        /// otherwise, the insertion is delegated to the root node's <see cref="FileNode.Add"/> method for recursive insertion.
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
        /// Searches the tree for a file node with the specified ID.
        /// </summary>
        /// <param name="id">The node ID to search for.</param>
        /// <returns>The <see cref="FileNode"/> instance if found; otherwise, <c>null</c>.</returns>
        public FileNode Search(long id) => _root == null ? null : _root.Search(id);

        /// <summary>
        /// Searches the tree for the parent node of the node with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the target node.</param>
        /// <returns>The parent <see cref="FileNode"/> of the target node; <c>null</c> if not found.</returns>
        public FileNode SearchParent(long id) => _root == null ? null : _root.SearchParent(id);

        /// <summary>
        /// Deletes the minimum node from the right subtree (used as a replacement operation when deleting a node with two children).
        /// </summary>
        /// <param name="node">The root node of the right subtree from which to find and delete the minimum node.</param>
        /// <returns>The ID of the deleted minimum node.</returns>
        /// <remarks>
        /// In a BST deletion operation, when the node to delete has two children,
        /// the minimum node from the right subtree must be found to replace the node being deleted.
        /// This method performs this operation and returns the ID of the deleted minimum node.
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
        /// Deletes the node with the specified ID from the tree.
        /// </summary>
        /// <param name="id">The ID of the node to delete.</param>
        /// <remarks>
        /// <para>
        /// Standard BST deletion operation handling three cases:
        /// <list type="bullet">
        ///   <item><description><b>Leaf node</b>: Directly sets the parent's corresponding child reference to <c>null</c>.</description></item>
        ///   <item><description><b>Single child</b>: Replaces the node with its child.</description></item>
        ///   <item><description><b>Two children</b>: Finds the minimum node in the right subtree to replace the node, then deletes that minimum node.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// If the tree is empty or the node does not exist, no operation is performed.
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
        /// Recursively compares corresponding child nodes of two binary search trees starting from the specified node, collecting differing nodes.
        /// </summary>
        /// <param name="node">The node from the current tree (left/base).</param>
        /// <param name="node0">The corresponding node from the target tree (right/new version).</param>
        /// <param name="nodes">The list reference used to collect differing nodes.</param>
        /// <remarks>
        /// <para>
        /// Comparison logic:
        /// <list type="bullet">
        ///   <item><description>If the left tree node exists and is not null, recursively compare its left child.</description></item>
        ///   <item><description>If the left tree node is null but the corresponding right tree node exists, treat the right node as added and add it to the diff list.</description></item>
        ///   <item><description>Performs symmetric operations for the right subtree.</description></item>
        ///   <item><description>When corresponding nodes are found to be non-equivalent (compared via <see cref="FileNode.Equals"/> by Hash and Name), the right tree node is added to the diff list.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// This method passes the diff list by <c>ref</c> to avoid frequent object creation during recursion.
        /// </para>
        /// </remarks>
        public void Compare(FileNode node, FileNode node0, ref List<FileNode> nodes)
        {
            if (node != null && node.Left != null)
            {
                if (!node.Equals(node0) && node0 != null) nodes.Add(node0);
                Compare(node.Left, node0?.Left, ref nodes);
            }
            else if (node0 != null && node0.Left != null)
            {
                nodes.Add(node0);
                Compare(node?.Left, node0.Left, ref nodes);
            }

            if (node != null && node.Right != null)
            {
                if (!node.Equals(node0) && node0 != null) nodes.Add(node0);
                Compare(node.Right, node0?.Right, ref nodes);
            }
            else if (node0 != null && node0.Right != null)
            {
                nodes.Add(node0);
                Compare(node?.Right, node0.Right, ref nodes);
            }
            else if (node0 != null)
            {
                nodes.Add(node0);
            }
        }

        /// <summary>
        /// Gets the root node of the tree.
        /// </summary>
        /// <returns>The root <see cref="FileNode"/>; <c>null</c> if the tree is empty.</returns>
        public FileNode GetRoot() => _root;

        #endregion Public Methods
    }