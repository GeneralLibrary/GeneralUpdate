using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Convenience accessor for UpdateOption constants.
    /// Maps to the underlying UpdateOption singleton constants.
    /// New code should use UpdateOptions.X instead of UpdateOption.X.
    /// </summary>
    public static class UpdateOptions
    {
        // ═══ Core ═══
        public static UpdateOption<int> AppType => UpdateOption.ValueOf<int>("APPTYPE");

        // ═══ Existing options (backward-compatible) ═══
        public static UpdateOption<Encoding> Encoding => UpdateOption.Encoding;
        public static UpdateOption<string> Format => UpdateOption.Format;
        public static UpdateOption<int?> DownloadTimeout => UpdateOption.DownloadTimeOut;
        public static UpdateOption<bool?> DriveEnabled => UpdateOption.Drive;
        public static UpdateOption<bool?> PatchEnabled => UpdateOption.Patch;
        public static UpdateOption<bool?> BackupEnabled => UpdateOption.BackUp;
        public static UpdateOption<UpdateMode?> Mode => UpdateOption.Mode;
        public static UpdateOption<bool> Silent => UpdateOption.EnableSilentUpdate;

        // ═══ New options ═══
        public static readonly UpdateOption<string?> UpdateUrl = UpdateOption.ValueOf<string?>("UPDATEURL");
        public static readonly UpdateOption<string> AppSecretKey = UpdateOption.ValueOf<string>("APPSECRETKEY");
        public static readonly UpdateOption<string> AppName = UpdateOption.ValueOf<string>("APPNAME");
        public static readonly UpdateOption<string> MainAppName = UpdateOption.ValueOf<string>("MAINAPPNAME");
        public static readonly UpdateOption<string> InstallPath = UpdateOption.ValueOf<string>("INSTALLPATH");
        public static readonly UpdateOption<string> ClientVersion = UpdateOption.ValueOf<string>("CLIENTVERSION");
        public static readonly UpdateOption<string?> UpgradeClientVersion = UpdateOption.ValueOf<string?>("UPGRADECLIENTVERSION");
        public static readonly UpdateOption<int?> Platform = UpdateOption.ValueOf<int?>("PLATFORM");
        public static readonly UpdateOption<bool> SilentAutoInstall = UpdateOption.ValueOf<bool>("SILENTAUTOINSTALL");
        public static readonly UpdateOption<int> MaxConcurrency = UpdateOption.ValueOf<int>("MAXCONCURRENCY");
        public static readonly UpdateOption<bool> EnableResume = UpdateOption.ValueOf<bool>("ENABLERESUME");
        public static readonly UpdateOption<int> RetryCount = UpdateOption.ValueOf<int>("RETRYCOUNT");
        public static readonly UpdateOption<bool> VerifyChecksum = UpdateOption.ValueOf<bool>("VERIFYCHECKSUM");
        public static readonly UpdateOption<string?> ReportUrl = UpdateOption.ValueOf<string?>("REPORTURL");
        public static readonly UpdateOption<string?> ProductId = UpdateOption.ValueOf<string?>("PRODUCTID");
        public static readonly UpdateOption<string?> PermissionScript = UpdateOption.ValueOf<string?>("PERMISSIONSCRIPT");
        public static readonly UpdateOption<string?> Scheme = UpdateOption.ValueOf<string?>("SCHEME");
        public static readonly UpdateOption<string?> Token = UpdateOption.ValueOf<string?("TOKEN");

        // ═══ OSS ═══
        public static readonly UpdateOption<int?> OSSProvider = UpdateOption.ValueOf<int?>("OSSPROVIDER");
        public static readonly UpdateOption<string?> OSSBucketRegion = UpdateOption.ValueOf<string?>("OSSBUCKETREGION");
    }
}
