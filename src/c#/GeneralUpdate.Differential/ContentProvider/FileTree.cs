using System.Collections.Generic;
using System.Diagnostics;

namespace GeneralUpdate.Differential.ContentProvider
{
    public class FileTree
    {
        FileNode root;

        public FileTree() { }

        public FileTree(IEnumerable<FileNode> nodes) 
        {
            foreach (var node in nodes) Add(node);
        }

        public void Add(FileNode node)
        {
            if (root == null)
            {
                root = node;
            }
            else
            {
                root.Add(node);
            }
        }

        public void InfixOrder()
        {
            if (root != null)
            {
                root.InfixOrder();
            }
            else
            {
                Debug.WriteLine("The binary sort tree is empty and cannot be traversed！");
            }
        }

        public FileNode Search(long id) => root == null ? null : root.Search(id);

        public FileNode SearchParent(long id) => root == null ? null : root.SearchParent(id);

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
            if (root == null)
            {
                return;
            }
            else
            {
                //先找到需要删除的节点targetnode
                FileNode targetNode = Search(id);
                //如果没有找到要删除的节点
                if (targetNode == null)
                {
                    return;
                }
                //如果我们发现当前这颗二叉排序树只有一个节点
                if (root.Left == null && root.Right == null)
                {
                    root = null;
                    return;
                }

                //找到targetnode的父节点
                FileNode parent = SearchParent(id);
                //如果要删除的节点是叶子节点
                if (targetNode.Left == null && targetNode.Right == null)
                {
                    //判断targetnode是父节点的左子节点，还是右子节点
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
                    //左右子树不为空的时候
                    long minVal = DelRightTreeMin(targetNode.Right);
                    targetNode.Id = minVal;
                }
                else
                {
                    //删除只有一棵树的节点
                    //如果要删除的节点有左子节点
                    if (targetNode.Left != null)
                    {
                        //如果targetnode是parent 的左子节点
                        if (parent.Left.Id == id)
                        {
                            parent.Left = targetNode.Left;
                        }
                        else
                        {
                            //targetNode是parent的右子节点
                            parent.Right = targetNode.Left;
                        }
                    }
                    else
                    {
                        //如果targetnode是parent的左子节点
                        if (parent.Left.Id == id)
                        {
                            parent.Left = targetNode.Right;
                        }
                        else
                        {
                            //targetNode是parent的右子节点
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
                if (!node.Equals(node0)) nodes.Add(node0);
                Compare(node.Left, node0.Left, ref nodes);
            }
            else if (node0.Left != null)
            {
                nodes.Add(node0);
                Compare(node.Right, node0.Right, ref nodes);
            }

            if (node != null && node.Right != null)
            {
                if (!node.Equals(node0)) nodes.Add(node0);
                Compare(node.Right, node0.Right, ref nodes);
            }
            else if (node0.Right != null)
            {
                nodes.Add(node0);
                Compare(node == null ? null : node.Right, node0.Right, ref nodes);
            }
            else
            {
                nodes.Add(node0);
            }
        }

        public FileNode GetRoot() => root;
    }
}
