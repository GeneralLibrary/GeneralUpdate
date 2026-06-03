using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     A generic <see cref="UpdateRequest" /> builder class that simplifies the creation of update configuration.
    ///     With just three core parameters (<c>UpdateUrl</c>, <c>Token</c>, <c>Scheme</c>), it automatically
    ///     generates platform-appropriate defaults. All other configuration items are optional.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This builder uses a fluent interface design — all Set methods return the current instance,
    ///         enabling method chaining. For example:
    ///         <code>
    ///             var config = new UpdateRequestBuilder()
    ///                 .SetUpdateUrl("https://update.example.com")
    ///                 .SetToken("mytoken")
    ///                 .SetScheme("https")
    ///                 .Build();
    ///         </code>
    ///     </para>
    ///     <para>
    ///         The design is inspired by the zero-configuration pattern used in projects like Velopack.
    ///         The <c>Create()</c> static factory method loads settings from an <c>update_config.json</c> file,
    ///         which has the highest priority — all values specified in the configuration file override
    ///         programmatic settings.
    ///     </para>
    ///     <para>
    ///         After building, calling the <see cref="Build" /> method triggers validation on the generated
    ///         <see cref="UpdateRequest" /> object via <see cref="UpdateRequest.Validate" />, ensuring the resulting
    ///         configuration is valid.
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateRequest" />
    /// <seealso cref="UpdateRequest.Validate" />
    public class UpdateRequestBuilder
    {
        // ──────────────────────────────
        //  可配置的默认值
        //  注意：UpdateAppName 和 InstallPath 的默认值在 UpdateRequest 基类中定义
        //  以下是 UpdateRequestBuilder 特有的默认值，用于支持建造者模式
        // ──────────────────────────────
        private string _updateUrl;
        private string _token;
        private string _scheme;
        private Security.AuthScheme _authScheme = Security.AuthScheme.Hmac;
        private string _basicUsername;
        private string _basicPassword;
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
        private string _driverDirectory;
        private List<string> _blackFiles;
        private List<string> _blackFormats;
        private List<string> _skipDirectorys;

        /// <summary>
        ///     Creates a <see cref="UpdateRequestBuilder" /> instance by loading settings from the
        ///     <c>update_config.json</c> configuration file.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The configuration file must exist in the application's running directory and contain all
        ///         required settings. This approach has the highest priority — all settings must be specified
        ///         in the JSON file.
        ///     </para>
        ///     <para>
        ///         Example JSON configuration file:
        ///         <code>
        ///             {
        ///                 "UpdateUrl": "https://update.example.com",
        ///                 "Token": "mytoken",
        ///                 "Scheme": "https",
        ///                 "MainAppName": "MyApp",
        ///                 "ClientVersion": "1.0.0.0"
        ///             }
        ///         </code>
        ///     </para>
        ///     <para>
        ///         If the configuration file does not exist or has an invalid format, a <see cref="FileNotFoundException" />
        ///         is thrown. This method does not fall back to manual settings. For manual configuration, use the
        ///         constructor and chain Set methods directly.
        ///     </para>
        /// </remarks>
        /// <returns>
        ///     A new <see cref="UpdateRequestBuilder" /> instance with settings loaded from the configuration file.
        /// </returns>
        /// <exception cref="FileNotFoundException">
        ///     Thrown when the <c>update_config.json</c> file cannot be found in the running directory.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the configuration file format is invalid or cannot be loaded.
        /// </exception>
        public static UpdateRequestBuilder Create()
        {
            // 尝试从配置文件加载
            var configFromFile = LoadFromConfigFile();
            if (configFromFile != null)
            {
                // 配置文件加载成功
                return configFromFile;
            }

            // 如果不存在配置文件，则抛出异常
            throw new FileNotFoundException("Configuration file 'update_config.json' not found in the running directory. Please create this file with the required settings.");
        }

        /// <summary>
        ///     Loads configuration from the <c>update_config.json</c> file in the running directory.
        /// </summary>
        /// <remarks>
        ///     This method attempts to read and parse the JSON file. If the file does not exist, the JSON format
        ///     is invalid, file read permissions are insufficient, or any other unexpected error occurs, it returns
        ///     <c>null</c> instead of throwing an exception, allowing the caller to decide on a fallback strategy.
        /// </remarks>
        /// <returns>
        ///     A <see cref="UpdateRequestBuilder" /> instance populated with the file settings on success;
        ///     <c>null</c> if the file does not exist, the format is invalid, or reading fails.
        /// </returns>
        private static UpdateRequestBuilder LoadFromConfigFile()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
                if (!File.Exists(configPath))
                {
                    return null;
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<UpdateRequest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null)
                {
                    return null;
                }

                // 使用加载的配置创建建造者
                var builder = new UpdateRequestBuilder();

                // 应用所有已加载的设置
                if (!string.IsNullOrWhiteSpace(config.UpdateUrl))
                    builder.SetUpdateUrl(config.UpdateUrl);
                if (!string.IsNullOrWhiteSpace(config.Token))
                    builder.SetToken(config.Token);
                if (!string.IsNullOrWhiteSpace(config.Scheme))
                    builder.SetScheme(config.Scheme);
                builder.SetAuthScheme(config.AuthScheme);
                if (!string.IsNullOrWhiteSpace(config.BasicUsername))
                    builder.SetBasicUsername(config.BasicUsername);
                if (!string.IsNullOrWhiteSpace(config.BasicPassword))
                    builder.SetBasicPassword(config.BasicPassword);
                if (!string.IsNullOrWhiteSpace(config.UpdateAppName))
                    builder.SetUpgradeAppName(config.UpdateAppName);
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
                if (!string.IsNullOrWhiteSpace(config.DriverDirectory))
                    builder.SetDriverDirectory(config.DriverDirectory);
                if (config.Files != null)
                    builder.SetFiles(config.Files);
                if (config.Formats != null)
                    builder.SetFormats(config.Formats);
                if (config.Directories != null)
                    builder.SetDirectories(config.Directories);

                builder.SetInstallPath(string.IsNullOrWhiteSpace(config.InstallPath) ? AppDomain.CurrentDomain.BaseDirectory : config.InstallPath);
                return builder;
            }
            catch (System.Text.Json.JsonException)
            {
                // JSON format is invalid, fall back to parameter-based approach
                return null;
            }
            catch (IOException)
            {
                // File read error, fall back to parameter-based approach
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // Insufficient permissions, fall back to parameter-based approach
                return null;
            }
            catch
            {
                // Any other unexpected error, fall back to parameter-based approach
                return null;
            }
        }

        /// <summary>
        ///     Sets the API endpoint URL for update checking.
        /// </summary>
        /// <param name="updateUrl">The API endpoint URL used to check for available updates.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="updateUrl" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetUpdateUrl(string updateUrl)
        {
            if (string.IsNullOrWhiteSpace(updateUrl))
                throw new ArgumentException("updateUrl cannot be null or empty.", nameof(updateUrl));

            _updateUrl = updateUrl;
            return this;
        }

        /// <summary>
        ///     Sets the authentication token for API requests.
        /// </summary>
        /// <param name="token">The authentication token for HTTP request headers.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="token" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("token cannot be null or empty.", nameof(token));

            _token = token;
            return this;
        }

        /// <summary>
        ///     Sets the URL scheme (e.g., "http" or "https").
        /// </summary>
        /// <param name="scheme">The URL scheme used for server communication.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="scheme" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetScheme(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("scheme cannot be null or empty.", nameof(scheme));

            _scheme = scheme;
            return this;
        }

        /// <summary>
        ///     Explicitly selects the HTTP authentication method.
        /// </summary>
        /// <param name="scheme">The authentication scheme to use. Defaults to <see cref="Security.AuthScheme.Hmac"/>.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetAuthScheme(Security.AuthScheme scheme)
        {
            _authScheme = scheme;
            return this;
        }

        /// <summary>
        ///     Sets the username for HTTP Basic Authentication.
        /// </summary>
        /// <param name="username">The username for Basic Authentication.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetBasicUsername(string username)
        {
            _basicUsername = username;
            return this;
        }

        /// <summary>
        ///     Sets the password for HTTP Basic Authentication.
        /// </summary>
        /// <param name="password">The password for Basic Authentication.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetBasicPassword(string password)
        {
            _basicPassword = password;
            return this;
        }

        /// <summary>
        ///     Sets the name of the upgrade application (the executable launched after the update completes).
        /// </summary>
        /// <param name="appName">The executable file name of the upgrade application.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="appName" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetUpgradeAppName(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("UpdateAppName cannot be null or empty.", nameof(appName));

            _appName = appName;
            return this;
        }

        /// <summary>
        ///     Sets the name of the main application.
        /// </summary>
        /// <param name="mainAppName">The name of the main application (without file extension).</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mainAppName" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetMainAppName(string mainAppName)
        {
            if (string.IsNullOrWhiteSpace(mainAppName))
                throw new ArgumentException("MainAppName cannot be null or empty.", nameof(mainAppName));

            _mainAppName = mainAppName;
            return this;
        }

        /// <summary>
        ///     Sets the current version number of the client application.
        /// </summary>
        /// <param name="clientVersion">The current version number of the client application (should follow semantic versioning format).</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientVersion" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetClientVersion(string clientVersion)
        {
            if (string.IsNullOrWhiteSpace(clientVersion))
                throw new ArgumentException("ClientVersion cannot be null or empty.", nameof(clientVersion));

            _clientVersion = clientVersion;
            return this;
        }

        /// <summary>
        ///     Sets the current version number of the upgrade client program.
        ///     Used for independent version management of the updater itself.
        /// </summary>
        /// <param name="upgradeClientVersion">The current version number of the upgrade application.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="upgradeClientVersion" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetUpgradeClientVersion(string upgradeClientVersion)
        {
            if (string.IsNullOrWhiteSpace(upgradeClientVersion))
                throw new ArgumentException("UpgradeClientVersion cannot be null or empty.", nameof(upgradeClientVersion));

            _upgradeClientVersion = upgradeClientVersion;
            return this;
        }

        /// <summary>
        ///     Sets the application secret key used for authentication.
        /// </summary>
        /// <param name="appSecretKey">The application secret key used for authenticating update requests.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="appSecretKey" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetAppSecretKey(string appSecretKey)
        {
            if (string.IsNullOrWhiteSpace(appSecretKey))
                throw new ArgumentException("AppSecretKey cannot be null or empty.", nameof(appSecretKey));

            _appSecretKey = appSecretKey;
            return this;
        }

        /// <summary>
        ///     Sets the unique product identifier.
        ///     Used to distinguish between multiple products sharing the same update server.
        /// </summary>
        /// <param name="productId">The unique application product identifier.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="productId" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetProductId(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId cannot be null or empty.", nameof(productId));

            _productId = productId;
            return this;
        }

        /// <summary>
        ///     Sets the installation path for the application files.
        /// </summary>
        /// <param name="installPath">The installation directory path for the application files.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="installPath" /> is null, empty, or consists only of whitespace.</exception>
        public UpdateRequestBuilder SetInstallPath(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                throw new ArgumentException("InstallPath cannot be null or empty.", nameof(installPath));

            _installPath = installPath;
            return this;
        }

        /// <summary>
        ///     Sets the URL of the update log web page.
        /// </summary>
        /// <param name="updateLogUrl">The web page URL for viewing the update log.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="updateLogUrl" /> is not empty but is not a valid absolute URI.</exception>
        public UpdateRequestBuilder SetUpdateLogUrl(string updateLogUrl)
        {
            if (!string.IsNullOrWhiteSpace(updateLogUrl) && !Uri.IsWellFormedUriString(updateLogUrl, UriKind.Absolute))
                throw new ArgumentException("UpdateLogUrl must be a valid absolute URI.", nameof(updateLogUrl));

            _updateLogUrl = updateLogUrl;
            return this;
        }

        /// <summary>
        ///     Sets the API endpoint URL for reporting update status and results.
        /// </summary>
        /// <param name="reportUrl">The API endpoint URL for reporting update status and results.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="reportUrl" /> is not empty but is not a valid absolute URI.</exception>
        public UpdateRequestBuilder SetReportUrl(string reportUrl)
        {
            if (!string.IsNullOrWhiteSpace(reportUrl) && !Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                throw new ArgumentException("ReportUrl must be a valid absolute URI.", nameof(reportUrl));

            _reportUrl = reportUrl;
            return this;
        }

        /// <summary>
        ///     Sets the name of the conflicting process to terminate before the update.
        /// </summary>
        /// <param name="bowl">The process name to terminate before starting the update.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetBowl(string bowl)
        {
            _bowl = bowl;
            return this;
        }

        /// <summary>
        ///     Sets the directory path containing driver files.
        ///     Used for driver-based update functionality (Drive mode).
        /// </summary>
        /// <param name="driverDirectory">The directory path containing the driver files.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetDriverDirectory(string driverDirectory)
        {
            _driverDirectory = driverDirectory;
            return this;
        }

        /// <summary>
        ///     Sets the list of blacklisted files to exclude from the update process.
        /// </summary>
        /// <param name="blackFiles">The list of specific files to be excluded.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetFiles(List<string> blackFiles)
        {
            _blackFiles = blackFiles ?? new List<string>();
            return this;
        }

        /// <summary>
        ///     Sets the list of blacklisted file format extensions to exclude from the update process.
        /// </summary>
        /// <param name="blackFormats">The list of file extensions to be excluded (e.g., ".log", ".tmp").</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetFormats(List<string> blackFormats)
        {
            _blackFormats = blackFormats ?? new List<string>();
            return this;
        }

        /// <summary>
        ///     Sets the list of directory paths to skip during the update process.
        /// </summary>
        /// <param name="skipDirectorys">The list of directory paths to skip during the update.</param>
        /// <returns>The current <see cref="UpdateRequestBuilder" /> instance for chaining.</returns>
        public UpdateRequestBuilder SetDirectories(List<string> skipDirectorys)
        {
            _skipDirectorys = skipDirectorys ?? new List<string>();
            return this;
        }

        /// <summary>
        ///     Builds and returns a complete <see cref="UpdateRequest" /> object containing all configured and default values.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method is the final step of the builder pattern. It assembles all configured values
        ///         (both explicitly set and defaulted) into a new <see cref="UpdateRequest" /> instance.
        ///     </para>
        ///     <para>
        ///         After building, it automatically calls <see cref="UpdateRequest.Validate" /> to validate the
        ///         configuration. If validation fails, the validation exception is wrapped in an
        ///         <see cref="InvalidOperationException" /> and rethrown.
        ///     </para>
        /// </remarks>
        /// <returns>A fully configured <see cref="UpdateRequest" /> instance.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the generated configuration object fails <see cref="UpdateRequest.Validate" /> validation.
        ///     The inner exception contains the specific validation failure details.
        /// </exception>
        public UpdateRequest Build()
        {
            // 创建 UpdateRequest 对象，填入所有值
            var configinfo = new UpdateRequest
            {
                UpdateUrl = _updateUrl,
                Token = _token,
                Scheme = _scheme,
                AuthScheme = _authScheme,
                BasicUsername = _basicUsername,
                BasicPassword = _basicPassword,
                UpdateAppName = _appName,
                MainAppName = _mainAppName,
                ClientVersion = _clientVersion,
                UpgradeClientVersion = _upgradeClientVersion,
                AppSecretKey = _appSecretKey,
                ProductId = _productId,
                InstallPath = _installPath,
                UpdateLogUrl = _updateLogUrl,
                ReportUrl = _reportUrl,
                Bowl = _bowl,
                DriverDirectory = _driverDirectory,
                Files = _blackFiles ?? new List<string>(),
                Formats = _blackFormats ?? new List<string>(),
                Directories = _skipDirectorys ?? new List<string>()
            };

            // 校验构建的配置
            try
            {
                configinfo.Validate();
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Failed to build valid UpdateRequest: {ex.Message}", ex);
            }

            return configinfo;
        }
    }
}
