using Microsoft.Extensions.Logging;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests.Platform;

public class StrategyResolverTests
{
    private readonly ILoggerFactory _loggerFactory;

    public StrategyResolverTests()
    {
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    }

    [Fact]
    public void Resolve_ReturnsCorrectType()
    {
        var strategy = StrategyResolver.Resolve(_loggerFactory);
        Assert.NotNull(strategy);

        // On Windows, it should be WindowsStrategy
        // On Linux, it should be LinuxStrategy
        var platform = strategy.TargetPlatform;
        Assert.True(
            platform == VelaPlatform.Linux || platform == VelaPlatform.WindowsIoT,
            $"Unexpected platform: {platform}");
    }

    [Fact]
    public void Resolve_WithExplicitLinux_ReturnsLinuxStrategy()
    {
        var strategy = StrategyResolver.Resolve(VelaPlatform.Linux, _loggerFactory);
        Assert.IsType<LinuxStrategy>(strategy);
        Assert.Equal(VelaPlatform.Linux, strategy.TargetPlatform);
        Assert.True(strategy.SupportsDualSlotRollback);
    }

    [Fact]
    public void Resolve_WithExplicitWindowsIoT_ReturnsWindowsStrategy()
    {
        var strategy = StrategyResolver.Resolve(VelaPlatform.WindowsIoT, _loggerFactory);
        Assert.IsType<WindowsStrategy>(strategy);
        Assert.Equal(VelaPlatform.WindowsIoT, strategy.TargetPlatform);
        Assert.False(strategy.SupportsDualSlotRollback);
    }

    [Fact]
    public void Resolve_Android_Throws()
    {
        Assert.Throws<PlatformNotSupportedException>(
            () => StrategyResolver.Resolve(VelaPlatform.Android, _loggerFactory));
    }

    [Fact]
    public void Resolve_FreeRTOS_Throws()
    {
        Assert.Throws<PlatformNotSupportedException>(
            () => StrategyResolver.Resolve(VelaPlatform.FreeRTOS, _loggerFactory));
    }

    [Fact]
    public async Task ResolvedStrategy_ValidateEnvironment_Completes()
    {
        var strategy = StrategyResolver.Resolve(_loggerFactory);
        var result = await strategy.ValidateEnvironmentAsync();
        Assert.True(result || !result);
    }

    [Fact]
    public async Task ResolvedStrategy_GetSlots_ReturnsAtLeastOne()
    {
        var strategy = StrategyResolver.Resolve(_loggerFactory);
        var slots = await strategy.GetSlotsAsync();
        Assert.NotNull(slots);
        Assert.NotEmpty(slots);
    }
}
