using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Strategies;

/// <summary>
/// 分支覆盖点：
/// StrategyFactory.Create()：
///   - Windows 平台 → 返回 WindowsBowlStrategy
///   - Linux 平台 → 返回 LinuxBowlStrategy
///   - macOS 平台 → 返回 MacBowlStrategy
///   - 其他平台 → 抛出 PlatformNotSupportedException
/// </summary>
public class StrategyFactoryTests
{
    [Fact]
    public void Create_返回非null_IBowlStrategy()
    {
        // On any supported platform (Win/Lin/Mac), Create should not return null
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var strategy = StrategyFactory.Create();
            Assert.NotNull(strategy);
            Assert.IsAssignableFrom<IBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_在Windows上返回WindowsBowlStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var strategy = StrategyFactory.Create();
            Assert.IsType<WindowsBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_在Linux上返回LinuxBowlStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var strategy = StrategyFactory.Create();
            Assert.IsType<LinuxBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_在macOS上返回MacBowlStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var strategy = StrategyFactory.Create();
            Assert.IsType<MacBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_支持平台_不抛出异常()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var exception = Record.Exception(() => StrategyFactory.Create());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void Create_多次调用_返回不同实例()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var s1 = StrategyFactory.Create();
            var s2 = StrategyFactory.Create();
            Assert.NotSame(s1, s2);
        }
    }
}
