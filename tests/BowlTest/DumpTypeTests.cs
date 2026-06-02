using GeneralUpdate.Bowl;

/// <summary>
/// 分支覆盖点：
/// DumpType 枚举：
///   - Full = 0
///   - Mini = 1
///   - Heap = 2
///   - 默认值为 Full
///   - 所有枚举值可显式转换
/// </summary>
public class DumpTypeTests
{
    [Fact]
    public void Full_值为0()
    {
        Assert.Equal(0, (int)DumpType.Full);
    }

    [Fact]
    public void Mini_值为1()
    {
        Assert.Equal(1, (int)DumpType.Mini);
    }

    [Fact]
    public void Heap_值为2()
    {
        Assert.Equal(2, (int)DumpType.Heap);
    }

    [Fact]
    public void 默认值为Full()
    {
        DumpType dt = default;
        Assert.Equal(DumpType.Full, dt);
    }

    [Theory]
    [InlineData(DumpType.Full)]
    [InlineData(DumpType.Mini)]
    [InlineData(DumpType.Heap)]
    public void 枚举值ToString不为空(DumpType dt)
    {
        Assert.NotEmpty(dt.ToString());
    }

    [Theory]
    [InlineData(0, DumpType.Full)]
    [InlineData(1, DumpType.Mini)]
    [InlineData(2, DumpType.Heap)]
    public void 从int显式转换为枚举_正确(int value, DumpType expected)
    {
        var dt = (DumpType)value;
        Assert.Equal(expected, dt);
    }

    [Fact]
    public void 三个枚举值互不相等()
    {
        Assert.NotEqual(DumpType.Full, DumpType.Mini);
        Assert.NotEqual(DumpType.Full, DumpType.Heap);
        Assert.NotEqual(DumpType.Mini, DumpType.Heap);
    }
}
