using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Differential.ContentProvider.FileTree
{
    public class FileTree
    {
        FileNode root;

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
                Console.WriteLine("二叉排序树为空，不能遍历！");
            }
        }

        public FileNode Search(long id)=> root == null ? null : root.Search(id);

        public FileNode SearchParent(long id)=> root == null ? null : root.SearchParent(id);

        /// <summary>
        /// 1.node 传入的节点当作二叉树排序树的根节点
        /// 2.删除node为根节点的二叉排序树的最小节点
        /// </summary>
        /// <param name="node">以node为根节点二叉排序树的最小节点的值</param>
        /// <returns></returns>
        public long DelRightTreeMin(FileNode node)
        {
            FileNode target = node;
            //循环的查找左节点，就会找到最小值
            while (target.Left != null)
            {
                target = target.Left;
            }
            //这时target指向了最小节点
            DelNode(target.Id);
            return target.Id;
        }

        /// <summary>
        /// 删除节点
        /// </summary>
        /// <param name="id"></param>
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
    }
}
