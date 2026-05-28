using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     通用的 <see cref="Configinfo" /> 建造者类，用于简化更新配置的创建过程。
    ///     只需三个核心参数（<c>UpdateUrl</c>、<c>Token</c>、<c>Scheme</c>），
    ///     即可自动生成平台适用的默认值，其他配置项均可选填。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         该建造者采用流式（Fluent）接口设计，所有 Set 方法均返回当前实例，
    ///         支持链式调用。例如：
    ///         <code>
    ///             var config = new ConfiginfoBuilder()
    ///                 .SetUpdateUrl("https://update.example.com")
    ///                 .SetToken("mytoken")
    ///                 .SetScheme("https")
    ///                 .Build();
    ///         </code>
    ///     </para>
    ///     <para>
    ///         设计灵感来源于 Velopack 等项目的零配置设计模式。
    ///         通过 <c>Create()</c> 静态工厂方法可从 <c>update_config.json</c> 配置文件加载设置，
    ///         该方式优先级最高，配置文件中指定的所有值将覆盖代码中的设置。
    ///     </para>
    ///     <para>
    ///         构建完成后调用 <see cref="Build" /> 方法会触发生成的 <see cref="Configinfo" /> 对象的
    ///         <see cref="Configinfo.Validate" /> 校验，确保生成的配置合法有效。
    ///     </para>
    /// </remarks>
    /// <seealso cref="Configinfo" />
    /// <seealso cref="Configinfo.Validate" />
    public class ConfiginfoBuilder
    {
        // ──────────────────────────────
        //  可配置的默认值
        //  注意：UpdateAppName 和 InstallPath 的默认值在 Configinfo 基类中定义
        //  以下是 ConfiginfoBuilder 特有的默认值，用于支持建造者模式
        // ──────────────────────────────
        private string _updateUrl;
        private string _token;
        private string _scheme;
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
        ///     通过从 <c>update_config.json</c> 配置文件中加载设置来创建
        ///     <see cref="ConfiginfoBuilder" /> 实例。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         配置文件必须存在于应用程序的运行目录中，并包含所有必需的设置。
        ///         配置文件具有最高优先级——所有设置都必须在 JSON 文件中指定。
        ///     </para>
        ///     <para>
        ///         JSON 配置文件示例：
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
        ///         如果配置文件不存在或格式无效，将抛出 <see cref="FileNotFoundException" />。
        ///         该方法不支持回退到手动设置——如果需要手动设置，请直接使用构造函数并通过 Set 方法链式构建。
        ///     </para>
        /// </remarks>
        /// <returns>
        ///     一个从配置文件加载了设置的新 <see cref="ConfiginfoBuilder" /> 实例。
        /// </returns>
        /// <exception cref="FileNotFoundException">
        ///     当运行目录中找不到 <c>update_config.json</c> 文件时抛出。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     当配置文件格式无效或无法加载时抛出。
        /// </exception>
        public static ConfiginfoBuilder Create()
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
        ///     从运行目录中的 <c>update_config.json</c> 文件加载配置。
        /// </summary>
        /// <remarks>
        ///     此方法会尝试读取并解析 JSON 文件。如果文件不存在、JSON 格式无效、
        ///     文件读取权限不足或发生其他意外错误，均会返回 <c>null</c> 而非抛出异常，
        ///     以便调用方决定回退策略。
        /// </remarks>
        /// <returns>
        ///     成功时返回包含文件设置的 <see cref="ConfiginfoBuilder" /> 实例；
        ///     如果文件不存在、格式无效或读取失败，则返回 <c>null</c>。
        /// </returns>
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

                // 使用加载的配置创建建造者
                var builder = new ConfiginfoBuilder();

                // 应用所有已加载的设置
                if (!string.IsNullOrWhiteSpace(config.UpdateUrl))
                    builder.SetUpdateUrl(config.UpdateUrl);
                if (!string.IsNullOrWhiteSpace(config.Token))
                    builder.SetToken(config.Token);
                if (!string.IsNullOrWhiteSpace(config.Scheme))
                    builder.SetScheme(config.Scheme);
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
                if (config.BlackFiles != null)
                    builder.SetBlackFiles(config.BlackFiles);
                if (config.BlackFormats != null)
                    builder.SetBlackFormats(config.BlackFormats);
                if (config.SkipDirectorys != null)
                    builder.SetSkipDirectorys(config.SkipDirectorys);

                builder.SetInstallPath(string.IsNullOrWhiteSpace(config.InstallPath) ? AppDomain.CurrentDomain.BaseDirectory : config.InstallPath);
                return builder;
            }
            catch (System.Text.Json.JsonException)
            {
                // JSON 格式无效，回退到参数方式
                return null;
            }
            catch (IOException)
            {
                // 文件读取错误，回退到参数方式
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // 权限不足，回退到参数方式
                return null;
            }
            catch
            {
                // 任何其他意外错误，回退到参数方式
                return null;
            }
        }

        /// <summary>
        ///     设置更新检查的 API 端点 URL。
        /// </summary>
        /// <param name="updateUrl">用于检查可用更新的 API 端点 URL。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="updateUrl" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetUpdateUrl(string updateUrl)
        {
            if (string.IsNullOrWhiteSpace(updateUrl))
                throw new ArgumentException("updateUrl cannot be null or empty.", nameof(updateUrl));

            _updateUrl = updateUrl;
            return this;
        }

        /// <summary>
        ///     设置 API 请求的身份验证令牌。
        /// </summary>
        /// <param name="token">用于 HTTP 请求头的身份验证令牌。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="token" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("token cannot be null or empty.", nameof(token));

            _token = token;
            return this;
        }

        /// <summary>
        ///     设置 URL 方案（例如 "http" 或 "https"）。
        /// </summary>
        /// <param name="scheme">用于服务器通信的 URL 方案。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="scheme" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetScheme(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("scheme cannot be null or empty.", nameof(scheme));

            _scheme = scheme;
            return this;
        }

        /// <summary>
        ///     设置升级应用程序的名称（更新完成后启动的可执行文件）。
        /// </summary>
        /// <param name="appName">升级应用程序的可执行文件名称。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="appName" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetUpgradeAppName(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("UpdateAppName cannot be null or empty.", nameof(appName));

            _appName = appName;
            return this;
        }

        /// <summary>
        ///     设置主应用程序的名称。
        /// </summary>
        /// <param name="mainAppName">主应用程序的名称（不含文件扩展名）。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="mainAppName" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetMainAppName(string mainAppName)
        {
            if (string.IsNullOrWhiteSpace(mainAppName))
                throw new ArgumentException("MainAppName cannot be null or empty.", nameof(mainAppName));

            _mainAppName = mainAppName;
            return this;
        }

        /// <summary>
        ///     设置客户端应用程序的当前版本号。
        /// </summary>
        /// <param name="clientVersion">客户端应用程序的当前版本号（应为语义化版本格式）。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="clientVersion" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetClientVersion(string clientVersion)
        {
            if (string.IsNullOrWhiteSpace(clientVersion))
                throw new ArgumentException("ClientVersion cannot be null or empty.", nameof(clientVersion));

            _clientVersion = clientVersion;
            return this;
        }

        /// <summary>
        ///     设置升级客户端程序的当前版本号。
        ///     用于实现更新器自身的独立版本管理。
        /// </summary>
        /// <param name="upgradeClientVersion">升级应用程序的当前版本号。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="upgradeClientVersion" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetUpgradeClientVersion(string upgradeClientVersion)
        {
            if (string.IsNullOrWhiteSpace(upgradeClientVersion))
                throw new ArgumentException("UpgradeClientVersion cannot be null or empty.", nameof(upgradeClientVersion));

            _upgradeClientVersion = upgradeClientVersion;
            return this;
        }

        /// <summary>
        ///     设置用于身份验证的应用程序密钥。
        /// </summary>
        /// <param name="appSecretKey">用于更新请求身份验证的应用程序密钥。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="appSecretKey" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetAppSecretKey(string appSecretKey)
        {
            if (string.IsNullOrWhiteSpace(appSecretKey))
                throw new ArgumentException("AppSecretKey cannot be null or empty.", nameof(appSecretKey));

            _appSecretKey = appSecretKey;
            return this;
        }

        /// <summary>
        ///     设置唯一产品标识符。
        ///     用于在共享同一更新服务器的多个产品之间进行区分。
        /// </summary>
        /// <param name="productId">唯一的应用程序产品标识符。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="productId" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetProductId(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId cannot be null or empty.", nameof(productId));

            _productId = productId;
            return this;
        }

        /// <summary>
        ///     设置应用程序文件的安装路径。
        /// </summary>
        /// <param name="installPath">应用程序文件所在的安装目录路径。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="installPath" /> 为 null、空字符串或仅含空白字符时抛出。</exception>
        public ConfiginfoBuilder SetInstallPath(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                throw new ArgumentException("InstallPath cannot be null or empty.", nameof(installPath));

            _installPath = installPath;
            return this;
        }

        /// <summary>
        ///     设置更新日志网页的 URL 地址。
        /// </summary>
        /// <param name="updateLogUrl">用于查看更新日志的网页 URL。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="updateLogUrl" /> 不为空但格式不是有效的绝对 URI 时抛出。</exception>
        public ConfiginfoBuilder SetUpdateLogUrl(string updateLogUrl)
        {
            if (!string.IsNullOrWhiteSpace(updateLogUrl) && !Uri.IsWellFormedUriString(updateLogUrl, UriKind.Absolute))
                throw new ArgumentException("UpdateLogUrl must be a valid absolute URI.", nameof(updateLogUrl));

            _updateLogUrl = updateLogUrl;
            return this;
        }

        /// <summary>
        ///     设置用于报告更新状态和结果的 API 端点 URL。
        /// </summary>
        /// <param name="reportUrl">用于报告更新状态和结果的 API 端点 URL。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="reportUrl" /> 不为空但格式不是有效的绝对 URI 时抛出。</exception>
        public ConfiginfoBuilder SetReportUrl(string reportUrl)
        {
            if (!string.IsNullOrWhiteSpace(reportUrl) && !Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                throw new ArgumentException("ReportUrl must be a valid absolute URI.", nameof(reportUrl));

            _reportUrl = reportUrl;
            return this;
        }

        /// <summary>
        ///     设置需要在更新前终止的冲突进程名称。
        /// </summary>
        /// <param name="bowl">应在开始更新前终止的进程名称。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        public ConfiginfoBuilder SetBowl(string bowl)
        {
            _bowl = bowl;
            return this;
        }

        /// <summary>
        ///     设置驱动程序文件所在的目录路径。
        ///     用于驱动更新功能（Drive 模式）。
        /// </summary>
        /// <param name="driverDirectory">包含驱动程序文件的目录路径。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        public ConfiginfoBuilder SetDriverDirectory(string driverDirectory)
        {
            _driverDirectory = driverDirectory;
            return this;
        }

        /// <summary>
        ///     设置应从更新过程中排除的黑名单文件列表。
        /// </summary>
        /// <param name="blackFiles">应被排除的特定文件列表。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        public ConfiginfoBuilder SetBlackFiles(List<string> blackFiles)
        {
            _blackFiles = blackFiles ?? new List<string>();
            return this;
        }

        /// <summary>
        ///     设置应从更新过程中排除的黑名单文件格式扩展名列表。
        /// </summary>
        /// <param name="blackFormats">应被排除的文件扩展名列表（例如 ".log"、".tmp"）。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        public ConfiginfoBuilder SetBlackFormats(List<string> blackFormats)
        {
            _blackFormats = blackFormats ?? new List<string>();
            return this;
        }

        /// <summary>
        ///     设置应在更新过程中跳过的目录路径列表。
        /// </summary>
        /// <param name="skipDirectorys">应在更新期间跳过的目录路径列表。</param>
        /// <returns>当前 <see cref="ConfiginfoBuilder" /> 实例，支持链式调用。</returns>
        public ConfiginfoBuilder SetSkipDirectorys(List<string> skipDirectorys)
        {
            _skipDirectorys = skipDirectorys ?? new List<string>();
            return this;
        }

        /// <summary>
        ///     构建并返回一个完整的 <see cref="Configinfo" /> 对象，包含所有已配置和默认的值。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         此方法是建造者模式的最终步骤。它会将所有配置的值（包括显式设置的和使用默认值的）
        ///         组装到新的 <see cref="Configinfo" /> 实例中。
        ///     </para>
        ///     <para>
        ///         构建完成后会自动调用 <see cref="Configinfo.Validate" /> 方法进行完整性校验。
        ///         如果校验失败，校验异常将被包装为 <see cref="InvalidOperationException" /> 重新抛出。
        ///     </para>
        /// </remarks>
        /// <returns>一个完全配置的 <see cref="Configinfo" /> 实例。</returns>
        /// <exception cref="InvalidOperationException">
        ///     当生成的配置对象未通过 <see cref="Configinfo.Validate" /> 校验时抛出，
        ///     内部异常包含具体的校验失败原因。
        /// </exception>
        public Configinfo Build()
        {
            // 创建 Configinfo 对象，填入所有值
            var configinfo = new Configinfo
            {
                UpdateUrl = _updateUrl,
                Token = _token,
                Scheme = _scheme,
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
                BlackFiles = _blackFiles ?? new List<string>(),
                BlackFormats = _blackFormats ?? new List<string>(),
                SkipDirectorys = _skipDirectorys ?? new List<string>()
            };

            // 校验构建的配置
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
