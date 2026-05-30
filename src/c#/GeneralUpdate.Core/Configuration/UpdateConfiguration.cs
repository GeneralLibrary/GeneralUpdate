using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Abstract base configuration class containing common fields shared by all configuration objects.
    ///     Serves as the base class for the user-facing configuration (<see cref="UpdateRequest" />), the internal
    ///     runtime state (<see cref="UpdateContext" />), and the inter-process communication parameters
    ///     (<see cref="ProcessContract" />), managing common properties in a single place to reduce code duplication.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class defines the configuration items common to the entire update workflow, including application
    ///         names, installation path, version numbers, authentication information, exclusion lists (blacklisted
    ///         files, formats, and directories), and network communication parameters (URL scheme, token, etc.).
    ///     </para>
    ///     <para>
    ///         <c>UpdateAppName</c> and <c>InstallPath</c> provide sensible defaults (<c>"Update.exe"</c> and the
    ///         current application directory, respectively), so the system can function even when these values are
    ///         not explicitly configured.
    ///     </para>
    ///     <para>
    ///         This class is abstract and cannot be instantiated directly. It must be used through derived classes
    ///         (<see cref="UpdateRequest" />, <see cref="UpdateContext" />).
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateRequest" />
    /// <seealso cref="UpdateContext" />
    /// <seealso cref="ProcessContract" />
    public abstract class UpdateConfiguration
    {
        /// <summary>
        ///     The executable file name of the updater application (e.g., "Update.exe").
        ///     When the client needs to launch the upgrade process, this name is used to locate and start the updater.
        /// </summary>
        /// <remarks>
        ///     Defaults to <c>"Update.exe"</c>. If the updater uses a different filename, it must be configured via
        ///     <see cref="UpdateRequestBuilder.SetUpgradeAppName" />.
        /// </remarks>
        public string UpdateAppName { get; set; } = "Update.exe";

        /// <summary>
        ///     The executable file name of the main application.
        ///     Used to identify the main application process that will be updated.
        /// </summary>
        /// <remarks>
        ///     This property is validated to be non-empty in <see cref="UpdateRequest.Validate" />.
        ///     In <see cref="ConfigurationMapper.MapToProcessContract" />, this value is mapped to the
        ///     <see cref="ProcessContract.AppName" /> property.
        /// </remarks>
        public string MainAppName { get; set; }

        /// <summary>
        ///     The installation path of the application files.
        ///     This is the root directory for all update file operations.
        /// </summary>
        /// <remarks>
        ///     Defaults to <c>AppDomain.CurrentDomain.BaseDirectory</c>, the base directory of the currently running
        ///     application. Typically, manual configuration is not required, but it must be explicitly set when
        ///     update files need to be installed to a non-default path.
        /// </remarks>
        public string InstallPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        ///     The directory path where the updater executable is located (optional).
        ///     Can be an absolute path or a relative path relative to <see cref="InstallPath" />.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         When this property is set, the upgrade process starts from the <c>UpdatePath</c> directory
        ///         instead of the <see cref="InstallPath" /> directory.
        ///     </para>
        ///     <para>
        ///         If this property is null or empty, it falls back to <see cref="InstallPath" /> for backward compatibility.
        ///     </para>
        ///     <para>
        ///         Example: When set to <c>"Upgrade"</c>, the updater will be located at
        ///         <c>InstallPath/Upgrade/UpdateAppName</c>.
        ///     </para>
        /// </remarks>
        public string UpdatePath { get; set; }

        /// <summary>
        ///     The URL of the update log web page.
        ///     Users can view detailed version change records at this address.
        /// </summary>
        /// <remarks>
        ///     In <see cref="UpdateRequest.Validate" />, if this property is set, it is validated to be a valid absolute URI.
        /// </remarks>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        ///     The application secret key used for authentication.
        ///     This key is required when requesting update information from the update server.
        /// </summary>
        /// <remarks>
        ///     This property is required and is validated to be non-empty in <see cref="UpdateRequest.Validate" />.
        /// </remarks>
        public string AppSecretKey { get; set; }

        /// <summary>
        ///     The current version number of the client application.
        ///     The format should follow semantic versioning conventions (e.g., "1.0.0").
        /// </summary>
        /// <remarks>
        ///     This property is required and is validated to be non-empty in <see cref="UpdateRequest.Validate" />.
        ///     Comparing <c>ClientVersion</c> against the latest version from the server determines whether the
        ///     main application needs updating (<see cref="UpdateContext.IsMainUpdate" />).
        /// </remarks>
        public string ClientVersion { get; set; }

        /// <summary>
        ///     A list of specific files to exclude from the update process.
        ///     Files in the blacklist will be skipped during update operations and will not be overwritten or deleted.
        /// </summary>
        /// <remarks>
        ///     Together with <see cref="Formats" /> and <see cref="Directories" />, this forms the update
        ///     exclusion strategy that protects critical files from being affected by the update.
        /// </remarks>
        public List<string> Files { get; set; }

        /// <summary>
        ///     A list of file format extensions to exclude from the update process.
        ///     For example: <c>[".log", ".tmp", ".cache"]</c> will skip all files with these extensions.
        /// </summary>
        /// <remarks>
        ///     This is a bulk exclusion mechanism, useful for skipping entire categories of files.
        ///     It complements <see cref="Files" />, which handles individual file exclusions.
        /// </remarks>
        public List<string> Formats { get; set; }

        /// <summary>
        ///     A list of directory paths to skip during the update process.
        ///     Entire directory trees in this list will be ignored and excluded from all update operations.
        /// </summary>
        public List<string> Directories { get; set; }

        /// <summary>
        ///     Returns a unified <see cref="BlackPolicy"/> view of the three blacklist properties.
        /// </summary>
        public BlackPolicy ToBlackPolicy() => new(
            Files?.AsReadOnly(),
            Formats?.AsReadOnly(),
            Directories?.AsReadOnly()
        );

        /// <summary>
        ///     The API endpoint URL for reporting update status and results.
        ///     Update progress and completion status are sent to this URL.
        /// </summary>
        /// <remarks>
        ///     This URL is mapped to <see cref="ProcessContract.ReportUrl" /> in
        ///     <see cref="ConfigurationMapper.MapToProcessContract" /> and is called back by the upgrade process
        ///     after the update completes.
        /// </remarks>
        public string ReportUrl { get; set; }

        /// <summary>
        ///     The name of the process to terminate before starting the update.
        ///     Typically used to shut down conflicting background processes (e.g., the "Bowl" process).
        /// </summary>
        /// <remarks>
        ///     In the update workflow, the system attempts to terminate the process matching this name before
        ///     launching the upgrade process, to avoid file-locking issues that could cause the update to fail.
        /// </remarks>
        public string Bowl { get; set; }

        /// <summary>
        ///     The URL scheme used for update requests (e.g., "http" or "https").
        ///     This scheme determines the protocol used when communicating with the update server.
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        ///     The authentication token used for API requests.
        ///     This token is included in HTTP request headers when communicating with the update server.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        ///     The directory path containing driver files, used for driver-based update functionality.
        ///     When driver updates are enabled, the system locates and installs driver files from this directory.
        /// </summary>
        public string DriverDirectory { get; set; }

        /// <summary>
        ///     The current update role — determines which application is launched by
        ///     <see cref="Strategy.AbstractStrategy.StartAppAsync" />.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         When set to <see cref="AppType.Client" />, launches <c>UpdateAppName</c> (the upgrade process).
        ///     </para>
        ///     <para>
        ///         When set to <see cref="AppType.Upgrade" />, launches the <c>MainAppName</c> main application
        ///         and the Bowl process.
        ///     </para>
        /// </remarks>
        public AppType? AppType { get; set; }

        /// <summary>
        ///     The API endpoint URL used to check for available updates.
        /// </summary>
        public string UpdateUrl { get; set; }

        /// <summary>
        ///     The current version number of the updater (the update client itself).
        /// </summary>
        public string UpgradeClientVersion { get; set; }

        /// <summary>
        ///     The unique product identifier for the current application.
        /// </summary>
        public string ProductId { get; set; }
    }
}
