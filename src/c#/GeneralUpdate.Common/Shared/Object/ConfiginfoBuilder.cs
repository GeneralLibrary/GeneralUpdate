using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Universal ConfigInfo builder class that simplifies creation of update configurations.
    /// Only requires three essential parameters (UpdateUrl, Token, Scheme) while automatically
    /// generating platform-appropriate defaults for all other configuration items.
    /// Inspired by zero-configuration design patterns from projects like Velopack.
    /// </summary>
    public class ConfiginfoBuilder
    {
        /// <summary>
        /// Default blacklisted file format extensions that are automatically excluded from updates.
        /// </summary>
        public static readonly string[] DefaultBlackFormats = { ".log", ".tmp" };

        private readonly string _updateUrl;
        private readonly string _token;
        private readonly string _scheme;
        
        // Configurable default values
        private string _appName = "App.exe";
        private string _mainAppName = "App.exe";
        private string _clientVersion = "1.0.0";
        private string _upgradeClientVersion = "1.0.0";
        private string _appSecretKey = "default-secret-key";
        private string _productId = "default-product-id";
        private string _installPath;
        private string _updateLogUrl;
        private string _reportUrl;
        private string _bowl;
        private string _script;
        private string _driverDirectory;
        private List<string> _blackFiles;
        private List<string> _blackFormats;
        private List<string> _skipDirectorys;

        /// <summary>
        /// Creates a new ConfiginfoBuilder instance using the specified update URL, authentication token, and scheme.
        /// This is the primary factory method for creating a builder with zero-configuration defaults.
        /// All other configuration properties will be automatically initialized with platform-appropriate defaults.
        /// </summary>
        /// <param name="updateUrl">The API endpoint URL for checking available updates. Must be a valid absolute URI.</param>
        /// <param name="token">The authentication token used for API requests.</param>
        /// <param name="scheme">The URL scheme used for update requests (e.g., "http" or "https").</param>
        /// <returns>A new ConfiginfoBuilder instance with all defaults initialized.</returns>
        /// <exception cref="ArgumentException">Thrown when any required parameter is null, empty, or invalid.</exception>
        public static ConfiginfoBuilder Create(string updateUrl, string token, string scheme)
        {
            return new ConfiginfoBuilder(updateUrl, token, scheme);
        }

        /// <summary>
        /// Initializes a new instance of the ConfiginfoBuilder with required parameters.
        /// Consider using <see cref="Create(string, string, string)"/> for a more fluent API.
        /// </summary>
        /// <param name="updateUrl">The API endpoint URL for checking available updates. Must be a valid absolute URI.</param>
        /// <param name="token">The authentication token used for API requests.</param>
        /// <param name="scheme">The URL scheme used for update requests (e.g., "http" or "https").</param>
        /// <exception cref="ArgumentException">Thrown when any required parameter is null, empty, or invalid.</exception>
        public ConfiginfoBuilder(string updateUrl, string token, string scheme)
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(updateUrl))
                throw new ArgumentException("UpdateUrl cannot be null or empty.", nameof(updateUrl));
            
            if (!Uri.IsWellFormedUriString(updateUrl, UriKind.Absolute))
                throw new ArgumentException("UpdateUrl must be a valid absolute URI.", nameof(updateUrl));
            
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty.", nameof(token));
            
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("Scheme cannot be null or empty.", nameof(scheme));
            
            _updateUrl = updateUrl;
            _token = token;
            _scheme = scheme;
            
            // Initialize platform-specific defaults
            InitializePlatformDefaults();
        }

        /// <summary>
        /// Initializes platform-specific default values based on the current operating system.
        /// </summary>
        private void InitializePlatformDefaults()
        {
            // Try to extract application metadata from project file
            var projectMetadata = TryExtractProjectMetadata();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InitializeWindowsDefaults(projectMetadata);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                InitializeLinuxDefaults(projectMetadata);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                InitializeMacOSDefaults(projectMetadata);
            }
            else
            {
                // Fallback to Linux-style defaults for other Unix-like platforms
                InitializeLinuxDefaults(projectMetadata);
            }
            
            // Initialize common defaults
            _blackFiles = new List<string>();
            _blackFormats = new List<string>(DefaultBlackFormats);
            _skipDirectorys = new List<string>();
        }

        /// <summary>
        /// Represents metadata extracted from a project file.
        /// </summary>
        private class ProjectMetadata
        {
            public string AppName { get; set; }
            public string Version { get; set; }
            public string Company { get; set; }
            public string Authors { get; set; }
        }

        /// <summary>
        /// Attempts to extract application metadata from the project file (csproj).
        /// This implements zero-configuration by reading from the host program's project metadata.
        /// </summary>
        /// <returns>ProjectMetadata with extracted values, or default values if extraction fails.</returns>
        private ProjectMetadata TryExtractProjectMetadata()
        {
            var metadata = new ProjectMetadata();
            
            try
            {
                // Start from the application's base directory
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string currentDirectory = baseDirectory;
                
                // Search up to 3 levels up for a csproj file
                for (int i = 0; i < 3; i++)
                {
                    if (string.IsNullOrEmpty(currentDirectory))
                        break;
                    
                    // Look for csproj files in the current directory
                    string[] csprojFiles = Directory.GetFiles(currentDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
                    
                    if (csprojFiles.Length > 0)
                    {
                        // Use the first csproj file found
                        string csprojPath = csprojFiles[0];
                        ExtractMetadataFromCsproj(csprojPath, metadata);
                        return metadata;
                    }
                    
                    // Move up one directory
                    DirectoryInfo parentDir = Directory.GetParent(currentDirectory);
                    currentDirectory = parentDir?.FullName;
                }
            }
            catch
            {
                // If extraction fails for any reason, return empty metadata
            }
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata fields from a csproj file.
        /// </summary>
        /// <param name="csprojPath">Path to the csproj file.</param>
        /// <param name="metadata">ProjectMetadata object to populate.</param>
        private void ExtractMetadataFromCsproj(string csprojPath, ProjectMetadata metadata)
        {
            try
            {
                string content = File.ReadAllText(csprojPath);
                
                // Extract <AssemblyName>
                metadata.AppName = ExtractXmlElement(content, "AssemblyName");
                
                // If AssemblyName is not specified, use the csproj file name (without extension)
                if (string.IsNullOrEmpty(metadata.AppName))
                {
                    metadata.AppName = Path.GetFileNameWithoutExtension(csprojPath);
                }
                
                // Extract <Version>
                metadata.Version = ExtractXmlElement(content, "Version");
                
                // Extract <Company>
                metadata.Company = ExtractXmlElement(content, "Company");
                
                // Extract <Authors>
                metadata.Authors = ExtractXmlElement(content, "Authors");
            }
            catch
            {
                // If parsing fails, use the file name as fallback for app name
                if (string.IsNullOrEmpty(metadata.AppName))
                {
                    metadata.AppName = Path.GetFileNameWithoutExtension(csprojPath);
                }
            }
        }

        /// <summary>
        /// Extracts the content of an XML element from a string.
        /// </summary>
        /// <param name="content">The XML/csproj content.</param>
        /// <param name="elementName">The name of the XML element to extract.</param>
        /// <returns>The element content, or null if not found.</returns>
        private string ExtractXmlElement(string content, string elementName)
        {
            string openTag = $"<{elementName}>";
            string closeTag = $"</{elementName}>";
            
            int startIndex = content.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                startIndex += openTag.Length;
                int endIndex = content.IndexOf(closeTag, startIndex, StringComparison.OrdinalIgnoreCase);
                if (endIndex > startIndex)
                {
                    return content.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            
            return null;
        }

        /// <summary>
        /// Initializes default values specific to Windows platform.
        /// </summary>
        /// <param name="metadata">The project metadata extracted from csproj file.</param>
        private void InitializeWindowsDefaults(ProjectMetadata metadata)
        {
            // Use the current application's base directory (no admin privileges required)
            // This extracts the path from the host program's runtime location
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // Windows uses backslash as path separator (handled automatically by Path.Combine)
            // Set Windows-specific executable names - prefer detected name from project file
            string appNameBase = metadata?.AppName ?? "App";
            _appName = appNameBase + ".exe";
            _mainAppName = appNameBase + ".exe";
            
            // Set version if available from project metadata
            if (!string.IsNullOrEmpty(metadata?.Version))
            {
                _clientVersion = metadata.Version;
                _upgradeClientVersion = metadata.Version;
            }
            
            // Set product ID from company/authors if available
            if (!string.IsNullOrEmpty(metadata?.Company))
            {
                _productId = metadata.Company;
            }
            else if (!string.IsNullOrEmpty(metadata?.Authors))
            {
                _productId = metadata.Authors;
            }
            
            // Windows typically doesn't need shell scripts for permissions
            _script = string.Empty;
        }

        /// <summary>
        /// Initializes default values specific to Linux platform.
        /// </summary>
        /// <param name="metadata">The project metadata extracted from csproj file.</param>
        private void InitializeLinuxDefaults(ProjectMetadata metadata)
        {
            // Use the current application's base directory (no admin privileges required)
            // This extracts the path from the host program's runtime location
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // Linux uses forward slash as path separator (handled automatically by Path.Combine)
            // Linux executables typically don't have .exe extension - prefer detected name from project file
            string appNameBase = metadata?.AppName ?? "app";
            _appName = appNameBase;
            _mainAppName = appNameBase;
            
            // Set version if available from project metadata
            if (!string.IsNullOrEmpty(metadata?.Version))
            {
                _clientVersion = metadata.Version;
                _upgradeClientVersion = metadata.Version;
            }
            
            // Set product ID from company/authors if available
            if (!string.IsNullOrEmpty(metadata?.Company))
            {
                _productId = metadata.Company;
            }
            else if (!string.IsNullOrEmpty(metadata?.Authors))
            {
                _productId = metadata.Authors;
            }
            
            // Default shell script for granting file permissions on Linux
            _script = @"#!/bin/bash
chmod +x ""$1""
";
        }

        /// <summary>
        /// Initializes default values specific to macOS platform.
        /// </summary>
        /// <param name="metadata">The project metadata extracted from csproj file.</param>
        private void InitializeMacOSDefaults(ProjectMetadata metadata)
        {
            // Use the current application's base directory (no admin privileges required)
            // This extracts the path from the host program's runtime location
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // macOS uses forward slash as path separator (handled automatically by Path.Combine)
            // macOS executables typically don't have .exe extension (similar to Linux) - prefer detected name from project file
            string appNameBase = metadata?.AppName ?? "app";
            _appName = appNameBase;
            _mainAppName = appNameBase;
            
            // Set version if available from project metadata
            if (!string.IsNullOrEmpty(metadata?.Version))
            {
                _clientVersion = metadata.Version;
                _upgradeClientVersion = metadata.Version;
            }
            
            // Set product ID from company/authors if available
            if (!string.IsNullOrEmpty(metadata?.Company))
            {
                _productId = metadata.Company;
            }
            else if (!string.IsNullOrEmpty(metadata?.Authors))
            {
                _productId = metadata.Authors;
            }
            
            // Default shell script for granting file permissions on macOS
            _script = @"#!/bin/bash
chmod +x ""$1""
";
        }

        /// <summary>
        /// Sets the application name (executable to start after update).
        /// </summary>
        /// <param name="appName">The name of the application executable.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetAppName(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("AppName cannot be null or empty.", nameof(appName));
            
            _appName = appName;
            return this;
        }

        /// <summary>
        /// Sets the main application name.
        /// </summary>
        /// <param name="mainAppName">The name of the main application without file extension.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetMainAppName(string mainAppName)
        {
            if (string.IsNullOrWhiteSpace(mainAppName))
                throw new ArgumentException("MainAppName cannot be null or empty.", nameof(mainAppName));
            
            _mainAppName = mainAppName;
            return this;
        }

        /// <summary>
        /// Sets the client version.
        /// </summary>
        /// <param name="clientVersion">The current version of the client application.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetClientVersion(string clientVersion)
        {
            if (string.IsNullOrWhiteSpace(clientVersion))
                throw new ArgumentException("ClientVersion cannot be null or empty.", nameof(clientVersion));
            
            _clientVersion = clientVersion;
            return this;
        }

        /// <summary>
        /// Sets the upgrade client version.
        /// </summary>
        /// <param name="upgradeClientVersion">The current version of the upgrade application.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetUpgradeClientVersion(string upgradeClientVersion)
        {
            if (string.IsNullOrWhiteSpace(upgradeClientVersion))
                throw new ArgumentException("UpgradeClientVersion cannot be null or empty.", nameof(upgradeClientVersion));
            
            _upgradeClientVersion = upgradeClientVersion;
            return this;
        }

        /// <summary>
        /// Sets the application secret key.
        /// </summary>
        /// <param name="appSecretKey">The secret key used for authentication.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetAppSecretKey(string appSecretKey)
        {
            if (string.IsNullOrWhiteSpace(appSecretKey))
                throw new ArgumentException("AppSecretKey cannot be null or empty.", nameof(appSecretKey));
            
            _appSecretKey = appSecretKey;
            return this;
        }

        /// <summary>
        /// Sets the product identifier.
        /// </summary>
        /// <param name="productId">The unique product identifier.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetProductId(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId cannot be null or empty.", nameof(productId));
            
            _productId = productId;
            return this;
        }

        /// <summary>
        /// Sets the installation path.
        /// </summary>
        /// <param name="installPath">The installation path where application files are located.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetInstallPath(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                throw new ArgumentException("InstallPath cannot be null or empty.", nameof(installPath));
            
            _installPath = installPath;
            return this;
        }

        /// <summary>
        /// Sets the update log URL.
        /// </summary>
        /// <param name="updateLogUrl">The URL address for the update log webpage.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetUpdateLogUrl(string updateLogUrl)
        {
            if (!string.IsNullOrWhiteSpace(updateLogUrl) && !Uri.IsWellFormedUriString(updateLogUrl, UriKind.Absolute))
                throw new ArgumentException("UpdateLogUrl must be a valid absolute URI.", nameof(updateLogUrl));
            
            _updateLogUrl = updateLogUrl;
            return this;
        }

        /// <summary>
        /// Sets the report URL.
        /// </summary>
        /// <param name="reportUrl">The API endpoint URL for reporting update status and results.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetReportUrl(string reportUrl)
        {
            if (!string.IsNullOrWhiteSpace(reportUrl) && !Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                throw new ArgumentException("ReportUrl must be a valid absolute URI.", nameof(reportUrl));
            
            _reportUrl = reportUrl;
            return this;
        }

        /// <summary>
        /// Sets the bowl process name.
        /// </summary>
        /// <param name="bowl">The process name that should be terminated before starting the update.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetBowl(string bowl)
        {
            _bowl = bowl;
            return this;
        }

        /// <summary>
        /// Sets the shell script content.
        /// </summary>
        /// <param name="script">Shell script content used to grant file permissions on Linux/Unix systems.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetScript(string script)
        {
            _script = script;
            return this;
        }

        /// <summary>
        /// Sets the driver directory.
        /// </summary>
        /// <param name="driverDirectory">The directory path containing driver files for driver update functionality.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetDriverDirectory(string driverDirectory)
        {
            _driverDirectory = driverDirectory;
            return this;
        }

        /// <summary>
        /// Sets the list of blacklisted files.
        /// </summary>
        /// <param name="blackFiles">List of specific files that should be excluded from the update process.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetBlackFiles(List<string> blackFiles)
        {
            _blackFiles = blackFiles ?? new List<string>();
            return this;
        }

        /// <summary>
        /// Sets the list of blacklisted file formats.
        /// </summary>
        /// <param name="blackFormats">List of file format extensions that should be excluded from the update process.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetBlackFormats(List<string> blackFormats)
        {
            _blackFormats = blackFormats ?? new List<string>();
            return this;
        }

        /// <summary>
        /// Sets the list of directories to skip.
        /// </summary>
        /// <param name="skipDirectorys">List of directory paths that should be skipped during the update process.</param>
        /// <returns>The current ConfiginfoBuilder instance for method chaining.</returns>
        public ConfiginfoBuilder SetSkipDirectorys(List<string> skipDirectorys)
        {
            _skipDirectorys = skipDirectorys ?? new List<string>();
            return this;
        }

        /// <summary>
        /// Builds and returns a complete Configinfo object with all configured and default values.
        /// </summary>
        /// <returns>A fully configured Configinfo instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the builder is in an invalid state.</exception>
        public Configinfo Build()
        {
            // Create the Configinfo object with all values
            var configinfo = new Configinfo
            {
                UpdateUrl = _updateUrl,
                Token = _token,
                Scheme = _scheme,
                AppName = _appName,
                MainAppName = _mainAppName,
                ClientVersion = _clientVersion,
                UpgradeClientVersion = _upgradeClientVersion,
                AppSecretKey = _appSecretKey,
                ProductId = _productId,
                InstallPath = _installPath,
                UpdateLogUrl = _updateLogUrl,
                ReportUrl = _reportUrl,
                Bowl = _bowl,
                Script = _script,
                DriverDirectory = _driverDirectory,
                BlackFiles = _blackFiles,
                BlackFormats = _blackFormats,
                SkipDirectorys = _skipDirectorys
            };

            // Validate the built configuration
            try
            {
                configinfo.Validate();
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Failed to build valid Configinfo: {ex.Message}", ex);
            }

            return configinfo;
        }
    }
}
