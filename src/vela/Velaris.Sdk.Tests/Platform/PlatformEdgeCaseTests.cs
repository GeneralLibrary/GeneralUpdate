using Microsoft.Extensions.Logging;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests.Platform;

public class PlatformEdgeCaseTests
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(b => b.AddConsole());

    [Fact]
    public async Task LinuxStrategy_GetSlots_ReturnsExactlyTwo()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        var slots = await s.GetSlotsAsync();
        Assert.Equal(2, slots.Length);
        Assert.Equal("A", slots[0].Id);
        Assert.Equal("B", slots[1].Id);
    }

    [Fact]
    public async Task LinuxStrategy_GetSlots_PathsStartWithDev()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        var slots = await s.GetSlotsAsync();
        Assert.All(slots, slot => Assert.StartsWith("/dev/", slot.DevicePath));
    }

    [Fact]
    public async Task LinuxStrategy_GetSlots_AllBootable()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        var slots = await s.GetSlotsAsync();
        Assert.All(slots, slot => Assert.True(slot.IsBootable));
    }

    [Fact]
    public async Task LinuxStrategy_PrepareUpdate_DoesNotThrow()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        await s.PrepareUpdateAsync(new FlashPackMetadata
        {
            BundleName = "test", BundleVersion = "1.0", PayloadSize = 100,
        });
    }

    [Fact]
    public async Task LinuxStrategy_Cleanup_BothPathsDontThrow()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        await s.CleanupAfterUpdateAsync(true);
        await s.CleanupAfterUpdateAsync(false);
    }

    [Fact]
    public async Task WindowsStrategy_GetSlots_ReturnsSingleSlot()
    {
        var s = new WindowsStrategy(_loggerFactory.CreateLogger<WindowsStrategy>());
        var slots = await s.GetSlotsAsync();
        Assert.Single(slots);
        Assert.Equal("windows-current", slots[0].Id);
    }

    [Fact]
    public async Task WindowsStrategy_PrepareUpdate_Throws()
    {
        var s = new WindowsStrategy(_loggerFactory.CreateLogger<WindowsStrategy>());
        await Assert.ThrowsAsync<NotImplementedException>(() => s.PrepareUpdateAsync(new FlashPackMetadata()));
    }

    [Fact]
    public async Task WindowsStrategy_Cleanup_Throws()
    {
        var s = new WindowsStrategy(_loggerFactory.CreateLogger<WindowsStrategy>());
        await Assert.ThrowsAsync<NotImplementedException>(() => s.CleanupAfterUpdateAsync(true));
    }

    [Fact]
    public void LinuxStrategy_SupportsDualSlotRollback()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        Assert.True(s.SupportsDualSlotRollback);
    }

    [Fact]
    public void WindowsStrategy_NoDualSlotRollback()
    {
        var s = new WindowsStrategy(_loggerFactory.CreateLogger<WindowsStrategy>());
        Assert.False(s.SupportsDualSlotRollback);
    }

    [Fact]
    public void LinuxStrategy_PreferredMethod_IsFullImageSwap()
    {
        var s = new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>());
        Assert.Equal(UpdateMethod.FullImageSwap, s.PreferredUpdateMethod);
    }

    [Fact]
    public void WindowsStrategy_PreferredMethod_IsFileOverlay()
    {
        var s = new WindowsStrategy(_loggerFactory.CreateLogger<WindowsStrategy>());
        Assert.Equal(UpdateMethod.FileOverlay, s.PreferredUpdateMethod);
    }
}
