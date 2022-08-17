using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Versionfile.Tree
{
    /// <summary>
    /// abstract node shared by both B and B+ tree nodes
    /// so that we can use this for common tests across B and B+ tree
    /// </summary>
    internal abstract class BNode<T> where T : IComparable
    {
        /// <summary>
        /// Array Index of this node in parent's Children array
        /// </summary>
        internal int Index;

        internal T[] Keys { get; set; }
        internal int KeyCount;

        //for common unit testing across B and B+ tree
        internal abstract BNode<T> GetParent();
        internal abstract BNode<T>[] GetChildren();

        internal BNode(int maxKeysPerNode)
        {
            Keys = new T[maxKeysPerNode];
        }

        internal int GetMedianIndex()
        {
            return (KeyCount / 2) + 1;
        }
    }

    internal class BplusTreeNode<T> : BNode<T> where T : IComparable
    {
        internal BplusTreeNode<T> Parent { get; set; }
        internal BplusTreeNode<T>[] Children { get; set; }

        internal bool IsLeaf => Children[0] == null;

        internal BplusTreeNode(int maxKeysPerNode, BplusTreeNode<T> parent)
            : base(maxKeysPerNode)
        {

            Parent = parent;
            Children = new BplusTreeNode<T>[maxKeysPerNode + 1];

        }

        /// <summary>
        /// For shared test method accross B and B+ tree
        /// </summary>
        internal override BNode<T> GetParent()
        {
            return Parent;
        }

        /// <summary>
        /// For shared test method accross B and B+ tree
        /// </summary>
        internal override BNode<T>[] GetChildren()
        {
            return Children;
        }

        /// <summary>
        /// Pointer to sibling leaf on left for faster enumeration
        /// </summary>
        public BplusTreeNode<T> Prev { get; set; }

        /// <summary>
        /// Pointer to sibling leaf on right for faster enumeration
        /// </summary>
        public BplusTreeNode<T> Next { get; set; }
    }
}
