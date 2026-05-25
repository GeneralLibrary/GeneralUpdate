using GeneralUpdate.Core.Pipeline;

namespace CoreTest.Pipeline;

public class PipelineContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_InvalidKey_ThrowsArgumentException(string key)
    {
        var ctx = new PipelineContext();
        var ex = Assert.Throws<ArgumentException>(() => ctx.Add(key, 42));
        Assert.Contains("Key", ex.Message);
    }

    [Fact]
    public void AddAndGet_ValueType_RoundTrip()
    {
        var ctx = new PipelineContext();
        ctx.Add("intKey", 42);
        Assert.Equal(42, ctx.Get<int>("intKey"));
    }

    [Fact]
    public void AddAndGet_ReferenceType_RoundTrip()
    {
        var ctx = new PipelineContext();
        ctx.Add("strKey", "hello");
        Assert.Equal("hello", ctx.Get<string>("strKey"));
    }

    [Fact]
    public void AddAndGet_NullValue_RoundTrip()
    {
        var ctx = new PipelineContext();
        ctx.Add<string>("nullKey", null);
        Assert.Null(ctx.Get<string>("nullKey"));
    }

    [Fact]
    public void Get_WrongType_ReturnsDefault()
    {
        var ctx = new PipelineContext();
        ctx.Add("key", 42);
        var result = ctx.Get<string>("key");
        Assert.Null(result);
    }

    [Fact]
    public void Get_KeyNotFound_ReturnsDefault()
    {
        var ctx = new PipelineContext();
        Assert.Equal(0, ctx.Get<int>("nonexistent"));
        Assert.Null(ctx.Get<string>("nonexistent"));
    }

    [Fact]
    public void Add_OverwritesExistingKey()
    {
        var ctx = new PipelineContext();
        ctx.Add("key", 1);
        ctx.Add("key", 2);
        Assert.Equal(2, ctx.Get<int>("key"));
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        var ctx = new PipelineContext();
        ctx.Add("key", 42);
        Assert.True(ctx.Remove("key"));
        Assert.False(ctx.ContainsKey("key"));
    }

    [Fact]
    public void Remove_NonexistentKey_ReturnsFalse()
    {
        var ctx = new PipelineContext();
        Assert.False(ctx.Remove("nope"));
    }

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        var ctx = new PipelineContext();
        ctx.Add("key", 1);
        Assert.True(ctx.ContainsKey("key"));
    }

    [Fact]
    public void ContainsKey_NonexistentKey_ReturnsFalse()
    {
        var ctx = new PipelineContext();
        Assert.False(ctx.ContainsKey("missing"));
    }
}
