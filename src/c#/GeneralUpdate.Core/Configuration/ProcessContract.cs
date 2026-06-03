using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Inter-process communication (IPC) parameter object.
    ///     This object is serialized to a JSON string and passed to the upgrade process via process arguments,
    ///     enabling the standalone upgrade application to execute the update operation with the correct configuration.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>ProcessContract</c> is the data transfer contract between the client process and the upgrade process
    ///         in the update workflow. Its lifecycle is as follows:
    ///         <list type="number">
    ///             <item>
    ///                 <description>
    ///                     Created at the end of the client update pipeline by
    ///                     <see cref="ConfigurationMapper.MapToProcessContract" /> from
    ///                     <see cref="UpdateContext" />.
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     Serialized to a JSON string and stored in the <see cref="UpdateContext.ProcessContract" /> property.
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     When the client launches the upgrade process, the JSON string is passed via command-line arguments.
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     The upgrade process deserializes the JSON string, reconstructs the <c>ProcessContract</c> object,
    ///                     and uses its configuration to perform the update.
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         Design notes:
    ///         <list type="bullet">
    ///             <item>
    ///                 <description>
    ///                     All fields are annotated with <see cref="JsonPropertyNameAttribute" /> to ensure consistent
    ///                     serialization names.
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     The constructor performs parameter validation to ensure the upgrade process receives a valid
    ///                     configuration.
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     Some field names differ slightly from other configuration classes to maintain JSON serialization
    ///                     backward compatibility with earlier versions. For example: <c>AppName</c> corresponds to
    ///                     <c>MainAppName</c>, <c>CurrentVersion</c> corresponds to <c>ClientVersion</c>.
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateContext" />
    /// <seealso cref="ConfigurationMapper" />
    /// <seealso cref="VersionEntry" />
    public class ProcessContract
    {
        /// <summary>
        ///     Default parameterless constructor for JSON deserialization.
        /// </summary>
        /// <remarks>
        ///     Required when deserializing a <c>ProcessContract</c> JSON string using
        ///     <see cref="System.Text.Json.JsonSerializer.Deserialize{T}(string, System.Text.Json.JsonSerializerOptions)" />.
        /// </remarks>
        public ProcessContract() { }

        /// <summary>
        ///     Parameterized constructor with validation for creating a <c>ProcessContract</c> instance.
        ///     All parameters are validated to ensure the upgrade process receives a valid configuration.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The constructor validates the following parameters for null or validity:
        ///         <list type="bullet">
        ///             <item>
        ///                 <description><paramref name="appName" />: Must not be null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="installPath" />: Must not be null, and the directory must exist</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="currentVersion" />: Must not be null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="lastVersion" />: Must not be null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="downloadTimeOut" />: Must be greater than or equal to 0</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="appSecretKey" />: Must not be null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="updateVersions" />: Must not be null or an empty collection</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="reportUrl" />: Optional; status reporting is skipped when null or empty</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="backupDirectory" />: Must not be null</description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </remarks>
        /// <param name="appName">
        ///     The application name to launch after the update completes (mapped from <see cref="UpdateConfiguration.MainAppName" />).
        /// </param>
        /// <param name="installPath">The installation directory path (must exist).</param>
        /// <param name="currentVersion">The current version number before the update.</param>
        /// <param name="lastVersion">The target version number after the update.</param>
        /// <param name="updateLogUrl">The URL for viewing the update log.</param>
        /// <param name="compressEncoding">The text encoding used for compression.</param>
        /// <param name="compressFormat">The compression format extension string (e.g., ZIP, 7Z).</param>
        /// <param name="downloadTimeOut">The download timeout in seconds, must be greater than 0.</param>
        /// <param name="appSecretKey">The application secret key used for authentication.</param>
        /// <param name="updateVersions">The list of version information to update; must not be empty.</param>
        /// <param name="reportUrl">The URL for reporting update status.</param>
        /// <param name="backupDirectory">The directory path for backup files.</param>
        /// <param name="bowl">The process name to terminate before the update.</param>
        /// <param name="scheme">The URL scheme for update requests.</param>
        /// <param name="token">The authentication token.</param>
        /// <param name="authScheme">Explicitly selects the HTTP authentication method.</param>
        /// <param name="basicUsername">The username for HTTP Basic Authentication.</param>
        /// <param name="basicPassword">The password for HTTP Basic Authentication.</param>
        /// <param name="driverDirectory">The directory path containing driver files.</param>
        /// <param name="tempPath">The temporary directory path where the client downloaded the update package.</param>
        /// <param name="blackFileFormats">The list of file format extensions to skip.</param>
        /// <param name="blackFiles">The list of specific files to skip.</param>
        /// <param name="skipDirectories">The list of directories to skip.</param>
        /// <param name="upgradePath">The directory where the upgrade executable resides (optional; defaults to <paramref name="installPath" />).</param>
        /// <param name="launchClient">Whether to launch the client application after the update completes (optional; defaults to <c>true</c>).</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="appName" />, <paramref name="installPath" />,
        ///     <paramref name="currentVersion" />, <paramref name="lastVersion" />,
        ///     <paramref name="appSecretKey" />, or
        ///     <paramref name="backupDirectory" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     Thrown when <paramref name="installPath" /> points to a non-existent directory,
        ///     <paramref name="downloadTimeOut" /> is less than 0, or
        ///     <paramref name="updateVersions" /> is null or an empty collection.
        /// </exception>
        public ProcessContract(string appName
            , string installPath
            , string currentVersion
            , string lastVersion
            , string updateLogUrl
            , Encoding compressEncoding
            , string compressFormat
            , int downloadTimeOut
            , string appSecretKey
            , List<VersionEntry> updateVersions
            , string reportUrl
            , string backupDirectory
            , string bowl
            , string scheme
            , string token
            , Security.AuthScheme authScheme
            , string basicUsername
            , string basicPassword
            , string driverDirectory
            , string tempPath
            , List<string> blackFileFormats
            , List<string> blackFiles
            , List<string> skipDirectories
            , string upgradePath = null
            , bool launchClient = true)
        {
            // Validate required string parameters
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            if (!Directory.Exists(installPath)) throw new ArgumentException($"{nameof(installPath)} path does not exist ! {installPath}.");
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            UpdateLogUrl = updateLogUrl;

            // Validate and set compression parameters
            CompressEncoding = compressEncoding.WebName;
            CompressFormat = compressFormat;
            if (downloadTimeOut < 0) throw new ArgumentException("Timeout must be greater than 0 !");
            DownloadTimeOut = downloadTimeOut;

            // Validate authentication parameters
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));

            // Validate update version collection
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = updateVersions;

            // Set report and backup parameters
            // ReportUrl is optional — when not specified, no status reporting is performed.
            ReportUrl = reportUrl;
            BackupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));

            // Set optional parameters
            Bowl = bowl;
            Scheme = scheme;
            Token = token;
            AuthScheme = authScheme;
            BasicUsername = basicUsername;
            BasicPassword = basicPassword;
            DriverDirectory = driverDirectory;
            TempPath = tempPath;

            // Set blacklist parameters
            Formats = blackFileFormats;
            Files = blackFiles;
            Directories = skipDirectories;

            // Set upgrade path (optional — defaults to InstallPath when not set)
            UpdatePath = upgradePath;

            // Set launch flag (defaults to true for backward compatibility)
            LaunchClientAfterUpdate = launchClient;
        }

        /// <summary>
        ///     The name of the application to launch after the update completes.
        ///     Note: In <c>ProcessContract</c>, this field stores the <c>MainAppName</c> value from other configuration classes.
        /// </summary>
        /// <remarks>
        ///     The JSON serialization name is <c>"UpdateAppName"</c> to maintain backward compatibility with
        ///     the serialization format of earlier versions.
        /// </remarks>
        [JsonPropertyName("UpdateAppName")]
        public string AppName { get; set; }

        /// <summary>
        ///     The installation directory where files will be updated.
        ///     All update operations are performed relative to this path.
        /// </summary>
        /// <remarks>
        ///     The constructor validates that this directory exists; otherwise, it throws <see cref="ArgumentException" />.
        /// </remarks>
        [JsonPropertyName("InstallPath")]
        public string InstallPath { get; set; }

        /// <summary>
        ///     The current version number of the application before the update.
        ///     Note: This value is mapped from <see cref="UpdateContext.ClientVersion" />.
        /// </summary>
        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        /// <summary>
        ///     The target version number after the update completes.
        ///     This is the latest available version on the update server.
        /// </summary>
        [JsonPropertyName("LastVersion")]
        public string LastVersion { get; set; }

        /// <summary>
        ///     The URL where users can view detailed update logs and change records.
        /// </summary>
        [JsonPropertyName("UpdateLogUrl")]
        public string UpdateLogUrl { get; set; }

        /// <summary>
        ///     The text encoding used for compressing/decompressing update packages.
        ///     Stored as an <see cref="Encoding.WebName" /> string (e.g., "utf-8", "ascii").
        /// </summary>
        [JsonPropertyName("CompressEncoding")]
        public string CompressEncoding { get; set; }

        /// <summary>
        ///     The compression format of the update package (e.g., "ZIP", "7Z").
        /// </summary>
        [JsonPropertyName("CompressFormat")]
        public string CompressFormat { get; set; }

        /// <summary>
        ///     The timeout for downloading the update package, in seconds.
        ///     If the download operation exceeds this time, it is considered failed.
        /// </summary>
        [JsonPropertyName("DownloadTimeOut")]
        public int DownloadTimeOut { get; set; }

        /// <summary>
        ///     The application secret key used for authenticating update requests.
        /// </summary>
        [JsonPropertyName("AppSecretKey")]
        public string AppSecretKey { get; set; }

        /// <summary>
        ///     The list of version information objects describing all pending updates.
        ///     May contain multiple version entries for incremental updates.
        /// </summary>
        [JsonPropertyName("UpdateVersions")]
        public List<VersionEntry> UpdateVersions { get; set; }

        /// <summary>
        ///     The API endpoint URL for reporting update progress and completion status.
        /// </summary>
        [JsonPropertyName("ReportUrl")]
        public string ReportUrl { get; set; }

        /// <summary>
        ///     The directory path where current version files are backed up before the update.
        ///     Used for rollback in case of update failure.
        /// </summary>
        [JsonPropertyName("BackupDirectory")]
        public string BackupDirectory { get; set; }

        /// <summary>
        ///     The name of the process to terminate before starting the update.
        ///     Typically used to shut down conflicting background processes that may hold file locks.
        /// </summary>
        [JsonPropertyName("Bowl")]
        public string Bowl { get; set; }

        /// <summary>
        ///     The URL scheme used for communicating with the update server (e.g., "http", "https").
        /// </summary>
        [JsonPropertyName("Scheme")]
        public string Scheme { get; set; }

        /// <summary>
        ///     The authentication token included in HTTP request headers for API requests.
        /// </summary>
        [JsonPropertyName("Token")]
        public string Token { get; set; }

        /// <summary>
        ///     Explicitly selects the HTTP authentication method.
        /// </summary>
        [JsonPropertyName("AuthScheme")]
        public Security.AuthScheme AuthScheme { get; set; } = Security.AuthScheme.Hmac;

        /// <summary>
        ///     The username for HTTP Basic Authentication.
        /// </summary>
        [JsonPropertyName("BasicUsername")]
        public string BasicUsername { get; set; }

        /// <summary>
        ///     The password for HTTP Basic Authentication.
        /// </summary>
        [JsonPropertyName("BasicPassword")]
        public string BasicPassword { get; set; }

        /// <summary>
        ///     The list of file format extensions to exclude from the update.
        /// </summary>
        public List<string> Formats { get; set; }

        /// <summary>
        ///     The list of specific file names to exclude from the update.
        /// </summary>
        public List<string> Files { get; set; }

        /// <summary>
        ///     The list of directory paths to skip during update operations.
        /// </summary>
        public List<string> Directories { get; set; }

        /// <summary>
        ///     The directory path containing driver files, used for driver-based update functionality.
        ///     When driver updates are enabled, the system locates and installs driver files from this directory.
        /// </summary>
        [JsonPropertyName("DriverDirectory")]
        public string DriverDirectory { get; set; }

        /// <summary>
        ///     The temporary directory path used by the client to download update packages.
        ///     The upgrade process reads update package files from this path via the update pipeline.
        /// </summary>
        [JsonPropertyName("TempPath")]
        public string TempPath { get; set; }

        /// <summary>
        ///     The directory path where the upgrade executable resides (optional).
        ///     When set, the upgrade process starts from <c>UpdatePath</c> instead of <see cref="InstallPath" />.
        /// </summary>
        [JsonPropertyName("UpdatePath")]
        public string UpdatePath { get; set; }

        /// <summary>
        ///     Whether to launch the client application after the update completes.
        ///     Defaults to <c>true</c>. Set to <c>false</c> to keep the application stopped after the update.
        /// </summary>
        [JsonPropertyName("LaunchClientAfterUpdate")]
        public bool LaunchClientAfterUpdate { get; set; } = true;

        /// <summary>
        ///     The report type for status reporting: 1 = Upgrade (active poll), 2 = Push (SignalR push).
        ///     Passed from ClientStrategy to UpdateStrategy so it uses the same type when reporting.
        /// </summary>
        [JsonPropertyName("ReportType")]
        public int ReportType { get; set; } = 1;
    }
}
