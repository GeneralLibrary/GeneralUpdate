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

    /// <summary>Oss client mode — checks version config, starts upgrade process.</summary>
    OssClient = 3,

    /// <summary>Oss upgrade mode — downloads packages from Oss, deploys to client.</summary>
    OssUpgrade = 4
}
