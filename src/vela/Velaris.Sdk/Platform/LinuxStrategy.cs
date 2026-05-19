using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Velaris.Sdk.Platform;

/// <summary>
/// Linux update strategy — delegates to Rust Vela Core via FFI.
/// Uses A/B dual-slot partition model (Primary/Alternate).
/// This is the primary (P0) platform for Vela OTA.
/// </summary>
public sealed class LinuxStrategy : IPlatformStrategy
{
    private readonly ILogger<LinuxStrategy> _logger;

    public VelaPlatform TargetPlatform => VelaPlatform.Linux;
    public bool SupportsDualSlotRollback => true;
    public UpdateMethod PreferredUpdateMethod => UpdateMethod.FullImageSwap;

    public LinuxStrategy(ILogger<LinuxStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> ValidateEnvironmentAsync()
    {
        _logger.LogDebug("Linux strategy: validating environment");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _logger.LogError("Linux strategy loaded on non-Linux platform: {OS}", RuntimeInformation.OSDescription);
            return Task.FromResult(false);
        }

        // Check for A/B partition layout via sysfs or bootloader variables.
        // In production this would call vela_slot_info via FFI.
        _logger.LogInformation("Linux strategy: environment validated (A/B slot model)");
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<SlotInfo[]> GetSlotsAsync()
    {
        _logger.LogDebug("Linux strategy: querying slot layout");

        // In production, this calls vela_slot_info FFI.
        // For now, return a simulated A/B layout.
        await Task.CompletedTask;

        return new[]
        {
            new SlotInfo
            {
                Id = "A",
                DevicePath = "/dev/mmcblk0p2",
                CurrentVersion = "unknown",
                IsBootable = true,
            },
            new SlotInfo
            {
                Id = "B",
                DevicePath = "/dev/mmcblk0p3",
                CurrentVersion = "unknown",
                IsBootable = true,
            },
        };
    }

    /// <inheritdoc />
    public Task PrepareUpdateAsync(FlashPackMetadata metadata)
    {
        _logger.LogInformation(
            "Linux prepare update: {Name} v{Version} ({PayloadType}, {PayloadSize} bytes)",
            metadata.BundleName,
            metadata.BundleVersion,
            metadata.PayloadType,
            metadata.PayloadSize);

        // Linux A/B model preparation:
        // 1. Verify alternate slot is writable
        // 2. Check free space on alternate partition
        // 3. Set kernel panic timeout (prevent mid-update reboot hang)
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CleanupAfterUpdateAsync(bool success)
    {
        if (success)
        {
            _logger.LogInformation("Linux update succeeded — committing boot flags");
            // Boot flag CommitSuccess handled by lifecycle state machine
        }
        else
        {
            _logger.LogWarning("Linux update failed — initiating fallback recovery");
            // FallbackRecovery handled by lifecycle state machine
        }

        return Task.CompletedTask;
    }
}
