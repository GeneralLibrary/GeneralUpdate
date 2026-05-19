using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Velaris.Sdk.Platform;

public sealed class LinuxStrategy : IPlatformStrategy
{
    private readonly ILogger<LinuxStrategy> _logger;
    public VelaPlatform TargetPlatform => VelaPlatform.Linux;
    public bool SupportsDualSlotRollback => true;
    public UpdateMethod PreferredUpdateMethod => UpdateMethod.FullImageSwap;

    public LinuxStrategy(ILogger<LinuxStrategy> logger) { _logger = logger; }

    public Task<bool> ValidateEnvironmentAsync()
    {
        _logger.LogDebug("Linux strategy: validating environment");
        return Task.FromResult(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
    }

    public Task<SlotInfo[]> GetSlotsAsync() => Task.FromResult(new[]
    {
        new SlotInfo { Id = "A", DevicePath = "/dev/mmcblk0p2", IsBootable = true },
        new SlotInfo { Id = "B", DevicePath = "/dev/mmcblk0p3", IsBootable = true },
    });

    public Task PrepareUpdateAsync(FlashPackMetadata metadata)
    {
        _logger.LogInformation("Linux prepare: {Name} v{Version}", metadata.BundleName, metadata.BundleVersion);
        return Task.CompletedTask;
    }

    public Task CleanupAfterUpdateAsync(bool success)
    {
        _logger.LogInformation("Linux cleanup: success={Success}", success);
        return Task.CompletedTask;
    }
}

public sealed class WindowsStrategy : IPlatformStrategy
{
    private readonly ILogger<WindowsStrategy> _logger;
    public VelaPlatform TargetPlatform => VelaPlatform.WindowsIoT;
    public bool SupportsDualSlotRollback => false;
    public UpdateMethod PreferredUpdateMethod => UpdateMethod.FileOverlay;

    public WindowsStrategy(ILogger<WindowsStrategy> logger) { _logger = logger; }

    public Task<bool> ValidateEnvironmentAsync()
    {
        _logger.LogDebug("Windows strategy: validating");
        return Task.FromResult(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    public Task<SlotInfo[]> GetSlotsAsync() => Task.FromResult(new[]
    {
        new SlotInfo { Id = "windows-current", DevicePath = Environment.GetFolderPath(Environment.SpecialFolder.System), IsBootable = true },
    });

    public Task PrepareUpdateAsync(FlashPackMetadata metadata) =>
        throw new NotImplementedException("Windows IoT update not yet implemented (P2 roadmap).");

    public Task CleanupAfterUpdateAsync(bool success) =>
        throw new NotImplementedException("Windows IoT cleanup not yet implemented (P2 roadmap).");
}
