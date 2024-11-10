using System.Collections.Generic;
using System.Diagnostics;

namespace GeneralUpdate.Common.FileBasic;

    /// <summary>
    /// Simple file binary tree.
    /// </summary>
    public class FileTree
    {
        #region Private Members

        private FileNode _root;

        #endregion Private Members

        #region Constructors

        public FileTree()
        { }

        public FileTree(IEnumerable<FileNode> nodes)
        {
            foreach (var node in nodes) Add(node);
        }

        #endregion Constructors

        #region Public Methods

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

        public void InfixOrder()
        {
            if (_root != null)
            {
                _root.InfixOrder();
            }
            else
            {
                Debug.WriteLine("The binary sort tree is empty and cannot be traversed！");
            }
        }

        public FileNode Search(long id) => _root == null ? null : _root.Search(id);

        public FileNode SearchParent(long id) => _root == null ? null : _root.SearchParent(id);

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
        /// Starting from the root node, recursively compares two different child nodes of the binary tree and nodes that are not included.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="node0"></param>
        /// <param name="nodes"></param>
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

        public FileNode GetRoot() => _root;

        #endregion Public Methods
    }