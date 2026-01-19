using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Common.Shared.Object;

/// <summary>
/// Internal runtime configuration class that combines user settings with computed state.
/// This class serves as the central configuration holder during the update workflow execution.
/// Inherits common fields from BaseConfigInfo and adds runtime-specific properties.
/// 
/// Responsibilities:
/// - Stores user-provided configuration from Configinfo
/// - Maintains computed runtime values (encoding, format, paths, versions)
/// - Tracks update workflow state (IsMainUpdate, IsUpgradeUpdate)
/// </summary>
public class GlobalConfigInfo : BaseConfigInfo
{
    // User-provided configuration fields (mapped from Configinfo)

    /// <summary>
    /// The API endpoint URL for checking available updates.
    /// Inherited from user configuration, used for version validation requests.
    /// </summary>
    public string UpdateUrl { get; set; }

    /// <summary>
    /// The current version of the upgrade application (the updater itself).
    /// Used separately from ClientVersion to manage updater upgrades.
    /// </summary>
    public string UpgradeClientVersion { get; set; }

    /// <summary>
    /// The unique product identifier for this application.
    /// Used to distinguish between multiple products sharing the same update server.
    /// </summary>
    public string ProductId { get; set; }

    // Runtime computed fields (calculated during workflow execution)


    // Runtime computed fields (calculated during workflow execution)

    /// <summary>
    /// The compression format of update packages (e.g., "ZIP", "7Z").
    /// Computed from UpdateOption.Format or defaults to ZIP.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Indicates whether the upgrade application itself needs to be updated.
    /// Computed by comparing UpgradeClientVersion with server response.
    /// </summary>
    public bool IsUpgradeUpdate { get; set; }

    /// <summary>
    /// Indicates whether the main application needs to be updated.
    /// Computed by comparing ClientVersion with server response.
    /// </summary>
    public bool IsMainUpdate { get; set; }

    /// <summary>
    /// List of version information objects to be updated.
    /// Populated from the update server response based on IsUpgradeUpdate/IsMainUpdate flags.
    /// </summary>
    public List<VersionInfo> UpdateVersions { get; set; }

    /// <summary>
    /// The encoding format used for file operations (e.g., UTF-8, ASCII).
    /// Computed from UpdateOption.Encoding or defaults to system default encoding.
    /// </summary>
    public Encoding Encoding { get; set; }

    /// <summary>
    /// Timeout duration in seconds for download operations.
    /// Computed from UpdateOption.DownloadTimeOut or defaults to 60 seconds.
    /// </summary>
    public int DownloadTimeOut { get; set; }

    /// <summary>
    /// The latest available version from the update server.
    /// Extracted from the server response body after version validation.
    /// </summary>
    public string LastVersion { get; set; }

    /// <summary>
    /// Temporary directory path for downloading and extracting update files.
    /// Computed using StorageManager.GetTempDirectory("main_temp").
    /// </summary>
    public string TempPath { get; set; }

    /// <summary>
    /// Serialized JSON string of ProcessInfo object for inter-process communication.
    /// Contains all parameters needed by the upgrade process.
    /// </summary>
    public string ProcessInfo { get; set; }

    /// <summary>
    /// Indicates whether driver update functionality is enabled.
    /// Computed from UpdateOption.Drive or defaults to false.
    /// </summary>
    public bool? DriveEnabled { get; set; }

    /// <summary>
    /// Indicates whether differential patch update is enabled.
    /// Computed from UpdateOption.Patch or defaults to true.
    /// </summary>
    public bool? PatchEnabled { get; set; }

    /// <summary>
    /// Dictionary for custom field name mappings.
    /// Used for flexible configuration transformations in specific scenarios.
    /// </summary>
    public Dictionary<string, string> FieldMappings { get; set; }

    /// <summary>
    /// Directory path where the current version files are backed up before update.
    /// Computed by combining InstallPath with a versioned directory name.
    /// </summary>
    public string BackupDirectory { get; set; }
}