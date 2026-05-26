using GeneralUpdate.Core.FileSystem;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="ComparisonResult"/>.
/// Covers: default constructor state, AddToLeft/Right/Different, multiple adds, empty adds, immutability via AsReadOnly.
/// </summary>
public class ComparisonResultTests
{
    private static FileNode Node(string name) => new() { Name = name, Path = "/root/" + name, Hash = "abc" };

    #region Constructor / Default State

    [Fact]
    public void Ctor_Default_AllListsEmpty()
    {
        var cr = new ComparisonResult();

        Assert.Empty(cr.LeftNodes);
        Assert.Empty(cr.RightNodes);
        Assert.Empty(cr.DifferentNodes);
    }

    [Fact]
    public void Ctor_Default_AllListsNotNull()
    {
        var cr = new ComparisonResult();

        Assert.NotNull(cr.LeftNodes);
        Assert.NotNull(cr.RightNodes);
        Assert.NotNull(cr.DifferentNodes);
    }

    #endregion

    #region AddToLeft

    [Fact]
    public void AddToLeft_SingleNode_AddedToLeft()
    {
        var cr = new ComparisonResult();
        var node = Node("a.txt");

        cr.AddToLeft(new[] { node });

        Assert.Single(cr.LeftNodes);
        Assert.Equal("a.txt", cr.LeftNodes[0].Name);
    }

    [Fact]
    public void AddToLeft_MultipleCalls_Accumulates()
    {
        var cr = new ComparisonResult();

        cr.AddToLeft(new[] { Node("a.txt") });
        cr.AddToLeft(new[] { Node("b.txt"), Node("c.txt") });

        Assert.Equal(3, cr.LeftNodes.Count);
    }

    [Fact]
    public void AddToLeft_EmptyEnumerable_CountUnchanged()
    {
        var cr = new ComparisonResult();
        cr.AddToLeft(Enumerable.Empty<FileNode>());

        Assert.Empty(cr.LeftNodes);
    }

    [Fact]
    public void AddToLeft_DoesNotAffectRightOrDifferent()
    {
        var cr = new ComparisonResult();
        cr.AddToLeft(new[] { Node("a.txt") });

        Assert.Empty(cr.RightNodes);
        Assert.Empty(cr.DifferentNodes);
    }

    #endregion

    #region AddToRight

    [Fact]
    public void AddToRight_SingleNode_AddedToRight()
    {
        var cr = new ComparisonResult();
        var node = Node("b.txt");

        cr.AddToRight(new[] { node });

        Assert.Single(cr.RightNodes);
        Assert.Equal("b.txt", cr.RightNodes[0].Name);
    }

    [Fact]
    public void AddToRight_MultipleCalls_Accumulates()
    {
        var cr = new ComparisonResult();

        cr.AddToRight(new[] { Node("x.txt") });
        cr.AddToRight(new[] { Node("y.txt") });

        Assert.Equal(2, cr.RightNodes.Count);
    }

    [Fact]
    public void AddToRight_DoesNotAffectLeftOrDifferent()
    {
        var cr = new ComparisonResult();
        cr.AddToRight(new[] { Node("b.txt") });

        Assert.Empty(cr.LeftNodes);
        Assert.Empty(cr.DifferentNodes);
    }

    #endregion

    #region AddDifferent

    [Fact]
    public void AddDifferent_SingleNode_AddedToDifferent()
    {
        var cr = new ComparisonResult();
        var node = Node("c.txt");

        cr.AddDifferent(new[] { node });

        Assert.Single(cr.DifferentNodes);
        Assert.Equal("c.txt", cr.DifferentNodes[0].Name);
    }

    [Fact]
    public void AddDifferent_MultipleCalls_Accumulates()
    {
        var cr = new ComparisonResult();

        cr.AddDifferent(new[] { Node("m.txt"), Node("n.txt") });
        cr.AddDifferent(new[] { Node("o.txt") });

        Assert.Equal(3, cr.DifferentNodes.Count);
    }

    [Fact]
    public void AddDifferent_DoesNotAffectLeftOrRight()
    {
        var cr = new ComparisonResult();
        cr.AddDifferent(new[] { Node("d.txt") });

        Assert.Empty(cr.LeftNodes);
        Assert.Empty(cr.RightNodes);
    }

    #endregion

    #region Combined usage

    [Fact]
    public void AllThreeSections_CanBePopulatedSimultaneously()
    {
        var cr = new ComparisonResult();

        cr.AddToLeft(new[] { Node("only-left.txt") });
        cr.AddToRight(new[] { Node("only-right.txt") });
        cr.AddDifferent(new[] { Node("changed.txt"), Node("also-changed.txt") });

        Assert.Single(cr.LeftNodes);
        Assert.Single(cr.RightNodes);
        Assert.Equal(2, cr.DifferentNodes.Count);
    }

    #endregion

    #region ReadOnly properties

    [Fact]
    public void LeftNodes_IsReadOnly()
    {
        var cr = new ComparisonResult();
        cr.AddToLeft(new[] { Node("a.txt") });

        Assert.True(((System.Collections.IList)cr.LeftNodes).IsReadOnly);
    }

    [Fact]
    public void RightNodes_IsReadOnly()
    {
        var cr = new ComparisonResult();
        cr.AddToRight(new[] { Node("b.txt") });

        Assert.True(((System.Collections.IList)cr.RightNodes).IsReadOnly);
    }

    [Fact]
    public void DifferentNodes_IsReadOnly()
    {
        var cr = new ComparisonResult();
        cr.AddDifferent(new[] { Node("c.txt") });

        Assert.True(((System.Collections.IList)cr.DifferentNodes).IsReadOnly);
    }

    #endregion
}
