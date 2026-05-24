namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Application role type — determines the update workflow.
/// </summary>
public enum AppType
{
    /// <summary>Main application — validates versions, downloads packages, starts upgrade process.</summary>
    Client = 1,

    /// <summary>Upgrade application — applies downloaded update packages, starts main app.</summary>
    Upgrade = 2,

    /// <summary>OSS (Object Storage Service) update mode — downloads from cloud storage.</summary>
    OSS = 3
}
