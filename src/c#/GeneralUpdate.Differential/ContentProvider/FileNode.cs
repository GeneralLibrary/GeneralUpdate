using System;
using System.Diagnostics;

namespace GeneralUpdate.Differential.ContentProvider
{
    public class FileNode
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public string MD5 { get; set; }

        public FileNode Left { get; set; }

        public FileNode Right { get; set; }

        public int LeftType { get; set; }

        public int RightType { get; set; }

        public FileNode() { }

        public FileNode(int id)
        {
            Id = id;
        }

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

        public void InfixOrder()
        {
            if (Left != null)
            {
                Left.InfixOrder();
            }
            Debug.WriteLine(this);
            if (Right != null)
            {
                Right.InfixOrder();
            }
        }

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
        /// Find the parent node of the node that you want to delete.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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
        /// Compare tree nodes equally by MD5 and file names.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var tempNode = obj as FileNode;
            return MD5.ToUpper().Equals(tempNode.MD5.ToUpper()) && Name.Equals(tempNode.Name);
        }
    }
}
