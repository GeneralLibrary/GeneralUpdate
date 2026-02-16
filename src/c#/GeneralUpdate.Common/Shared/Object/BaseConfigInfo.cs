using System;
using System.Collections.Generic;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Base configuration class containing common fields shared across all configuration objects.
    /// This class serves as the foundation for user-facing configuration (Configinfo),
    /// internal runtime state (GlobalConfigInfo), and inter-process communication (ProcessInfo).
    /// </summary>
    public abstract class BaseConfigInfo
    {
        /// <summary>
        /// The name of the application that needs to be started after update.
        /// This is the executable name without extension (e.g., "MyApp" for MyApp.exe).
        /// Default value is "Update.exe".
        /// </summary>
        public string AppName { get; set; } = "Update.exe";

        /// <summary>
        /// The name of the main application without file extension.
        /// Used to identify the primary application process that will be updated.
        /// </summary>
        public string MainAppName { get; set; }

        /// <summary>
        /// The installation path where application files are located.
        /// This is the root directory used for update file operations.
        /// Default value is the current program's running directory.
        /// </summary>
        public string InstallPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// The URL address for the update log webpage.
        /// Users can view detailed changelog information at this address.
        /// </summary>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// The application secret key used for authentication.
        /// This key is validated when requesting update information from the server.
        /// </summary>
        public string AppSecretKey { get; set; }

        /// <summary>
        /// The current version of the client application.
        /// Format should follow semantic versioning (e.g., "1.0.0").
        /// </summary>
        public string ClientVersion { get; set; }

        /// <summary>
        /// List of specific files that should be excluded from the update process.
        /// Files in this blacklist will be skipped during update operations.
        /// </summary>
        public List<string> BlackFiles { get; set; }

        /// <summary>
        /// List of file format extensions that should be excluded from the update process.
        /// For example: [".log", ".tmp", ".cache"] will skip all files with these extensions.
        /// </summary>
        public List<string> BlackFormats { get; set; }

        /// <summary>
        /// List of directory paths that should be skipped during the update process.
        /// Entire directories in this list will be ignored during update operations.
        /// </summary>
        public List<string> SkipDirectorys { get; set; }

        /// <summary>
        /// The API endpoint URL for reporting update status and results.
        /// Update progress and completion status will be sent to this URL.
        /// </summary>
        public string ReportUrl { get; set; }

        /// <summary>
        /// The process name that should be terminated before starting the update.
        /// This is typically used to close conflicting processes (e.g., "Bowl" process).
        /// </summary>
        public string Bowl { get; set; }

        /// <summary>
        /// The URL scheme used for update requests (e.g., "http" or "https").
        /// This determines the protocol used for server communication.
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        /// The authentication token used for API requests.
        /// This token is included in HTTP headers when communicating with the update server.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Shell script content used to grant file permissions on Linux/Unix systems.
        /// This script is executed after update to ensure proper file permissions.
        /// </summary>
        public string Script { get; set; }
        
        /// <summary>
        /// The directory path containing driver files for driver update functionality.
        /// Used when DriveEnabled is true to locate and install driver files during updates.
        /// </summary>
        public string DriverDirectory { get; set; }
    }
}
