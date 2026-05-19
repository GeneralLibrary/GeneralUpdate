namespace Velaris.Sdk.Platform;

public interface IPlatformStrategy
{
    VelaPlatform TargetPlatform { get; }
    Task<bool> ValidateEnvironmentAsync();
    Task<SlotInfo[]> GetSlotsAsync();
    Task PrepareUpdateAsync(FlashPackMetadata metadata);
    Task CleanupAfterUpdateAsync(bool success);
    bool SupportsDualSlotRollback { get; }
    UpdateMethod PreferredUpdateMethod { get; }
}

public enum VelaPlatform { Linux, WindowsIoT, Android, FreeRTOS }
public enum UpdateMethod { FullImageSwap, FileOverlay, PackageManager, ApplicationOnly }

public sealed class SlotInfo
{
    public string Id { get; init; } = "";
    public string DevicePath { get; init; } = "";
    public string CurrentVersion { get; init; } = "";
    public bool IsBootable { get; init; }
}

public sealed class FlashPackMetadata
{
    public string BundleName { get; init; } = "";
    public string BundleVersion { get; init; } = "";
    public string FormatVersion { get; init; } = "";
    public string PayloadType { get; init; } = "";
    public long PayloadSize { get; init; }
    public string RequiresVersion { get; init; } = "";
}
