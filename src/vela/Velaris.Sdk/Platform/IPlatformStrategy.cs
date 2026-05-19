using System.Text.Json.Serialization;
using Velaris.Sdk.SafeHandles;

namespace Velaris.Sdk.Platform;

/// <summary>
/// Platform abstraction for OS-specific update behavior.
/// Vela supports Linux (A/B slots), Windows IoT, and future targets.
/// </summary>
public interface IPlatformStrategy
{
    /// <summary>Platform identifier.</summary>
    VelaPlatform TargetPlatform { get; }

    /// <summary>Validate the target environment before update.</summary>
    Task<bool> ValidateEnvironmentAsync();

    /// <summary>Get slot layout for this platform.</summary>
    Task<SlotInfo[]> GetSlotsAsync();

    /// <summary>Prepare the platform for an update.</summary>
    Task PrepareUpdateAsync(FlashPackMetadata metadata);

    /// <summary>Clean up after update (success or failure).</summary>
    Task CleanupAfterUpdateAsync(bool success);

    /// <summary>Whether dual-slot rollback is supported.</summary>
    bool SupportsDualSlotRollback { get; }

    /// <summary>Recommended update method for this platform.</summary>
    UpdateMethod PreferredUpdateMethod { get; }
}

/// <summary>Target platform enumeration.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VelaPlatform
{
    Linux,
    WindowsIoT,
    Android,
    FreeRTOS,
}

/// <summary>Update method for different platforms.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateMethod
{
    /// <summary>Full image swap (A/B slots).</summary>
    FullImageSwap,

    /// <summary>File-level overlay replacement.</summary>
    FileOverlay,

    /// <summary>Platform package manager.</summary>
    PackageManager,

    /// <summary>Application-level only.</summary>
    ApplicationOnly,
}

/// <summary>Slot information for a platform partition.</summary>
public sealed class SlotInfo
{
    public string Id { get; init; } = "";
    public string DevicePath { get; init; } = "";
    public string CurrentVersion { get; init; } = "";
    public bool IsBootable { get; init; }
}

/// <summary>Metadata about an update bundle.</summary>
public sealed class FlashPackMetadata
{
    public string BundleName { get; init; } = "";
    public string BundleVersion { get; init; } = "";
    public string FormatVersion { get; init; } = "";
    public string PayloadType { get; init; } = "";
    public long PayloadSize { get; init; }
    public string RequiresVersion { get; init; } = "";
}
