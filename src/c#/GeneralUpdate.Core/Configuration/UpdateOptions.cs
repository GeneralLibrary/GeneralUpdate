using System;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Framework-level constant definitions for update options.
    ///     Each option has a unique string name and a sensible default value.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOptions</c> is a static class that centrally defines all configurable options for the update
    ///         framework as <see cref="UpdateOption{T}" /> instances. Each option contains a unique string name and
    ///         a framework-defined default value.
    ///     </para>
    ///     <para>
    ///         Business-related configuration (URLs, secrets, application names, etc.) does not belong in this class
    ///         and should be placed in <see cref="Configinfo" /> or <see cref="BaseConfigInfo" />. This class is
    ///         concerned only with behavioral options (such as whether to enable resumable downloads, retry count,
    ///         concurrency level, etc.).
    ///     </para>
    ///     <para>
    ///         Option values are stored and retrieved via <see cref="UpdateOption.ValueOf{T}(string, T)" />, which
    ///         uses a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}" /> to guarantee
    ///         that each name maps to a unique option instance (singleton pattern).
    ///     </para>
    ///     <para>
    ///         Default values for each option:
    ///         <list type="bullet">
    ///             <item><description>Concurrent downloads default to 3 (<see cref="MaxConcurrency" />)</description></item>
    ///             <item><description>Resumable downloads default to enabled (<see cref="EnableResume" />)</description></item>
    ///             <item><description>Download timeout defaults to 30 seconds (<see cref="DownloadTimeout" />)</description></item>
    ///             <item><description>Differential patching defaults to enabled (<see cref="PatchEnabled" />)</description></item>
    ///             <item><description>Pre-update backup defaults to enabled (<see cref="BackupEnabled" />)</description></item>
    ///             <item><description>Silent update defaults to disabled (<see cref="Silent" />)</description></item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOption{T}" />
    /// <seealso cref="Configinfo" />
    /// <seealso cref="BaseConfigInfo" />
    public static class UpdateOptions
    {
        // ════════════════════════════════════════
        //  核心选项
        // ════════════════════════════════════════

        /// <summary>
        ///     The application role type — <see cref="AppType.Client" />, <see cref="AppType.Upgrade" />,
        ///     or <see cref="AppType.Oss" />.
        ///     Defaults to <see cref="AppType.Client" />.
        /// </summary>
        public static UpdateOption<AppType> AppType { get; } = UpdateOption.ValueOf<AppType>("APPTYPE", Configuration.AppType.Client);

        // ════════════════════════════════════════
        //  Diff Mode
        // ════════════════════════════════════════

        /// <summary>
        ///     The diff/patch generation mode — <see cref="DiffMode.Serial" /> (serial) or
        ///     <see cref="DiffMode.Parallel" /> (parallel).
        ///     Defaults to <see cref="DiffMode.Serial" />.
        /// </summary>
        public static UpdateOption<DiffMode> DiffMode { get; } = UpdateOption.ValueOf<DiffMode>("DIFFMODE", Configuration.DiffMode.Serial);

        // ════════════════════════════════════════
        //  Backward Compatibility Options
        // ════════════════════════════════════════

        /// <summary>
        ///     The text encoding used for update package compression.
        ///     Defaults to <see cref="System.Text.Encoding.UTF8" />.
        /// </summary>
        public static UpdateOption<Encoding> Encoding { get; } = UpdateOption.ValueOf<Encoding>("COMPRESSENCODING", System.Text.Encoding.UTF8);

        /// <summary>
        ///     The compression format used for update packages.
        ///     Defaults to <see cref="Configuration.Format.Zip" />.
        /// </summary>
        public static UpdateOption<Format> Format { get; } = UpdateOption.ValueOf<Format>("COMPRESSFORMAT", Configuration.Format.Zip);

        /// <summary>
        ///     The download operation timeout in seconds.
        ///     Defaults to 30 seconds.
        /// </summary>
        public static UpdateOption<int?> DownloadTimeout { get; } = UpdateOption.ValueOf<int?>("DOWNLOADTIMEOUT", 30);

        /// <summary>
        ///     Whether differential patch updates are enabled.
        ///     Defaults to <c>true</c>.
        /// </summary>
        public static UpdateOption<bool?> PatchEnabled { get; } = UpdateOption.ValueOf<bool?>("PATCH", true);

        /// <summary>
        ///     Whether pre-update backup is enabled.
        ///     Defaults to <c>true</c>.
        /// </summary>
        public static UpdateOption<bool?> BackupEnabled { get; } = UpdateOption.ValueOf<bool?>("BACKUP", true);

        /// <summary>
        ///     Whether silent background update mode is enabled.
        ///     Defaults to <c>false</c>.
        /// </summary>
        public static UpdateOption<bool> Silent { get; } = UpdateOption.ValueOf<bool>("ENABLESILENTUPDATE", false);

        // ════════════════════════════════════════
        //  Silent Mode Options
        // ════════════════════════════════════════

        /// <summary>
        ///     The polling interval (in minutes) for checking updates in silent mode.
        ///     Defaults to 60 minutes.
        /// </summary>
        public static UpdateOption<int> SilentPollIntervalMinutes { get; } = UpdateOption.ValueOf<int>("SILENTPOLLINTERVALMINUTES", 60);

        /// <summary>
        ///     Whether to automatically launch the client application after the upgrade process completes in silent mode.
        ///     Defaults to <c>true</c>.
        /// </summary>
        public static UpdateOption<bool> LaunchClientAfterUpdate { get; } = UpdateOption.ValueOf<bool>("LAUNCHCLIENTAFTERUPDATE", true);

        // ════════════════════════════════════════
        //  Concurrency and Resume Options
        // ════════════════════════════════════════

        /// <summary>
        ///     The maximum number of concurrent download operations.
        ///     Defaults to 3.
        /// </summary>
        public static UpdateOption<int> MaxConcurrency { get; } = UpdateOption.ValueOf<int>("MAXCONCURRENCY", 3);

        /// <summary>
        ///     Whether HTTP resumable downloads (based on Range headers) are enabled.
        ///     Defaults to <c>true</c>.
        /// </summary>
        public static UpdateOption<bool> EnableResume { get; } = UpdateOption.ValueOf<bool>("ENABLERESUME", true);

        // ════════════════════════════════════════
        //  Fault Tolerance and Retry Options
        // ════════════════════════════════════════

        /// <summary>
        ///     The maximum number of retry attempts on download failure.
        ///     Defaults to 3.
        /// </summary>
        public static UpdateOption<int> RetryCount { get; } = UpdateOption.ValueOf<int>("RETRYCOUNT", 3);

        /// <summary>
        ///     Whether to perform SHA256 checksum verification after download.
        ///     Defaults to <c>true</c>.
        /// </summary>
        public static UpdateOption<bool> VerifyChecksum { get; } = UpdateOption.ValueOf<bool>("VERIFYCHECKSUM", true);

        /// <summary>
        ///     The initial retry interval for the exponential backoff strategy.
        ///     Defaults to 1 second.
        /// </summary>
        public static UpdateOption<TimeSpan> RetryInterval { get; } = UpdateOption.ValueOf<TimeSpan>("RETRYINTERVAL", TimeSpan.FromSeconds(1));
    }
}
