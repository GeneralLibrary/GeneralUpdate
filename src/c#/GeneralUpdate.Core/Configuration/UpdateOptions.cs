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
        public static UpdateOption<int> AppType { get; } = UpdateOption.ValueOf<int>("APPTYPE", Configuration.AppType.ClientApp);

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
        public static UpdateOption<int?> Platform { get; } = UpdateOption.ValueOf<int?>("PLATFORM", null);
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
        public static UpdateOption<int?> OSSProvider { get; } = UpdateOption.ValueOf<int?>("OSSPROVIDER", null);
        public static UpdateOption<string?> OSSBucketRegion { get; } = UpdateOption.ValueOf<string?>("OSSBUCKETREGION", null);

        // ═══ Blacklist ═══
        public static UpdateOption<BlackListConfig> BlackList { get; } = UpdateOption.ValueOf<BlackListConfig>("BLACKLIST", BlackListConfig.Empty);
    }
}
