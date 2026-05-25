using GeneralUpdate.Core.FileSystem;

namespace CoreTest.FileSystem;

public class FileNodeTests
{
    // ── Add ──

    [Fact]
    public void Add_NullNode_SkippedWithoutException()
    {
        var root = new FileNode(10);
        var ex = Record.Exception(() => root.Add(null));
        Assert.Null(ex);
    }

    [Fact]
    public void Add_SmallerIdWithNullLeft_AssignsDirectly()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(5));
        Assert.NotNull(root.Left);
        Assert.Equal(5, root.Left.Id);
    }

    [Fact]
    public void Add_SmallerIdWithNonNullLeft_Recurses()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(7));
        root.Add(new FileNode(5));
        Assert.Equal(7, root.Left.Id);
        Assert.Equal(5, root.Left.Left.Id);
    }

    [Fact]
    public void Add_EqualOrLargerIdWithNullRight_AssignsDirectly()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(10));
        Assert.NotNull(root.Right);
        Assert.Equal(10, root.Right.Id);
    }

    [Fact]
    public void Add_LargerIdWithNonNullRight_Recurses()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(15));
        root.Add(new FileNode(12));
        Assert.Equal(15, root.Right.Id);
        Assert.Equal(12, root.Right.Left.Id);
    }

    // ── Search ──

    [Fact]
    public void Search_ExactMatch_ReturnsThis()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(5));
        root.Add(new FileNode(15));
        var result = root.Search(10);
        Assert.Same(root, result);
    }

    [Fact]
    public void Search_SmallerIdWithNullLeft_ReturnsNull()
    {
        var root = new FileNode(10);
        var result = root.Search(5);
        Assert.Null(result);
    }

    [Fact]
    public void Search_SmallerIdFoundInLeft_ReturnsNode()
    {
        var root = new FileNode(10);
        var child = new FileNode(5);
        root.Add(child);
        var result = root.Search(5);
        Assert.Same(child, result);
    }

    [Fact]
    public void Search_LargerIdWithNullRight_ReturnsNull()
    {
        var root = new FileNode(10);
        var result = root.Search(15);
        Assert.Null(result);
    }

    [Fact]
    public void Search_LargerIdFoundInRight_ReturnsNode()
    {
        var root = new FileNode(10);
        var child = new FileNode(15);
        root.Add(child);
        var result = root.Search(15);
        Assert.Same(child, result);
    }

    // ── SearchParent ──

    [Fact]
    public void SearchParent_DirectLeftChild_ReturnsThis()
    {
        var root = new FileNode(10);
        var child = new FileNode(5);
        root.Add(child);
        var parent = root.SearchParent(5);
        Assert.Same(root, parent);
    }

    [Fact]
    public void SearchParent_DirectRightChild_ReturnsThis()
    {
        var root = new FileNode(10);
        var child = new FileNode(15);
        root.Add(child);
        var parent = root.SearchParent(15);
        Assert.Same(root, parent);
    }

    [Fact]
    public void SearchParent_DeepLeftChild_ReturnsParent()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(7));
        root.Add(new FileNode(5));
        var parent = root.SearchParent(5);
        Assert.Equal(7, parent.Id);
    }

    [Fact]
    public void SearchParent_DeepRightChild_ReturnsParent()
    {
        var root = new FileNode(10);
        root.Add(new FileNode(15));
        root.Add(new FileNode(12));
        var parent = root.SearchParent(12);
        Assert.Equal(15, parent.Id);
    }

    [Fact]
    public void SearchParent_NotFound_ReturnsNull()
    {
        var root = new FileNode(10);
        var result = root.SearchParent(999);
        Assert.Null(result);
    }

    // ── Equals ──

    [Fact]
    public void Equals_NullObject_ReturnsFalse()
    {
        var node = new FileNode(1) { Name = "test", Hash = "abc" };
        Assert.False(node.Equals(null));
    }

    [Fact]
    public void Equals_NonFileNodeType_ThrowsArgumentException()
    {
        var node = new FileNode(1) { Name = "test", Hash = "abc" };
        Assert.Throws<ArgumentException>(() => node.Equals("not a node"));
    }

    [Fact]
    public void Equals_SameHashAndName_ReturnsTrue()
    {
        var a = new FileNode(1) { Name = "file.dll", Hash = "abc123" };
        var b = new FileNode(2) { Name = "file.dll", Hash = "abc123" };
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentHash_ReturnsFalse()
    {
        var a = new FileNode(1) { Name = "file.dll", Hash = "abc" };
        var b = new FileNode(2) { Name = "file.dll", Hash = "def" };
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentName_ReturnsFalse()
    {
        var a = new FileNode(1) { Name = "file1.dll", Hash = "abc" };
        var b = new FileNode(2) { Name = "file2.dll", Hash = "abc" };
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_CaseInsensitiveHash_ReturnsTrue()
    {
        var a = new FileNode(1) { Name = "file.dll", Hash = "ABC123" };
        var b = new FileNode(2) { Name = "file.dll", Hash = "abc123" };
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_CaseInsensitiveName_ReturnsTrue()
    {
        var a = new FileNode(1) { Name = "FILE.DLL", Hash = "abc" };
        var b = new FileNode(2) { Name = "file.dll", Hash = "abc" };
        Assert.True(a.Equals(b));
    }

    // ── GetHashCode ──

    [Fact]
    public void GetHashCode_SameNameAndHash_SameValue()
    {
        var a = new FileNode(1) { Name = "file.dll", Hash = "abc" };
        var b = new FileNode(2) { Name = "file.dll", Hash = "abc" };
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
