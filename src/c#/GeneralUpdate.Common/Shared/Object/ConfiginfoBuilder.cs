using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
        public static readonly string[] DefaultBlackFormats;

        static ConfiginfoBuilder()
        {
            DefaultBlackFormats = new string[0];
        }

        private readonly string _updateUrl;
        private readonly string _token;
        private readonly string _scheme;
        
        // Configurable default values
        // Note: AppName and InstallPath defaults are set in Configinfo class itself
        // These are ConfiginfoBuilder-specific defaults to support the builder pattern
        private string _appName = "Update.exe";
        private string _mainAppName;
        private string _clientVersion;
        private string _upgradeClientVersion;
        private string _appSecretKey;
        private string _productId;
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
        /// Creates a new ConfiginfoBuilder instance by loading configuration from update_config.json file.
        /// The configuration file must exist in the running directory and contain all required settings.
        /// Configuration file has the highest priority - all settings must be specified in the JSON file.
        /// </summary>
        /// <returns>A new ConfiginfoBuilder instance with settings loaded from the configuration file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when update_config.json is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the configuration file is invalid or cannot be loaded.</exception>
        public static ConfiginfoBuilder Create()
        {
            // Try to load from configuration file
            var configFromFile = LoadFromConfigFile();
            if (configFromFile != null)
            {
                // Configuration file loaded successfully
                return configFromFile;
            }
            
            // If no config file exists, throw an exception
            throw new FileNotFoundException("Configuration file 'update_config.json' not found in the running directory. Please create this file with the required settings.");
        }

        /// <summary>
        /// Loads configuration from update_config.json file in the running directory.
        /// </summary>
        /// <returns>ConfiginfoBuilder with settings from file, or null if file doesn't exist or is invalid.</returns>
        private static ConfiginfoBuilder LoadFromConfigFile()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
                if (!File.Exists(configPath))
                {
                    return null;
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Configinfo>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (config == null)
                {
                    return null;
                }

                // Create a builder with the loaded configuration
                var builder = new ConfiginfoBuilder(
                    config.UpdateUrl ?? string.Empty, 
                    config.Token ?? string.Empty, 
                    config.Scheme ?? string.Empty);

                // Apply all loaded settings
                if (!string.IsNullOrWhiteSpace(config.AppName))
                    builder.SetAppName(config.AppName);
                if (!string.IsNullOrWhiteSpace(config.MainAppName))
                    builder.SetMainAppName(config.MainAppName);
                if (!string.IsNullOrWhiteSpace(config.ClientVersion))
                    builder.SetClientVersion(config.ClientVersion);
                if (!string.IsNullOrWhiteSpace(config.UpgradeClientVersion))
                    builder.SetUpgradeClientVersion(config.UpgradeClientVersion);
                if (!string.IsNullOrWhiteSpace(config.AppSecretKey))
                    builder.SetAppSecretKey(config.AppSecretKey);
                if (!string.IsNullOrWhiteSpace(config.ProductId))
                    builder.SetProductId(config.ProductId);
                if (!string.IsNullOrWhiteSpace(config.InstallPath))
                    builder.SetInstallPath(config.InstallPath);
                if (!string.IsNullOrWhiteSpace(config.UpdateLogUrl))
                    builder.SetUpdateLogUrl(config.UpdateLogUrl);
                if (!string.IsNullOrWhiteSpace(config.ReportUrl))
                    builder.SetReportUrl(config.ReportUrl);
                if (!string.IsNullOrWhiteSpace(config.Bowl))
                    builder.SetBowl(config.Bowl);
                if (!string.IsNullOrWhiteSpace(config.Script))
                    builder.SetScript(config.Script);
                if (!string.IsNullOrWhiteSpace(config.DriverDirectory))
                    builder.SetDriverDirectory(config.DriverDirectory);
                if (config.BlackFiles != null)
                    builder.SetBlackFiles(config.BlackFiles);
                if (config.BlackFormats != null)
                    builder.SetBlackFormats(config.BlackFormats);
                if (config.SkipDirectorys != null)
                    builder.SetSkipDirectorys(config.SkipDirectorys);

                return builder;
            }
            catch (System.Text.Json.JsonException)
            {
                // Invalid JSON format, fall back to parameters
                return null;
            }
            catch (IOException)
            {
                // File read error, fall back to parameters
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // Permission denied, fall back to parameters
                return null;
            }
            catch
            {
                // Any other unexpected error, fall back to parameters
                return null;
            }
        }

        /// <summary>
        /// Initializes a new instance of the ConfiginfoBuilder with required parameters.
        /// This constructor is private. Use <see cref="Create(string, string, string)"/> for creating instances.
        /// </summary>
        /// <param name="updateUrl">The API endpoint URL for checking available updates. Must be a valid absolute URI.</param>
        /// <param name="token">The authentication token used for API requests.</param>
        /// <param name="scheme">The URL scheme used for update requests (e.g., "http" or "https").</param>
        /// <exception cref="ArgumentException">Thrown when any required parameter is null, empty, or invalid.</exception>
        private ConfiginfoBuilder(string updateUrl, string token, string scheme)
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
            // Initialize common defaults
            _blackFiles = new List<string>();
            _blackFormats = new List<string>(DefaultBlackFormats);
            _skipDirectorys = new List<string>();
            
            // Set default InstallPath to current program running directory
            // This is set here to ensure the builder has a consistent default
            // even though BaseConfigInfo also has this default via property initializer
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
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
