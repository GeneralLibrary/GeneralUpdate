using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     Internal runtime configuration class that combines user-provided configuration with computed runtime state.
///     Serves as the central configuration holder during the update workflow, inheriting common fields from
///     <see cref="BaseConfigInfo" /> and adding runtime-specific properties.
/// </summary>
/// <remarks>
///     <para>
///         <c>GlobalConfigInfo</c> is the internal hub configuration object for the update workflow. It is mapped
///         from <see cref="Configinfo" /> via <see cref="ConfigurationMapper.MapToGlobalConfigInfo" /> and is
///         progressively populated with runtime-computed values during the various stages of the update pipeline
///         (download, extraction, differential update, etc.).
///     </para>
///     <para>
///         Its primary responsibilities include:
///         <list type="bullet">
///             <item>
///                 <description>Storing user-provided configuration (inherited from <see cref="Configinfo" />)</description>
///             </item>
///             <item>
///                 <description>Maintaining computed runtime values (encoding, format, paths, version numbers, etc.)</description>
///             </item>
///             <item>
///                 <description>Tracking the update workflow state (<see cref="IsMainUpdate" />, <see cref="IsUpgradeUpdate" />)</description>
///             </item>
///         </list>
///     </para>
///     <para>
///         After the update pipeline completes, <c>GlobalConfigInfo</c> is mapped to <see cref="ProcessInfo" /> via
///         <see cref="ConfigurationMapper.MapToProcessInfo" />, serialized to JSON, and passed to the upgrade process
///         through IPC.
///     </para>
/// </remarks>
/// <seealso cref="BaseConfigInfo" />
/// <seealso cref="Configinfo" />
/// <seealso cref="ConfigurationMapper" />
/// <seealso cref="ProcessInfo" />
public class GlobalConfigInfo : BaseConfigInfo
{
    // ──────────────────────────────
    //  用户配置字段（从 Configinfo 映射而来）
    // ──────────────────────────────

    /// <summary>
    ///     The API endpoint URL used to check for available updates.
    ///     Inherited from user configuration, used to send version check requests to the server.
    /// </summary>
    /// <remarks>
    ///     This value is mapped from <see cref="Configinfo.UpdateUrl" /> by
    ///     <see cref="ConfigurationMapper.MapToGlobalConfigInfo" />.
    /// </remarks>
    public string UpdateUrl { get; set; }

    /// <summary>
    ///     The current version number of the updater (the update client itself).
    ///     Managed separately from <see cref="BaseConfigInfo.ClientVersion" /> to enable independent updater upgrades.
    /// </summary>
    /// <remarks>
    ///     Comparing this version number against the server response determines the value of <see cref="IsUpgradeUpdate" />.
    /// </remarks>
    public string UpgradeClientVersion { get; set; }

    /// <summary>
    ///     The unique product identifier for the current application.
    ///     Used to distinguish between multiple products sharing the same update server.
    /// </summary>
    public string ProductId { get; set; }

    // ──────────────────────────────
    //  Runtime-computed fields (calculated during pipeline execution)
    // ──────────────────────────────

    /// <summary>
    ///     The compression format used for update packages.
    ///     Computed from <see cref="UpdateOptions.Format" />, defaults to ZIP.
    /// </summary>
    public Format Format { get; set; }

    /// <summary>
    ///     Indicates whether the updater itself needs to be updated.
    ///     Computed by comparing <see cref="UpgradeClientVersion" /> against the latest version from the server.
    /// </summary>
    public bool IsUpgradeUpdate { get; set; }

    /// <summary>
    ///     Indicates whether the main application needs to be updated.
    ///     Computed by comparing <see cref="BaseConfigInfo.ClientVersion" /> against the latest version from the server.
    /// </summary>
    public bool IsMainUpdate { get; set; }

    /// <summary>
    ///     Whether to launch the client application after the update completes.
    ///     Defaults to <c>true</c>. Set to <c>false</c> in silent mode to let the caller control the restart timing.
    /// </summary>
    public bool LaunchClientAfterUpdate { get; set; } = true;

    /// <summary>
    ///     The report type for status reporting: 1 = Upgrade (active poll), 2 = Push (SignalR push).
    ///     Passed from ClientStrategy through ProcessInfo to UpdateStrategy.
    /// </summary>
    public int ReportType { get; set; } = 1;

    /// <summary>
    ///     The list of version information objects to be updated.
    ///     Populated from the update server response based on <see cref="IsUpgradeUpdate" /> and <see cref="IsMainUpdate" />.
    /// </summary>
    public List<VersionInfo> UpdateVersions { get; set; }

    /// <summary>
    ///     The text encoding used for file operations (e.g., UTF-8, ASCII).
    ///     Computed from <see cref="UpdateOptions.Encoding" />, defaults to the system default encoding.
    /// </summary>
    public Encoding Encoding { get; set; }

    /// <summary>
    ///     The download operation timeout in seconds.
    ///     Computed from <see cref="UpdateOptions.DownloadTimeout" />, defaults to 60 seconds.
    /// </summary>
    public int DownloadTimeOut { get; set; }

    /// <summary>
    ///     The latest available version number on the update server.
    ///     Extracted from the server response body after the version check completes.
    /// </summary>
    public string LastVersion { get; set; }

    /// <summary>
    ///     The temporary directory path used for downloading and extracting update files.
    ///     Computed via <c>StorageManager.GetTempDirectory("main_temp")</c>.
    /// </summary>
    public string TempPath { get; set; }

    /// <summary>
    ///     The <see cref="ProcessInfo" /> object serialized to a JSON string.
    ///     Contains all parameters required by the upgrade process, used for inter-process communication (IPC).
    /// </summary>
    public string ProcessInfo { get; set; }

    /// <summary>
    ///     Whether differential patch updates are enabled.
    ///     Computed from <see cref="UpdateOptions.PatchEnabled" />, defaults to <c>true</c>.
    /// </summary>
    public bool? PatchEnabled { get; set; }

    /// <summary>
    ///     Whether to back up the current version before applying an update.
    ///     Computed from <see cref="UpdateOptions.BackupEnabled" />, defaults to <c>true</c>.
    /// </summary>
    public bool? BackupEnabled { get; set; }

    /// <summary>
    ///     The directory path where the current version files are backed up before the update.
    ///     Computed by combining <see cref="BaseConfigInfo.InstallPath" /> with a version-specific directory name.
    /// </summary>
    public string BackupDirectory { get; set; }

    // ──────────────────────────────
    //  Download/Update behavior options (injected from UpdateOptions)
    // ──────────────────────────────

    /// <summary>
    ///     The maximum number of concurrent download operations.
    ///     Computed from <see cref="UpdateOptions.MaxConcurrency" />, defaults to 2.
    /// </summary>
    /// <remarks>
    ///     The valid range is 1 to <see cref="Environment.ProcessorCount" /> * 2.
    ///     Values outside this range are automatically clamped to the boundary.
    /// </remarks>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    ///     Whether HTTP Range requests are supported for resumable downloads.
    ///     Computed from <see cref="UpdateOptions.EnableResume" />, defaults to <c>true</c>.
    /// </summary>
    public bool EnableResume { get; set; } = true;

    /// <summary>
    ///     The maximum number of retry attempts on download failure.
    ///     Computed from <see cref="UpdateOptions.RetryCount" />, defaults to 3.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    ///     The initial retry interval for the exponential backoff strategy.
    ///     Computed from <see cref="UpdateOptions.RetryInterval" />, defaults to 1 second.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Whether to perform SHA256 checksum verification after download.
    ///     Computed from <see cref="UpdateOptions.VerifyChecksum" />, defaults to <c>true</c>.
    /// </summary>
    public bool VerifyChecksum { get; set; } = true;

    /// <summary>
    ///     The diff/patch generation mode — serial (<see cref="Configuration.DiffMode.Serial" />) or parallel
    ///     (<see cref="Configuration.DiffMode.Parallel" />).
    ///     Computed from <see cref="UpdateOptions.DiffMode" />, defaults to <see cref="Configuration.DiffMode.Serial" />.
    /// </summary>
    public DiffMode DiffMode { get; set; } = DiffMode.Serial;
}
