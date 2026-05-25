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
}
