using Velaris.Sdk.Platform;

namespace Velaris.Sdk;

/// <summary>
/// Typed configuration for the Velaris SDK.
/// All properties are AOT-safe value types or sealed strings.
/// </summary>
public sealed class VelaConfig
{
    /// <summary>Hub server base URL.</summary>
    public string HubBaseUrl { get; set; } = "https://hub.vela-ota.dev/api/v1";

    /// <summary>Hub poll interval in seconds.</summary>
    public int PollIntervalSeconds { get; set; } = 300;

    /// <summary>Hub auth token.</summary>
    public string? AuthToken { get; set; }

    /// <summary>Download directory for FlashPack artifacts.</summary>
    public string DownloadDir { get; set; } = "/var/cache/vela/downloads";

    /// <summary>Block device path for slot management.</summary>
    public string BlockDevice { get; set; } = "/dev/mmcblk0";

    /// <summary>Device identity key for attestation.</summary>
    public string? IdentityKeyPath { get; set; }

    /// <summary>Enable hardware watchdog.</summary>
    public bool WatchdogEnabled { get; set; } = true;

    /// <summary>Preferred platform (auto-detect if null).</summary>
    public VelaPlatform? PreferredPlatform { get; set; }

    /// <summary>Health pulse interval in seconds.</summary>
    public int PulseIntervalSeconds { get; set; } = 300;

    /// <summary>Enable mock mode for testing (no real HW/FFI).</summary>
    public bool MockMode { get; set; }
}
