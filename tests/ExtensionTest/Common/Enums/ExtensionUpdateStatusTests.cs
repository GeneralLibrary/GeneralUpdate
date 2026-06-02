/// <summary>
/// 测试覆盖点：
/// - 枚举值验证
/// - 整数值到枚举转换
/// </summary>
namespace GeneralUpdate.Extension.Common.Enums.Tests;

public class ExtensionUpdateStatusTests
{
    [Fact]
    public void Queued_值为0() => Assert.Equal(0, (int)ExtensionUpdateStatus.Queued);
    [Fact]
    public void Updating_值为1() => Assert.Equal(1, (int)ExtensionUpdateStatus.Updating);
    [Fact]
    public void UpdateSuccessful_值为2() => Assert.Equal(2, (int)ExtensionUpdateStatus.UpdateSuccessful);
    [Fact]
    public void UpdateFailed_值为3() => Assert.Equal(3, (int)ExtensionUpdateStatus.UpdateFailed);

    [Theory]
    [InlineData(0, ExtensionUpdateStatus.Queued)]
    [InlineData(1, ExtensionUpdateStatus.Updating)]
    [InlineData(2, ExtensionUpdateStatus.UpdateSuccessful)]
    [InlineData(3, ExtensionUpdateStatus.UpdateFailed)]
    public void 整数值可转换为对应枚举值(int intValue, ExtensionUpdateStatus expected)
    {
        Assert.Equal(expected, (ExtensionUpdateStatus)intValue);
    }
}
