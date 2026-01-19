using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Inter-process communication parameter object.
    /// This class is serialized to JSON and passed to the upgrade process, enabling
    /// the separate upgrade application to perform updates with the correct configuration.
    /// 
    /// Design Notes:
    /// - All fields are serialized using [JsonPropertyName] attributes
    /// - Constructor performs validation to ensure required parameters are provided
    /// - Field naming differs slightly from other config classes for backward compatibility
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Default constructor for deserialization.
        /// </summary>
        public ProcessInfo() { }

        /// <summary>
        /// Parameterized constructor with validation for creating ProcessInfo instances.
        /// All parameters are validated to ensure the upgrade process receives valid configuration.
        /// </summary>
        /// <param name="appName">The name of the application to start after update (maps from MainAppName)</param>
        /// <param name="installPath">The installation directory path (must exist)</param>
        /// <param name="currentVersion">The current version before update</param>
        /// <param name="lastVersion">The target version after update</param>
        /// <param name="updateLogUrl">The URL for viewing update logs</param>
        /// <param name="compressEncoding">The encoding used for compressed files</param>
        /// <param name="compressFormat">The compression format (ZIP, 7Z, etc.)</param>
        /// <param name="downloadTimeOut">The download timeout in seconds (must be > 0)</param>
        /// <param name="appSecretKey">The application secret key for authentication</param>
        /// <param name="updateVersions">List of version information to update (must not be empty)</param>
        /// <param name="reportUrl">The URL for reporting update status</param>
        /// <param name="backupDirectory">The directory path for backup files</param>
        /// <param name="bowl">The process name to terminate before updating</param>
        /// <param name="scheme">The URL scheme for update requests</param>
        /// <param name="token">The authentication token</param>
        /// <param name="script">The Linux permission script</param>
        /// <param name="blackFileFormats">List of file format extensions to skip</param>
        /// <param name="blackFiles">List of specific files to skip</param>
        /// <param name="skipDirectories">List of directories to skip</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="ArgumentException">Thrown when parameters fail validation</exception>
        public ProcessInfo(string appName
            , string installPath
            , string currentVersion
            , string lastVersion
            , string updateLogUrl
            , Encoding compressEncoding
            , string compressFormat
            , int downloadTimeOut
            , string appSecretKey
            , List<VersionInfo> updateVersions
            , string reportUrl
            , string backupDirectory
            , string bowl
            , string scheme
            , string token
            , string script
            , List<string> blackFileFormats
            , List<string> blackFiles
            , List<string> skipDirectories)
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
            
            // Validate update versions collection
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = updateVersions;
            
            // Set reporting and backup parameters
            ReportUrl = reportUrl ?? throw new ArgumentNullException(nameof(reportUrl));
            BackupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
            
            // Set optional parameters
            Bowl = bowl;
            Scheme = scheme;
            Token = token;
            Script = script;
            
            // Set blacklist parameters
            BlackFileFormats = blackFileFormats;
            BlackFiles = blackFiles;
            SkipDirectorys = skipDirectories;
        }

        /// <summary>
        /// The name of the application to start after the update completes.
        /// Note: In ProcessInfo, this field holds the MainAppName value from other config classes.
        /// </summary>
        [JsonPropertyName("AppName")]
        public string AppName { get; set; }

        /// <summary>
        /// The installation directory where files will be updated.
        /// All update operations are performed relative to this path.
        /// </summary>
        [JsonPropertyName("InstallPath")]
        public string InstallPath { get; set; }

        /// <summary>
        /// The current version of the application before the update.
        /// Note: Maps from GlobalConfigInfo.ClientVersion.
        /// </summary>
        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        /// <summary>
        /// The target version after the update completes.
        /// This is the latest version available from the update server.
        /// </summary>
        [JsonPropertyName("LastVersion")]
        public string LastVersion { get; set; }

        /// <summary>
        /// The URL where users can view detailed update logs and changelogs.
        /// </summary>
        [JsonPropertyName("UpdateLogUrl")]
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// The text encoding used for compressing/decompressing update packages.
        /// Stored as WebName string (e.g., "utf-8", "ascii").
        /// </summary>
        [JsonPropertyName("CompressEncoding")]
        public string CompressEncoding { get; set; }

        /// <summary>
        /// The compression format of update packages (e.g., "ZIP", "7Z").
        /// </summary>
        [JsonPropertyName("CompressFormat")]
        public string CompressFormat { get; set; }

        /// <summary>
        /// The timeout duration in seconds for downloading update packages.
        /// Download operations will fail if they exceed this duration.
        /// </summary>
        [JsonPropertyName("DownloadTimeOut")]
        public int DownloadTimeOut { get; set; }

        /// <summary>
        /// The application secret key used for authenticating update requests.
        /// </summary>
        [JsonPropertyName("AppSecretKey")]
        public string AppSecretKey { get; set; }

        /// <summary>
        /// List of version information objects describing all versions to be updated.
        /// Can contain multiple versions for incremental updates.
        /// </summary>
        [JsonPropertyName("UpdateVersions")]
        public List<VersionInfo> UpdateVersions { get; set; }

        /// <summary>
        /// The API endpoint URL for reporting update progress and completion status.
        /// </summary>
        [JsonPropertyName("ReportUrl")]
        public string ReportUrl { get; set; }
        
        /// <summary>
        /// The directory path where current version files are backed up before updating.
        /// Used for rollback if the update fails.
        /// </summary>
        [JsonPropertyName("BackupDirectory")]
        public string BackupDirectory { get; set; }
        
        /// <summary>
        /// The name of a process that should be terminated before starting the update.
        /// Typically used to close conflicting background processes.
        /// </summary>
        [JsonPropertyName("Bowl")]
        public string Bowl { get; set; }
        
        /// <summary>
        /// The URL scheme for update server communication (e.g., "http", "https").
        /// </summary>
        [JsonPropertyName("Scheme")]
        public string Scheme { get; set; }
    
        /// <summary>
        /// The authentication token included in HTTP headers for API requests.
        /// </summary>
        [JsonPropertyName("Token")]
        public string Token { get; set; }
        
        /// <summary>
        /// List of file format extensions that should be excluded from updates.
        /// Note: Different property name (BlackFileFormats) than in other config classes (BlackFormats).
        /// </summary>
        [JsonPropertyName("BlackFileFormats")]
        public List<string> BlackFileFormats { get; set; }
        
        /// <summary>
        /// List of specific file names that should be excluded from updates.
        /// </summary>
        [JsonPropertyName("BlackFiles")]
        public List<string> BlackFiles { get; set; }

        /// <summary>
        /// List of directory paths that should be skipped during update operations.
        /// </summary>
        [JsonPropertyName("SkipDirectorys")]
        public List<string> SkipDirectorys { get; set; }
        
        /// <summary>
        /// Shell script content for granting file permissions on Linux/Unix systems.
        /// Executed after update to ensure updated files have correct permissions.
        /// </summary>
        [JsonPropertyName("Script")]
        public string Script { get; set; }
    }
}