using System;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Framework-level update option constants.
    /// Each option has a unique string name and a reasonable default value.
    /// Business-specific configuration (URLs, keys, app names, etc.) belongs in
    /// <see cref="Configinfo"/> / <see cref="BaseConfigInfo"/>.
    /// </summary>
    public static class UpdateOptions
    {
        // ═══ Core ═══
        /// <summary>Application role type — Client, Upgrade, or OSS.</summary>
        public static UpdateOption<AppType> AppType { get; } = UpdateOption.ValueOf<AppType>("APPTYPE", Configuration.AppType.Client);

        // ═══ Diff mode ═══
        /// <summary>Diff/patch generation mode — Serial or Parallel.</summary>
        public static UpdateOption<DiffMode> DiffMode { get; } = UpdateOption.ValueOf<DiffMode>("DIFFMODE", Configuration.DiffMode.Serial);

        // ═══ Backward-compatible options ═══
        /// <summary>Compression encoding for update packages.</summary>
        public static UpdateOption<Encoding> Encoding { get; } = UpdateOption.ValueOf<Encoding>("COMPRESSENCODING", System.Text.Encoding.UTF8);

        /// <summary>Compression format (e.g., "ZIP").</summary>
        public static UpdateOption<string> Format { get; } = UpdateOption.ValueOf<string>("COMPRESSFORMAT", "ZIP");

        /// <summary>Download timeout in seconds.</summary>
        public static UpdateOption<int?> DownloadTimeout { get; } = UpdateOption.ValueOf<int?>("DOWNLOADTIMEOUT", 30);

        /// <summary>Whether driver update mode is enabled.</summary>
        public static UpdateOption<bool?> DriveEnabled { get; } = UpdateOption.ValueOf<bool?>("DRIVE", false);

        /// <summary>Whether differential patch update is enabled.</summary>
        public static UpdateOption<bool?> PatchEnabled { get; } = UpdateOption.ValueOf<bool?>("PATCH", true);

        /// <summary>Whether backup before update is enabled.</summary>
        public static UpdateOption<bool?> BackupEnabled { get; } = UpdateOption.ValueOf<bool?>("BACKUP", true);

        /// <summary>Update mode override.</summary>
        public static UpdateOption<UpdateMode?> Mode { get; } = UpdateOption.ValueOf<UpdateMode?>("MODE", null);

        /// <summary>Whether silent background update is enabled.</summary>
        public static UpdateOption<bool> Silent { get; } = UpdateOption.ValueOf<bool>("ENABLESILENTUPDATE", false);

        // ═══ Silent mode ═══
        /// <summary>Whether silent updates auto-install without user intervention.</summary>
        public static UpdateOption<bool> SilentAutoInstall { get; } = UpdateOption.ValueOf<bool>("SILENTAUTOINSTALL", false);

        /// <summary>Polling interval in minutes for silent update checks.</summary>
        public static UpdateOption<int> SilentPollIntervalMinutes { get; } = UpdateOption.ValueOf<int>("SILENTPOLLINTERVALMINUTES", 60);

        // ═══ Concurrency & Resume ═══
        /// <summary>Maximum concurrent download operations.</summary>
        public static UpdateOption<int> MaxConcurrency { get; } = UpdateOption.ValueOf<int>("MAXCONCURRENCY", 3);

        /// <summary>Whether download resume is enabled.</summary>
        public static UpdateOption<bool> EnableResume { get; } = UpdateOption.ValueOf<bool>("ENABLERESUME", true);

        // ═══ Resilience ═══
        /// <summary>Number of retry attempts for failed operations.</summary>
        public static UpdateOption<int> RetryCount { get; } = UpdateOption.ValueOf<int>("RETRYCOUNT", 3);

        /// <summary>Whether checksum verification is performed after download.</summary>
        public static UpdateOption<bool> VerifyChecksum { get; } = UpdateOption.ValueOf<bool>("VERIFYCHECKSUM", true);

        /// <summary>Initial retry interval for exponential back-off. Default 1 second.</summary>
        public static UpdateOption<TimeSpan> RetryInterval { get; } = UpdateOption.ValueOf<TimeSpan>("RETRYINTERVAL", TimeSpan.FromSeconds(1));

        // ═══ OSS ═══
        /// <summary>Object Storage Service provider type.</summary>
        public static UpdateOption<OssProvider?> OSSProvider { get; } = UpdateOption.ValueOf<OssProvider?>("OSSPROVIDER", null);

        /// <summary>OSS bucket region identifier.</summary>
        public static UpdateOption<string?> OSSBucketRegion { get; } = UpdateOption.ValueOf<string?>("OSSBUCKETREGION", null);

        // ═══ Blacklist ═══
        /// <summary>Blacklist configuration for files and directories to exclude from updates.</summary>
        public static UpdateOption<BlackListConfig> BlackList { get; } = UpdateOption.ValueOf<BlackListConfig>("BLACKLIST", BlackListConfig.Empty);

        // ═══ SignalR Hub ═══
        /// <summary>SignalR Hub configuration for push-based updates.</summary>
        public static UpdateOption<HubConfig?> Hub { get; } = UpdateOption.ValueOf<HubConfig?>("HUB", null);
    }
}
