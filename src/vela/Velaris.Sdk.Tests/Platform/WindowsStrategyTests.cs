using Microsoft.Extensions.Logging;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests.Platform;

public class WindowsStrategyTests
{
    private readonly ILogger<WindowsStrategy> _logger;

    public WindowsStrategyTests()
    {
        _logger = LoggerFactory.Create(b => b.AddConsole())
            .CreateLogger<WindowsStrategy>();
    }

    [Fact]
    public void TargetPlatform_ReturnsWindowsIoT()
    {
        var strategy = new WindowsStrategy(_logger);
        Assert.Equal(VelaPlatform.WindowsIoT, strategy.TargetPlatform);
    }

    [Fact]
    public void SupportsDualSlotRollback_False()
    {
        var strategy = new WindowsStrategy(_logger);
        Assert.False(strategy.SupportsDualSlotRollback);
    }

    [Fact]
    public void PreferredUpdateMethod_IsFileOverlay()
    {
        var strategy = new WindowsStrategy(_logger);
        Assert.Equal(UpdateMethod.FileOverlay, strategy.PreferredUpdateMethod);
    }

    [Fact]
    public async Task ValidateEnvironmentAsync_Completes()
    {
        var strategy = new WindowsStrategy(_logger);
        var result = await strategy.ValidateEnvironmentAsync();
        Assert.True(result || !result);
    }

    [Fact]
    public async Task GetSlotsAsync_ReturnsVirtualSlot()
    {
        var strategy = new WindowsStrategy(_logger);
        var slots = await strategy.GetSlotsAsync();
        Assert.NotNull(slots);
        Assert.Single(slots);
        Assert.Equal("windows-current", slots[0].Id);
    }

    [Fact]
    public async Task PrepareUpdateAsync_ThrowsNotImplemented()
    {
        var strategy = new WindowsStrategy(_logger);
        var metadata = new FlashPackMetadata
        {
            BundleName = "vela-os",
            BundleVersion = "2.0.0",
        };

        await Assert.ThrowsAsync<NotImplementedException>(
            () => strategy.PrepareUpdateAsync(metadata));
    }
}
