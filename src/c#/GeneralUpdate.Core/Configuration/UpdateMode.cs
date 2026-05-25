namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Specifies the deployment mode for updates.
/// </summary>
public enum UpdateMode
{
    /// <summary>Standard file-based update.</summary>
    Default = 0,
    /// <summary>Script-based custom update logic.</summary>
    Scripts = 1
}