using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     Internal runtime configuration class that combines user-provided configuration with computed runtime state.
///     Serves as the central configuration holder during the update workflow, inheriting common fields from
///     <see cref="UpdateConfiguration" /> and adding runtime-specific properties.
/// </summary>
/// <remarks>
///     <para>
///         <c>UpdateContext</c> is the internal hub configuration object for the update workflow. It is mapped
///         from <see cref="UpdateRequest" /> via <see cref="ConfigurationMapper.MapToUpdateContext" /> and is
///         progressively populated with runtime-computed values during the various stages of the update pipeline
///         (download, extraction, differential update, etc.).
///     </para>
///     <para>
///         Its primary responsibilities include:
///         <list type="bullet">
///             <item>
///                 <description>Storing user-provided configuration (inherited from <see cref="UpdateRequest" />)</description>
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
///         After the update pipeline completes, <c>UpdateContext</c> is mapped to <see cref="ProcessContract" /> via
///         <see cref="ConfigurationMapper.MapToProcessContract" />, serialized to JSON, and passed to the upgrade process
///         through IPC.
///     </para>
/// </remarks>
/// <seealso cref="UpdateConfiguration" />
/// <seealso cref="UpdateRequest" />
/// <seealso cref="ConfigurationMapper" />
/// <seealso cref="ProcessContract" />
public class UpdateContext : UpdateConfiguration
{
    // ──────────────────────────────
    //  Runtime-computed fields (calculated during pipeline execution)
    // ──────────────────────────────

    /// <summary>
    ///     The compression format used for update packages.
    ///     Computed from <see cref="Option.Format" />, defaults to ZIP.
    /// </summary>
    public Format Format { get; set; }

    /// <summary>
    ///     Indicates whether the updater itself needs to be updated.
    ///     Computed by comparing <see cref="UpgradeClientVersion" /> against the latest version from the server.
    /// </summary>
    public bool IsUpgradeUpdate { get; set; }

    /// <summary>
    ///     Indicates whether the main application needs to be updated.
    ///     Computed by comparing <see cref="UpdateConfiguration.ClientVersion" /> against the latest version from the server.
    /// </summary>
    public bool IsMainUpdate { get; set; }

    /// <summary>
    ///     Whether to launch the client application after the update completes.
    ///     Defaults to <c>true</c>. Set to <c>false</c> in silent mode to let the caller control the restart timing.
    /// </summary>
    public bool LaunchClientAfterUpdate { get; set; } = true;

    /// <summary>
    ///     The report type for status reporting: 1 = Upgrade (active poll), 2 = Push (SignalR push).
    ///     Passed from ClientStrategy through ProcessContract to UpdateStrategy.
    /// </summary>
    public int ReportType { get; set; } = 1;

    /// <summary>
    ///     The list of version information objects to be updated.
    ///     Populated from the update server response based on <see cref="IsUpgradeUpdate" /> and <see cref="IsMainUpdate" />.
    /// </summary>
    public List<VersionEntry> UpdateVersions { get; set; }

    /// <summary>
    ///     The text encoding used for file operations (e.g., UTF-8, ASCII).
    ///     Computed from <see cref="Option.Encoding" />, defaults to the system default encoding.
    /// </summary>
    public Encoding Encoding { get; set; }

    /// <summary>
    ///     The download operation timeout in seconds.
    ///     Computed from <see cref="Option.DownloadTimeout" />, defaults to 60 seconds.
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
    ///     The <see cref="ProcessContract" /> object serialized to a JSON string.
    ///     Contains all parameters required by the upgrade process, used for inter-process communication (IPC).
    /// </summary>
    public string ProcessContract { get; set; }

    /// <summary>
    ///     Whether differential patch updates are enabled.
    ///     Computed from <see cref="Option.PatchEnabled" />, defaults to <c>true</c>.
    /// </summary>
    public bool? PatchEnabled { get; set; }

    /// <summary>
    ///     Whether to back up the current version before applying an update.
    ///     Computed from <see cref="Option.BackupEnabled" />, defaults to <c>true</c>.
    /// </summary>
    public bool? BackupEnabled { get; set; }

    /// <summary>
    ///     The directory path where the current version files are backed up before the update.
    ///     Computed by combining <see cref="UpdateConfiguration.InstallPath" /> with a version-specific directory name.
    /// </summary>
    public string BackupDirectory { get; set; }

    // ──────────────────────────────
    //  Download/Update behavior options (injected from Option)
    // ──────────────────────────────

    /// <summary>
    ///     The maximum number of concurrent download operations.
    ///     Computed from <see cref="Option.MaxConcurrency" />, defaults to 2.
    /// </summary>
    /// <remarks>
    ///     The valid range is 1 to <see cref="Environment.ProcessorCount" /> * 2.
    ///     Values outside this range are automatically clamped to the boundary.
    /// </remarks>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    ///     Whether HTTP Range requests are supported for resumable downloads.
    ///     Computed from <see cref="Option.EnableResume" />, defaults to <c>true</c>.
    /// </summary>
    public bool EnableResume { get; set; } = true;

    /// <summary>
    ///     The maximum number of retry attempts on download failure.
    ///     Computed from <see cref="Option.RetryCount" />, defaults to 3.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    ///     The initial retry interval for the exponential backoff strategy.
    ///     Computed from <see cref="Option.RetryInterval" />, defaults to 1 second.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Whether to perform SHA256 checksum verification after download.
    ///     Computed from <see cref="Option.VerifyChecksum" />, defaults to <c>true</c>.
    /// </summary>
    public bool VerifyChecksum { get; set; } = true;

    /// <summary>
    ///     The diff/patch generation mode — serial (<see cref="Configuration.DiffMode.Serial" />) or parallel
    ///     (<see cref="Configuration.DiffMode.Parallel" />).
    ///     Computed from <see cref="Option.DiffMode" />, defaults to <see cref="Configuration.DiffMode.Serial" />.
    /// </summary>
    public DiffMode DiffMode { get; set; } = DiffMode.Serial;
}
