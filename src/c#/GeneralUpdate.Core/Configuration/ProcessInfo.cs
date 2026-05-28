using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     进程间通信（IPC）参数对象。
    ///     此对象被序列化为 JSON 字符串，通过进程参数传递给升级进程，
    ///     使独立的升级应用程序能够使用正确的配置执行更新操作。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>ProcessInfo</c> 是更新流程中客户端进程与升级进程之间的数据传输契约。
    ///         其生命周期如下：
    ///         <list type="number">
    ///             <item>
    ///                 <description>
    ///                     在客户端更新管道的最后阶段，由
    ///                     <see cref="ConfigurationMapper.MapToProcessInfo" /> 从
    ///                     <see cref="GlobalConfigInfo" /> 映射创建。
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     序列化为 JSON 字符串，存储在 <see cref="GlobalConfigInfo.ProcessInfo" /> 属性中。
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     客户端启动升级进程时，将 JSON 字符串通过命令行参数传递。
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     升级进程反序列化 JSON 字符串，恢复 <c>ProcessInfo</c> 对象，并使用其中的配置执行更新。
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         设计说明：
    ///         <list type="bullet">
    ///             <item>
    ///                 <description>
    ///                     所有字段均使用 <see cref="JsonPropertyNameAttribute" /> 注解以确保序列化名称一致。
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     构造函数会执行参数校验，确保升级进程收到的配置是合法有效的。
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     某些字段的名称与其他配置类略有不同，这是为了保持与早期版本的 JSON 序列化向后兼容。
    ///                     例如：<c>AppName</c> 对应 <c>MainAppName</c>、<c>CurrentVersion</c> 对应 <c>ClientVersion</c>。
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <seealso cref="GlobalConfigInfo" />
    /// <seealso cref="ConfigurationMapper" />
    /// <seealso cref="VersionInfo" />
    public class ProcessInfo
    {
        /// <summary>
        ///     默认无参构造函数，用于 JSON 反序列化。
        /// </summary>
        /// <remarks>
        ///     在使用 <see cref="System.Text.Json.JsonSerializer.Deserialize{T}(string, System.Text.Json.JsonSerializerOptions)" />
        ///     反序列化 <c>ProcessInfo</c> 的 JSON 字符串时需要此构造函数。
        /// </remarks>
        public ProcessInfo() { }

        /// <summary>
        ///     带参数校验的构造函数，用于创建 <c>ProcessInfo</c> 实例。
        ///     所有参数均经过校验，确保升级进程接收到有效的配置。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         构造函数会对以下参数进行非空或有效性校验：
        ///         <list type="bullet">
        ///             <item>
        ///                 <description><paramref name="appName" />：不能为 null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="installPath" />：不能为 null，且路径必须存在</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="currentVersion" />：不能为 null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="lastVersion" />：不能为 null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="downloadTimeOut" />：必须大于等于 0</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="appSecretKey" />：不能为 null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="updateVersions" />：不能为 null 或空集合</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="reportUrl" />：不能为 null</description>
        ///             </item>
        ///             <item>
        ///                 <description><paramref name="backupDirectory" />：不能为 null</description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </remarks>
        /// <param name="appName">
        ///     更新完成后要启动的应用程序名称（从 <see cref="BaseConfigInfo.MainAppName" /> 映射而来）。
        /// </param>
        /// <param name="installPath">安装目录路径（必须存在）。</param>
        /// <param name="currentVersion">更新前的当前版本号。</param>
        /// <param name="lastVersion">更新后的目标版本号。</param>
        /// <param name="updateLogUrl">查看更新日志的 URL。</param>
        /// <param name="compressEncoding">压缩文件所使用的编码。</param>
        /// <param name="compressFormat">压缩格式（ZIP、7Z 等）的扩展名字符串。</param>
        /// <param name="downloadTimeOut">下载超时时间（秒），必须大于 0。</param>
        /// <param name="appSecretKey">用于身份验证的应用程序密钥。</param>
        /// <param name="updateVersions">要更新的版本信息列表，不能为空。</param>
        /// <param name="reportUrl">用于报告更新状态的 URL。</param>
        /// <param name="backupDirectory">备份文件的目录路径。</param>
        /// <param name="bowl">更新前需要终止的进程名称。</param>
        /// <param name="scheme">更新请求的 URL 方案。</param>
        /// <param name="token">身份验证令牌。</param>
        /// <param name="driverDirectory">驱动程序文件所在的目录路径。</param>
        /// <param name="tempPath">客户端下载更新包所在的临时目录路径。</param>
        /// <param name="blackFileFormats">需要跳过的文件格式扩展名列表。</param>
        /// <param name="blackFiles">需要跳过的特定文件列表。</param>
        /// <param name="skipDirectories">需要跳过的目录列表。</param>
        /// <param name="upgradePath">升级可执行文件所在的目录（可选，默认使用 <paramref name="installPath" />）。</param>
        /// <param name="launchClient">升级完成后是否启动客户端应用程序（可选，默认为 <c>true</c>）。</param>
        /// <exception cref="ArgumentNullException">
        ///     当 <paramref name="appName" />、<paramref name="installPath" />、
        ///     <paramref name="currentVersion" />、<paramref name="lastVersion" />、
        ///     <paramref name="appSecretKey" />、<paramref name="reportUrl" /> 或
        ///     <paramref name="backupDirectory" /> 为 <c>null</c> 时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     当 <paramref name="installPath" /> 指向的目录不存在、
        ///     <paramref name="downloadTimeOut" /> 小于 0 或
        ///     <paramref name="updateVersions" /> 为 null 或空集合时抛出。
        /// </exception>
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
            , string driverDirectory
            , string tempPath
            , List<string> blackFileFormats
            , List<string> blackFiles
            , List<string> skipDirectories
            , string upgradePath = null
            , bool launchClient = true)
        {
            // 校验必填字符串参数
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            if (!Directory.Exists(installPath)) throw new ArgumentException($"{nameof(installPath)} path does not exist ! {installPath}.");
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            UpdateLogUrl = updateLogUrl;

            // 校验并设置压缩参数
            CompressEncoding = compressEncoding.WebName;
            CompressFormat = compressFormat;
            if (downloadTimeOut < 0) throw new ArgumentException("Timeout must be greater than 0 !");
            DownloadTimeOut = downloadTimeOut;

            // 校验认证参数
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));

            // 校验更新版本集合
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = updateVersions;

            // 设置报告和备份参数
            ReportUrl = reportUrl ?? throw new ArgumentNullException(nameof(reportUrl));
            BackupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));

            // 设置可选参数
            Bowl = bowl;
            Scheme = scheme;
            Token = token;
            DriverDirectory = driverDirectory;
            TempPath = tempPath;

            // 设置黑名单参数
            BlackFileFormats = blackFileFormats;
            BlackFiles = blackFiles;
            SkipDirectorys = skipDirectories;

            // 设置升级路径（可选 — 未设置时默认使用 InstallPath）
            UpdatePath = upgradePath;

            // 设置启动标志（默认 true — 向后兼容）
            LaunchClientAfterUpdate = launchClient;
        }

        /// <summary>
        ///     更新完成后要启动的应用程序名称。
        ///     注意：在 <c>ProcessInfo</c> 中，此字段存储的是其他配置类中的
        ///     <c>MainAppName</c> 值。
        /// </summary>
        /// <remarks>
        ///     JSON 序列化名称为 <c>"UpdateAppName"</c>，这是为了保持与早期版本
        ///     的序列化格式向后兼容。
        /// </remarks>
        [JsonPropertyName("UpdateAppName")]
        public string AppName { get; set; }

        /// <summary>
        ///     文件将被更新到的安装目录。
        ///     所有更新操作均相对于此路径执行。
        /// </summary>
        /// <remarks>
        ///     构造函数会校验此路径必须存在，否则抛出 <see cref="ArgumentException" />。
        /// </remarks>
        [JsonPropertyName("InstallPath")]
        public string InstallPath { get; set; }

        /// <summary>
        ///     更新前应用程序的当前版本号。
        ///     注意：此值映射自 <see cref="GlobalConfigInfo.ClientVersion" />。
        /// </summary>
        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        /// <summary>
        ///     更新完成后的目标版本号。
        ///     即更新服务器上可用的最新版本号。
        /// </summary>
        [JsonPropertyName("LastVersion")]
        public string LastVersion { get; set; }

        /// <summary>
        ///     用户可查看详细更新日志和变更记录的 URL。
        /// </summary>
        [JsonPropertyName("UpdateLogUrl")]
        public string UpdateLogUrl { get; set; }

        /// <summary>
        ///     用于压缩/解压缩更新包的文本编码。
        ///     以 <see cref="Encoding.WebName" /> 字符串形式存储（例如 "utf-8"、"ascii"）。
        /// </summary>
        [JsonPropertyName("CompressEncoding")]
        public string CompressEncoding { get; set; }

        /// <summary>
        ///     更新包的压缩格式（例如 "ZIP"、"7Z"）。
        /// </summary>
        [JsonPropertyName("CompressFormat")]
        public string CompressFormat { get; set; }

        /// <summary>
        ///     下载更新包的超时时间（秒）。
        ///     如果下载操作超过此时间将被视为失败。
        /// </summary>
        [JsonPropertyName("DownloadTimeOut")]
        public int DownloadTimeOut { get; set; }

        /// <summary>
        ///     用于更新请求身份验证的应用程序密钥。
        /// </summary>
        [JsonPropertyName("AppSecretKey")]
        public string AppSecretKey { get; set; }

        /// <summary>
        ///     描述所有待更新版本的版本信息对象列表。
        ///     对于增量更新可包含多个版本条目。
        /// </summary>
        [JsonPropertyName("UpdateVersions")]
        public List<VersionInfo> UpdateVersions { get; set; }

        /// <summary>
        ///     用于报告更新进度和完成状态的 API 端点 URL。
        /// </summary>
        [JsonPropertyName("ReportUrl")]
        public string ReportUrl { get; set; }

        /// <summary>
        ///     更新前当前版本文件备份到的目录路径。
        ///     在更新失败时用于回滚操作。
        /// </summary>
        [JsonPropertyName("BackupDirectory")]
        public string BackupDirectory { get; set; }

        /// <summary>
        ///     在开始更新前应被终止的进程名称。
        ///     通常用于关闭可能产生文件锁定的冲突后台进程。
        /// </summary>
        [JsonPropertyName("Bowl")]
        public string Bowl { get; set; }

        /// <summary>
        ///     与更新服务器通信的 URL 方案（例如 "http"、"https"）。
        /// </summary>
        [JsonPropertyName("Scheme")]
        public string Scheme { get; set; }

        /// <summary>
        ///     包含在 HTTP 请求头中的身份验证令牌，用于 API 请求。
        /// </summary>
        [JsonPropertyName("Token")]
        public string Token { get; set; }

        /// <summary>
        ///     应从更新中排除的文件格式扩展名列表。
        ///     注意：此处的属性名称（<c>BlackFileFormats</c>）与其他配置类
        ///     （<c>BlackFormats</c>）不同，以保持 JSON 序列化的向后兼容性。
        /// </summary>
        [JsonPropertyName("BlackFileFormats")]
        public List<string> BlackFileFormats { get; set; }

        /// <summary>
        ///     应从更新中排除的特定文件名称列表。
        /// </summary>
        [JsonPropertyName("BlackFiles")]
        public List<string> BlackFiles { get; set; }

        /// <summary>
        ///     更新操作期间应跳过的目录路径列表。
        /// </summary>
        [JsonPropertyName("SkipDirectorys")]
        public List<string> SkipDirectorys { get; set; }

        /// <summary>
        ///     包含驱动程序文件的目录路径，用于驱动更新功能。
        ///     当启用驱动更新时，系统会从此目录定位并安装驱动程序文件。
        /// </summary>
        [JsonPropertyName("DriverDirectory")]
        public string DriverDirectory { get; set; }

        /// <summary>
        ///     客户端下载更新包时使用的临时目录路径。
        ///     升级进程通过更新管道从此路径读取更新包文件。
        /// </summary>
        [JsonPropertyName("TempPath")]
        public string TempPath { get; set; }

        /// <summary>
        ///     升级可执行文件所在的目录路径（可选）。
        ///     当设置此值时，升级进程将从 <c>UpdatePath</c> 启动，
        ///     而非 <see cref="InstallPath" />。
        /// </summary>
        [JsonPropertyName("UpdatePath")]
        public string UpdatePath { get; set; }

        /// <summary>
        ///     升级过程完成后是否启动客户端应用程序。
        ///     默认为 <c>true</c>。设置为 <c>false</c> 可在更新后保持应用程序停止状态。
        /// </summary>
        [JsonPropertyName("LaunchClientAfterUpdate")]
        public bool LaunchClientAfterUpdate { get; set; } = true;
    }
}
