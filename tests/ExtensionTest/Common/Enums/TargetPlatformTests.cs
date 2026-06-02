/// <summary>
/// 测试覆盖点：
/// - Flags 特性验证_位运算组合
/// - 枚举值: None=0, Windows=1, Linux=2, MacOS=4, All=7
/// - 位运算 OR 组合: Windows|Linux=3, Windows|MacOS=5
/// - 位运算 AND 检测: (Windows|Linux) & Windows != 0 => true
/// - 位运算 AND 检测: Windows & Linux == 0 => false
/// - None & Any == 0
/// - All 包含所有平台
/// </summary>
namespace GeneralUpdate.Extension.Common.Enums.Tests;

public class TargetPlatformTests
{
    [Fact]
    public void None_值为0()
    {
        Assert.Equal(0, (int)TargetPlatform.None);
    }

    [Fact]
    public void Windows_值为1()
    {
        Assert.Equal(1, (int)TargetPlatform.Windows);
    }

    [Fact]
    public void Linux_值为2()
    {
        Assert.Equal(2, (int)TargetPlatform.Linux);
    }

    [Fact]
    public void MacOS_值为4()
    {
        Assert.Equal(4, (int)TargetPlatform.MacOS);
    }

    [Fact]
    public void All_值为7()
    {
        Assert.Equal(7, (int)TargetPlatform.All);
    }

    [Fact]
    public void All_包含Windows_Linux_MacOS的位或()
    {
        Assert.Equal(TargetPlatform.All, TargetPlatform.Windows | TargetPlatform.Linux | TargetPlatform.MacOS);
    }

    [Fact]
    public void Windows与Linux组合_值为3()
    {
        Assert.Equal(3, (int)(TargetPlatform.Windows | TargetPlatform.Linux));
    }

    [Fact]
    public void Windows与MacOS组合_值为5()
    {
        Assert.Equal(5, (int)(TargetPlatform.Windows | TargetPlatform.MacOS));
    }

    [Theory]
    [InlineData(TargetPlatform.Windows, TargetPlatform.Windows, true)]
    [InlineData(TargetPlatform.Windows, TargetPlatform.Linux, false)]
    [InlineData(TargetPlatform.Linux, TargetPlatform.Windows, false)]
    [InlineData(TargetPlatform.Linux, TargetPlatform.Linux, true)]
    [InlineData(TargetPlatform.Windows | TargetPlatform.Linux, TargetPlatform.Windows, true)]
    [InlineData(TargetPlatform.Windows | TargetPlatform.Linux, TargetPlatform.Linux, true)]
    [InlineData(TargetPlatform.Windows | TargetPlatform.Linux, TargetPlatform.MacOS, false)]
    [InlineData(TargetPlatform.All, TargetPlatform.Windows, true)]
    [InlineData(TargetPlatform.All, TargetPlatform.Linux, true)]
    [InlineData(TargetPlatform.All, TargetPlatform.MacOS, true)]
    [InlineData(TargetPlatform.None, TargetPlatform.Windows, false)]
    [InlineData(TargetPlatform.None, TargetPlatform.All, false)]
    [InlineData(TargetPlatform.Windows, TargetPlatform.None, false)]
    public void 位与运算检测平台支持(TargetPlatform platforms, TargetPlatform check, bool expected)
    {
        bool result = (platforms & check) != 0;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Any与None位与结果为0()
    {
        Assert.Equal(0, (int)(TargetPlatform.Windows & TargetPlatform.None));
        Assert.Equal(0, (int)(TargetPlatform.All & TargetPlatform.None));
    }
}
