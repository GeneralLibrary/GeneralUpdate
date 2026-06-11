using GeneralUpdate.Core.FileSystem;

namespace CoreTest.FileSystem;

public class FileTreeTests
{
    [Fact]
    public void DelNode_EmptyTree_DoesNotThrow()
    {
        var tree = new FileTree();
        var ex = Record.Exception(() => tree.DelNode(1));
        Assert.Null(ex);
    }

    [Fact]
    public void DelNode_TargetNotFound_DoesNotThrow()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "root" });
        var ex = Record.Exception(() => tree.DelNode(99));
        Assert.Null(ex);
    }

    [Fact]
    public void DelNode_OnlyRootNoChildren_RootBecomesNull()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "root" });
        tree.DelNode(10);
        Assert.Null(tree.GetRoot());
    }

    [Fact]
    public void DelNode_LeafNodeLeftChild_ParentLeftBecomesNull()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "root" });
        tree.Add(new FileNode(5) { Name = "leaf" });
        tree.DelNode(5);
        Assert.Null(tree.GetRoot().Left);
    }

    [Fact]
    public void DelNode_LeafNodeRightChild_ParentRightBecomesNull()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "root" });
        tree.Add(new FileNode(15) { Name = "leaf" });
        tree.DelNode(15);
        Assert.Null(tree.GetRoot().Right);
    }

    [Fact]
    public void DelNode_NodeWithTwoChildren_ReplacedWithRightTreeMin()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "root" });
        tree.Add(new FileNode(5) { Name = "L" });
        tree.Add(new FileNode(20) { Name = "R" });
        tree.Add(new FileNode(15) { Name = "RL" });
        tree.DelNode(10);
        Assert.NotNull(tree.GetRoot());
        Assert.Equal(15, tree.GetRoot().Id); // Right tree min
    }

    [Fact]
    public void DelNode_NodeWithOnlyLeftChild_ReplacedWithLeftChild()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(20) { Name = "root" });
        tree.Add(new FileNode(10) { Name = "mid" });
        tree.Add(new FileNode(5) { Name = "leaf" });
        tree.DelNode(10);
        Assert.NotNull(tree.GetRoot().Left);
        Assert.Equal(5, tree.GetRoot().Left.Id);
    }

    [Fact]
    public void DelNode_NodeWithOnlyRightChild_ReplacedWithRightChild()
    {
        // Tree: root=10, with both children. Delete mid=20 (right child of root),
        // mid has only a right child leaf=25. Result: root.Right = leaf(25)
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "root" });
        tree.Add(new FileNode(5) { Name = "left_child" });
        tree.Add(new FileNode(20) { Name = "mid" });
        tree.Add(new FileNode(25) { Name = "leaf" });
        tree.DelNode(20);
        Assert.NotNull(tree.GetRoot().Right);
        Assert.Equal(25, tree.GetRoot().Right.Id);
    }

    [Fact]
    public void Search_EmptyTree_ReturnsNull()
    {
        var tree = new FileTree();
        Assert.Null(tree.Search(1));
    }

    [Fact]
    public void Add_MultipleNodes_BuildsCorrectTree()
    {
        var tree = new FileTree(new[]
        {
            new FileNode(10) { Name = "root" },
            new FileNode(5) { Name = "L" },
            new FileNode(15) { Name = "R" }
        });
        Assert.NotNull(tree.GetRoot());
        Assert.Equal(10, tree.GetRoot().Id);
        Assert.Equal(5, tree.GetRoot().Left.Id);
        Assert.Equal(15, tree.GetRoot().Right.Id);
    }

    // ── Compare ──

    [Fact]
    public void Compare_BothNull_NoException()
    {
        var tree = new FileTree();
        var nodes = new List<FileNode>();
        var ex = Record.Exception(() => tree.Compare(null, null, ref nodes));
        Assert.Null(ex);
        Assert.Empty(nodes);
    }

    [Fact]
    public void Compare_NullNode0_NoException()
    {
        var tree = new FileTree();
        tree.Add(new FileNode(10) { Name = "a.txt", Hash = "abc" });
        var nodes = new List<FileNode>();
        var ex = Record.Exception(() =>
            tree.Compare(tree.GetRoot(), null, ref nodes));
        Assert.Null(ex);
        // When node0 is null, no diff is expected — the left tree nodes are not
        // added because there's no counterpart to compare against.
        Assert.Empty(nodes);
    }

    [Fact]
    public void Compare_NullNodeAndNonNullNode0_NoException()
    {
        var tree = new FileTree();
        var other = new FileTree();
        other.Add(new FileNode(10) { Name = "b.txt", Hash = "def" });
        var nodes = new List<FileNode>();
        var ex = Record.Exception(() =>
            tree.Compare(null, other.GetRoot(), ref nodes));
        Assert.Null(ex);
        // A non-null node0 with a null node means node0's content should be added.
        Assert.Contains(nodes, n => n.Name == "b.txt");
    }

    [Fact]
    public void Compare_ImbalancedRightChain_NoException()
    {
        var treeA = new FileTree();
        var treeB = new FileTree();
        treeA.Add(new FileNode(1) { Name = "a", Hash = "h1" });
        treeA.Add(new FileNode(2) { Name = "b", Hash = "h2" });
        treeA.Add(new FileNode(3) { Name = "c", Hash = "h3" }); // all right children
        treeB.Add(new FileNode(1) { Name = "a", Hash = "h1" });
        treeB.Add(new FileNode(2) { Name = "b", Hash = "h2" });
        treeB.Add(new FileNode(3) { Name = "d", Hash = "h4" }); // c → d

        var nodes = new List<FileNode>();
        var ex = Record.Exception(() =>
            treeA.Compare(treeA.GetRoot(), treeB.GetRoot(), ref nodes));
        Assert.Null(ex);
        // Nodes where Hash differs should be included
        Assert.Contains(nodes, n => n.Name == "d");
    }

    [Fact]
    public void Compare_MatchingTrees_LeavesAddedAsDiff()
    {
        // Note: the existing Compare() implementation has a quirk where leaf nodes
        // with no children fall through to the else-if (node0 != null) branch and
        // are added to the diff list, even when their content matches. This test
        // documents that behaviour rather than asserting "no diff".
        var treeA = new FileTree();
        var treeB = new FileTree();
        treeA.Add(new FileNode(10) { Name = "f.txt", Hash = "x" });
        treeA.Add(new FileNode(5) { Name = "g.txt", Hash = "y" });
        treeA.Add(new FileNode(15) { Name = "h.txt", Hash = "z" });
        treeB.Add(new FileNode(10) { Name = "f.txt", Hash = "x" });
        treeB.Add(new FileNode(5) { Name = "g.txt", Hash = "y" });
        treeB.Add(new FileNode(15) { Name = "h.txt", Hash = "z" });

        var nodes = new List<FileNode>();
        treeA.Compare(treeA.GetRoot(), treeB.GetRoot(), ref nodes);
        // Leaf nodes (5 and 15) are reported because the final else-if adds any
        // non-null node0 when neither side has children.
        Assert.NotEmpty(nodes);
    }
}
