/// <summary>
/// 测试覆盖点：
/// - 构造函数
///   - 传入 IPlatformServices => 使用传入的实例
///   - 不传入 IPlatformServices => 使用默认 RuntimePlatformServices
/// - GetCurrentPlatform() 委托给 _platformServices
/// - IsCurrentPlatformSupported(extension)
///   - extension.SupportedPlatforms 包含当前平台 => true
///   - extension.SupportedPlatforms 不包含当前平台 => false
///   - extension.SupportedPlatforms = None => false
///   - extension.SupportedPlatforms = All => true
/// - IsPlatformSupported(extension, platform)
///   - 位运算 AND 检查各种组合
/// </summary>
using Moq;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Compatibility.Tests;

public class PlatformMatcherTests
{
    // ===== 构造函数测试 =====

    [Fact]
    public void 构造函数_传入PlatformServices_使用传入实例()
    {
        var mock = new Mock<IPlatformServices>();
        mock.Setup(m => m.GetCurrentPlatform()).Returns(TargetPlatform.Linux);
        var matcher = new PlatformMatcher(mock.Object);
        Assert.Equal(TargetPlatform.Linux, matcher.GetCurrentPlatform());
    }

    [Fact]
    public void 构造函数_不传入参数_使用默认RuntimePlatformServices()
    {
        var matcher = new PlatformMatcher();
        var platform = matcher.GetCurrentPlatform();
        Assert.NotEqual(TargetPlatform.None, platform); // 必然运行在某平台上
    }

    // ===== GetCurrentPlatform 测试 =====

    [Fact]
    public void GetCurrentPlatform_委托给注入的PlatformServices()
    {
        var mock = new Mock<IPlatformServices>();
        mock.Setup(m => m.GetCurrentPlatform()).Returns(TargetPlatform.Windows);
        var matcher = new PlatformMatcher(mock.Object);

        Assert.Equal(TargetPlatform.Windows, matcher.GetCurrentPlatform());
        mock.Verify(m => m.GetCurrentPlatform(), Times.Once);
    }

    // ===== IsCurrentPlatformSupported 测试 =====

    [Fact]
    public void IsCurrentPlatformSupported_扩展支持当前平台_返回true()
    {
        var mock = new Mock<IPlatformServices>();
        mock.Setup(m => m.GetCurrentPlatform()).Returns(TargetPlatform.Windows);
        var matcher = new PlatformMatcher(mock.Object);
        var ext = new ExtensionMetadata { SupportedPlatforms = TargetPlatform.Windows };

        Assert.True(matcher.IsCurrentPlatformSupported(ext));
    }

    [Fact]
    public void IsCurrentPlatformSupported_扩展不支持当前平台_返回false()
    {
        var mock = new Mock<IPlatformServices>();
        mock.Setup(m => m.GetCurrentPlatform()).Returns(TargetPlatform.Windows);
        var matcher = new PlatformMatcher(mock.Object);
        var ext = new ExtensionMetadata { SupportedPlatforms = TargetPlatform.Linux };

        Assert.False(matcher.IsCurrentPlatformSupported(ext));
    }

    [Fact]
    public void IsCurrentPlatformSupported_当前平台None_扩展为All_返回false()
    {
        var mock = new Mock<IPlatformServices>();
        mock.Setup(m => m.GetCurrentPlatform()).Returns(TargetPlatform.None);
        var matcher = new PlatformMatcher(mock.Object);
        var ext = new ExtensionMetadata { SupportedPlatforms = TargetPlatform.All };

        Assert.False(matcher.IsCurrentPlatformSupported(ext));
    }

    [Fact]
    public void IsCurrentPlatformSupported_当前平台为All_扩展为Windows_返回true()
    {
        var mock = new Mock<IPlatformServices>();
        mock.Setup(m => m.GetCurrentPlatform()).Returns(TargetPlatform.All);
        var matcher = new PlatformMatcher(mock.Object);
        var ext = new ExtensionMetadata { SupportedPlatforms = TargetPlatform.Windows };

        Assert.True(matcher.IsCurrentPlatformSupported(ext));
    }

    // ===== IsPlatformSupported 测试 =====

    [Theory]
    [InlineData(TargetPlatform.Windows, TargetPlatform.Windows, true)]
    [InlineData(TargetPlatform.Windows, TargetPlatform.Linux, false)]
    [InlineData(TargetPlatform.Linux, TargetPlatform.Linux, true)]
    [InlineData(TargetPlatform.Windows | TargetPlatform.Linux, TargetPlatform.Windows, true)]
    [InlineData(TargetPlatform.Windows | TargetPlatform.Linux, TargetPlatform.Linux, true)]
    [InlineData(TargetPlatform.Windows | TargetPlatform.Linux, TargetPlatform.MacOS, false)]
    [InlineData(TargetPlatform.All, TargetPlatform.Windows, true)]
    [InlineData(TargetPlatform.All, TargetPlatform.MacOS, true)]
    [InlineData(TargetPlatform.None, TargetPlatform.Windows, false)]
    public void IsPlatformSupported_各种平台组合验证(TargetPlatform supported, TargetPlatform check, bool expected)
    {
        var matcher = new PlatformMatcher(); // 平台服务不影响此方法
        var ext = new ExtensionMetadata { SupportedPlatforms = supported };
        Assert.Equal(expected, matcher.IsPlatformSupported(ext, check));
    }
}
