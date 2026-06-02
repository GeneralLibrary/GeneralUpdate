using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Branch coverage points for StrategyFactory.Create():
/// - Windows platform → returns WindowsBowlStrategy
/// - Linux platform → returns LinuxBowlStrategy
/// - Unsupported platform → throws PlatformNotSupportedException
/// </summary>
public class StrategyFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNull_IBowlStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var strategy = StrategyFactory.Create();
            Assert.NotNull(strategy);
            Assert.IsAssignableFrom<IBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_OnWindows_ReturnsWindowsBowlStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var strategy = StrategyFactory.Create();
            Assert.IsType<WindowsBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_OnLinux_ReturnsLinuxBowlStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var strategy = StrategyFactory.Create();
            Assert.IsType<LinuxBowlStrategy>(strategy);
        }
    }

    [Fact]
    public void Create_SupportedPlatform_DoesNotThrow()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var exception = Record.Exception(() => StrategyFactory.Create());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void Create_MultipleCalls_ReturnDifferentInstances()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var s1 = StrategyFactory.Create();
            var s2 = StrategyFactory.Create();
            Assert.NotSame(s1, s2);
        }
    }
}
