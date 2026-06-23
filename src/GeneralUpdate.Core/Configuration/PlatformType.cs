namespace GeneralUpdate.Core.Configuration;

/// <summary>Platform type enumeration.</summary>
public enum PlatformType
{
    /// <summary>Unknown / not detected.</summary>
    Unknown = 0,

    /// <summary>Microsoft Windows.</summary>
    Windows = 1,

    /// <summary>Linux distributions (Ubuntu, Debian, UOS, Kylin, etc.).</summary>
    Linux = 2,

    /// <summary>Apple macOS.</summary>
    MacOS = 3
}
