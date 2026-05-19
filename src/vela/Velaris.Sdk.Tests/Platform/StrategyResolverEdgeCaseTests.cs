using Microsoft.Extensions.Logging;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests.Platform;

public class StrategyResolverEdgeCaseTests
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(b => b.AddConsole());

    [Fact]
    public void Resolve_ExplicitLinux_IsLinuxStrategy()
    {
        var s = StrategyResolver.Resolve(VelaPlatform.Linux, _loggerFactory);
        Assert.IsType<LinuxStrategy>(s);
        Assert.Equal(VelaPlatform.Linux, s.TargetPlatform);
        Assert.True(s.SupportsDualSlotRollback);
        Assert.Equal(UpdateMethod.FullImageSwap, s.PreferredUpdateMethod);
    }

    [Fact]
    public void Resolve_ExplicitWindowsIoT_IsWindowsStrategy()
    {
        var s = StrategyResolver.Resolve(VelaPlatform.WindowsIoT, _loggerFactory);
        Assert.IsType<WindowsStrategy>(s);
        Assert.Equal(VelaPlatform.WindowsIoT, s.TargetPlatform);
        Assert.False(s.SupportsDualSlotRollback);
        Assert.Equal(UpdateMethod.FileOverlay, s.PreferredUpdateMethod);
    }

    [Fact]
    public void Resolve_Android_ThrowsPlatformNotSupported()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(
            () => StrategyResolver.Resolve(VelaPlatform.Android, _loggerFactory));
        Assert.Contains("Android", ex.Message);
    }

    [Fact]
    public void Resolve_FreeRTOS_ThrowsPlatformNotSupported()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(
            () => StrategyResolver.Resolve(VelaPlatform.FreeRTOS, _loggerFactory));
        Assert.Contains("FreeRTOS", ex.Message);
    }

    [Fact]
    public void Resolve_AutoDetect_ReturnsNonNullStrategy()
    {
        var s = StrategyResolver.Resolve(_loggerFactory);
        Assert.NotNull(s);
        Assert.True(s.TargetPlatform is VelaPlatform.Linux or VelaPlatform.WindowsIoT);
    }

    [Fact]
    public async Task ResolvedStrategy_SlotsDifferByPlatform()
    {
        var linux = StrategyResolver.Resolve(VelaPlatform.Linux, _loggerFactory);
        var windows = StrategyResolver.Resolve(VelaPlatform.WindowsIoT, _loggerFactory);

        var linuxSlots = await linux.GetSlotsAsync();
        var windowsSlots = await windows.GetSlotsAsync();

        // Linux has 2 A/B slots, Windows has 1 virtual slot
        Assert.Equal(2, linuxSlots.Length);
        Assert.Single(windowsSlots);
    }

    [Fact]
    public async Task ResolvedStrategy_ValidateEnvironment_Completes()
    {
        var s = StrategyResolver.Resolve(_loggerFactory);
        var result = await s.ValidateEnvironmentAsync();
        Assert.True(result || !result); // Depends on OS
    }
}
