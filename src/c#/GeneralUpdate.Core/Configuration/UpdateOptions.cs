using System;
using System.Text;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Convenience accessor for UpdateOption constants.
    /// Each option has a unique string name and a reasonable default value.
    /// Use via <c>.Option(UpdateOptions.UpdateUrl, "https://...")</c>.
    /// </summary>
    public static class UpdateOptions
    {
        // ═══ Core ═══
        public static UpdateOption<AppType> AppType { get; } = UpdateOption.ValueOf<AppType>("APPTYPE", Configuration.AppType.Client);

        // ═══ Diff mode ═══
        public static UpdateOption<DiffMode> DiffMode { get; } = UpdateOption.ValueOf<DiffMode>("DIFFMODE", Configuration.DiffMode.Serial);

        // ═══ Backward-compatible options ═══
        public static UpdateOption<Encoding> Encoding { get; } = UpdateOption.ValueOf<Encoding>("COMPRESSENCODING", System.Text.Encoding.UTF8);
        public static UpdateOption<string> Format { get; } = UpdateOption.ValueOf<string>("COMPRESSFORMAT", "ZIP");
        public static UpdateOption<int?> DownloadTimeout { get; } = UpdateOption.ValueOf<int?>("DOWNLOADTIMEOUT", 30);
        public static UpdateOption<bool?> DriveEnabled { get; } = UpdateOption.ValueOf<bool?>("DRIVE", false);
        public static UpdateOption<bool?> PatchEnabled { get; } = UpdateOption.ValueOf<bool?>("PATCH", true);
        public static UpdateOption<bool?> BackupEnabled { get; } = UpdateOption.ValueOf<bool?>("BACKUP", true);
        public static UpdateOption<UpdateMode?> Mode { get; } = UpdateOption.ValueOf<UpdateMode?>("MODE", null);
        public static UpdateOption<bool> Silent { get; } = UpdateOption.ValueOf<bool>("ENABLESILENTUPDATE", false);

        // ═══ New options ═══
        public static UpdateOption<string?> UpdateUrl { get; } = UpdateOption.ValueOf<string?>("UPDATEURL", null);
        public static UpdateOption<string> AppSecretKey { get; } = UpdateOption.ValueOf<string>("APPSECRETKEY", string.Empty);
        public static UpdateOption<string> AppName { get; } = UpdateOption.ValueOf<string>("APPNAME", string.Empty);
        public static UpdateOption<string> MainAppName { get; } = UpdateOption.ValueOf<string>("MAINAPPNAME", string.Empty);
        public static UpdateOption<string> InstallPath { get; } = UpdateOption.ValueOf<string>("INSTALLPATH", AppContext.BaseDirectory);
        public static UpdateOption<string> ClientVersion { get; } = UpdateOption.ValueOf<string>("CLIENTVERSION", string.Empty);
        public static UpdateOption<string?> UpgradeClientVersion { get; } = UpdateOption.ValueOf<string?>("UPGRADECLIENTVERSION", null);
        public static UpdateOption<PlatformType?> Platform { get; } = UpdateOption.ValueOf<PlatformType?>("PLATFORM", null);
        public static UpdateOption<bool> SilentAutoInstall { get; } = UpdateOption.ValueOf<bool>("SILENTAUTOINSTALL", false);
        public static UpdateOption<int> SilentPollIntervalMinutes { get; } = UpdateOption.ValueOf<int>("SILENTPOLLINTERVALMINUTES", 60);
        public static UpdateOption<int> MaxConcurrency { get; } = UpdateOption.ValueOf<int>("MAXCONCURRENCY", 3);
        public static UpdateOption<bool> EnableResume { get; } = UpdateOption.ValueOf<bool>("ENABLERESUME", true);
        public static UpdateOption<int> RetryCount { get; } = UpdateOption.ValueOf<int>("RETRYCOUNT", 3);
        public static UpdateOption<bool> VerifyChecksum { get; } = UpdateOption.ValueOf<bool>("VERIFYCHECKSUM", true);
        public static UpdateOption<string?> ReportUrl { get; } = UpdateOption.ValueOf<string?>("REPORTURL", null);
        public static UpdateOption<string?> ProductId { get; } = UpdateOption.ValueOf<string?>("PRODUCTID", null);
        public static UpdateOption<string?> PermissionScript { get; } = UpdateOption.ValueOf<string?>("PERMISSIONSCRIPT", null);
        public static UpdateOption<string?> Scheme { get; } = UpdateOption.ValueOf<string?>("SCHEME", null);
        public static UpdateOption<string?> Token { get; } = UpdateOption.ValueOf<string?>("TOKEN", null);

        // ═══ OSS ═══
        public static UpdateOption<OssProvider?> OSSProvider { get; } = UpdateOption.ValueOf<OssProvider?>("OSSPROVIDER", null);
        public static UpdateOption<string?> OSSBucketRegion { get; } = UpdateOption.ValueOf<string?>("OSSBUCKETREGION", null);

        // ═══ Blacklist ═══
        public static UpdateOption<BlackListConfig> BlackList { get; } = UpdateOption.ValueOf<BlackListConfig>("BLACKLIST", BlackListConfig.Empty);

        // ═══ Watchdog ═══
        /// <summary>Bowl (crash monitor / watchdog) executable path.</summary>
        public static UpdateOption<string?> Bowl { get; } = UpdateOption.ValueOf<string?>("BOWL", null);

        // ═══ Logging & Script ═══
        /// <summary>Remote update log / changelog URL.</summary>
        public static UpdateOption<string?> UpdateLogUrl { get; } = UpdateOption.ValueOf<string?>("UPDATELOGURL", null);
        /// <summary>Custom execution script path for pre/post-update actions.</summary>
        public static UpdateOption<string?> Script { get; } = UpdateOption.ValueOf<string?>("SCRIPT", null);

        // ═══ Retry ═══
        /// <summary>Initial retry interval for exponential backoff. Default 1 second.</summary>
        public static UpdateOption<TimeSpan> RetryInterval { get; } = UpdateOption.ValueOf<TimeSpan>("RETRYINTERVAL", TimeSpan.FromSeconds(1));

        // ═══ SignalR Hub ═══
        /// <summary>SignalR Hub configuration for push-based updates.</summary>
        public static UpdateOption<HubConfig?> Hub { get; } = UpdateOption.ValueOf<HubConfig?>("HUB", null);
    }
}
