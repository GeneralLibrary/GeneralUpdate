using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration;

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
    /// The compression format of update packages.
    /// Computed from UpdateOption.Format or defaults to ZIP.
    /// </summary>
    public Format Format { get; set; }

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
    /// Whether to launch the client app after the upgrade process completes.
    /// Default true. Set to false in silent mode when the caller wants to control restart timing.
    /// </summary>
    public bool LaunchClientAfterUpdate { get; set; } = true;

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
    /// Indicates whether differential patch update is enabled.
    /// Computed from UpdateOption.Patch or defaults to true.
    /// </summary>
    public bool? PatchEnabled { get; set; }

    /// <summary>
    /// Whether to back up the current version before applying an update.
    /// Computed from UpdateOption.BackupEnabled, defaults to true.
    /// </summary>
    public bool? BackupEnabled { get; set; }

    /// <summary>
    /// Directory path where the current version files are backed up before update.
    /// Computed by combining InstallPath with a versioned directory name.
    /// </summary>
    public string BackupDirectory { get; set; }

    // ═══ Download/update behaviour options (wired from UpdateOptions) ═══

    /// <summary>
    /// Maximum number of concurrent download operations.
    /// Computed from UpdateOption.MaxConcurrency, defaults to 2.
    /// Valid range: 1 to <see cref="Environment.ProcessorCount"/> * 2.
    /// </summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    /// Whether to resume interrupted downloads via HTTP Range requests.
    /// Computed from UpdateOption.EnableResume, defaults to true.
    /// </summary>
    public bool EnableResume { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed download operations.
    /// Computed from UpdateOption.RetryCount, defaults to 3.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Initial retry interval for exponential back-off.
    /// Computed from UpdateOption.RetryInterval, defaults to 1 second.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to perform SHA256 checksum verification after download.
    /// Computed from UpdateOption.VerifyChecksum, defaults to true.
    /// </summary>
    public bool VerifyChecksum { get; set; } = true;

    /// <summary>
    /// Diff/patch generation mode — Serial or Parallel.
    /// Computed from UpdateOption.DiffMode, defaults to <see cref="Configuration.DiffMode.Serial"/>.
    /// </summary>
    public DiffMode DiffMode { get; set; } = DiffMode.Serial;
}
