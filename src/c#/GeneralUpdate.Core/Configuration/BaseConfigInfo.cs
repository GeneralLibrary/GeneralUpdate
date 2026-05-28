using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     基础配置抽象类，包含所有配置对象共有的公共字段。
    ///     作为面向用户的配置 (<see cref="Configinfo" />)、内部运行时状态 (<see cref="GlobalConfigInfo" />)
    ///     以及进程间通信参数 (<see cref="ProcessInfo" />) 的基类，统一管理公共属性以减少重复代码。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         该类定义了更新流程中通用的配置项，包括应用名称、安装路径、版本号、认证信息、
    ///         排除列表（黑名单文件、格式、目录）以及网络通信相关参数（URL Scheme、Token 等）。
    ///     </para>
    ///     <para>
    ///         其中 <c>UpdateAppName</c> 和 <c>InstallPath</c> 提供了合理的默认值，
    ///         分别为 <c>"Update.exe"</c> 和当前程序运行目录，在未显式配置时可正常工作。
    ///     </para>
    ///     <para>
    ///         此类为抽象类，不能直接实例化，必须通过派生类（<see cref="Configinfo" />、
    ///         <see cref="GlobalConfigInfo" />）使用。
    ///     </para>
    /// </remarks>
    /// <seealso cref="Configinfo" />
    /// <seealso cref="GlobalConfigInfo" />
    /// <seealso cref="ProcessInfo" />
    public abstract class BaseConfigInfo
    {
        /// <summary>
        ///     升级应用程序的可执行文件名称（例如 "Update.exe"）。
        ///     当客户端需要启动升级进程时，使用此名称定位并启动升级程序。
        /// </summary>
        /// <remarks>
        ///     默认值为 <c>"Update.exe"</c>。如果升级程序使用了不同的文件名，
        ///     需要通过 <see cref="ConfiginfoBuilder.SetUpgradeAppName" /> 进行配置。
        /// </remarks>
        public string UpdateAppName { get; set; } = "Update.exe";

        /// <summary>
        ///     主应用程序的可执行文件名称。
        ///     用于标识将要被更新的主应用程序进程。
        /// </summary>
        /// <remarks>
        ///     该属性在 <see cref="Configinfo.Validate" /> 中会被校验不能为空。
        ///     在 <see cref="ConfigurationMapper.MapToProcessInfo" /> 中，
        ///     此值会被映射到 <see cref="ProcessInfo.AppName" /> 属性。
        /// </remarks>
        public string MainAppName { get; set; }

        /// <summary>
        ///     应用程序文件的安装路径。
        ///     这是所有更新文件操作所依据的根目录。
        /// </summary>
        /// <remarks>
        ///     默认值为 <c>AppDomain.CurrentDomain.BaseDirectory</c>，
        ///     即当前程序运行所在的基目录。通常情况下无需手动设置，
        ///     但在需要将更新文件安装到非默认路径时必须显式配置。
        /// </remarks>
        public string InstallPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        ///     升级可执行文件所在的目录路径（可选）。
        ///     可以是绝对路径，也可以是相对于 <see cref="InstallPath" /> 的相对路径。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         当设置了此属性时，升级进程将从 <c>UpdatePath</c> 目录启动，
        ///         而非 <see cref="InstallPath" /> 目录。
        ///     </para>
        ///     <para>
        ///         如果此属性为 null 或空字符串，则回退到 <see cref="InstallPath" />，
        ///         保持向后兼容性。
        ///     </para>
        ///     <para>
        ///         示例：设置为 <c>"Upgrade"</c> 时，升级程序将位于
        ///         <c>InstallPath/Upgrade/UpdateAppName</c>。
        ///     </para>
        /// </remarks>
        public string UpdatePath { get; set; }

        /// <summary>
        ///     更新日志网页的 URL 地址。
        ///     用户可通过此地址查看详细的版本变更记录。
        /// </summary>
        /// <remarks>
        ///     在 <see cref="Configinfo.Validate" /> 中，如果此属性已设置，
        ///     则会校验其是否为有效的绝对 URI 格式。
        /// </remarks>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        ///     用于身份验证的应用程序密钥。
        ///     在向更新服务器请求更新信息时，需要此密钥进行身份认证。
        /// </summary>
        /// <remarks>
        ///     该属性为必填项，在 <see cref="Configinfo.Validate" /> 中会校验不能为空。
        /// </remarks>
        public string AppSecretKey { get; set; }

        /// <summary>
        ///     客户端应用程序的当前版本号。
        ///     格式应遵循语义化版本规范（例如 "1.0.0"）。
        /// </summary>
        /// <remarks>
        ///     该属性为必填项，在 <see cref="Configinfo.Validate" /> 中会校验不能为空。
        ///     通过比较 <c>ClientVersion</c> 与服务端返回的最新版本号，
        ///     可确定主应用是否需要更新（<see cref="GlobalConfigInfo.IsMainUpdate" />）。
        /// </remarks>
        public string ClientVersion { get; set; }

        /// <summary>
        ///     应从更新过程中排除的特定文件列表。
        ///     黑名单中的文件将在更新操作期间被跳过，不会被覆盖或删除。
        /// </summary>
        /// <remarks>
        ///     与 <see cref="BlackFormats" /> 和 <see cref="SkipDirectorys" /> 共同构成了
        ///     更新排除策略，用于保护不应被更新操作影响的关键文件。
        /// </remarks>
        public List<string> BlackFiles { get; set; }

        /// <summary>
        ///     应从更新过程中排除的文件格式扩展名列表。
        ///     例如：<c>[".log", ".tmp", ".cache"]</c> 将跳过所有具有这些扩展名的文件。
        /// </summary>
        /// <remarks>
        ///     此为批量排除机制，适用于需要跳过某一类文件的场景，
        ///     与 <see cref="BlackFiles" /> 针对单个文件的排除方式互补。
        /// </remarks>
        public List<string> BlackFormats { get; set; }

        /// <summary>
        ///     应在更新过程中跳过的目录路径列表。
        ///     列表中的整个目录树都将被忽略，不参与任何更新操作。
        /// </summary>
        public List<string> SkipDirectorys { get; set; }

        /// <summary>
        ///     用于报告更新状态和结果的 API 端点 URL。
        ///     更新的进度和完成状态将发送到此 URL。
        /// </summary>
        /// <remarks>
        ///     此 URL 在 <see cref="ConfigurationMapper.MapToProcessInfo" /> 中会映射到
        ///     <see cref="ProcessInfo.ReportUrl" />，由升级进程在更新完成后进行回调。
        /// </remarks>
        public string ReportUrl { get; set; }

        /// <summary>
        ///     在开始更新前应被终止的进程名称。
        ///     通常用于关闭可能冲突的后台进程（例如 "Bowl" 进程）。
        /// </summary>
        /// <remarks>
        ///     在更新流程中，启动升级进程之前会尝试终止此名称对应的进程，
        ///     以避免文件占用导致更新失败。
        /// </remarks>
        public string Bowl { get; set; }

        /// <summary>
        ///     用于更新请求的 URL 方案（例如 "http" 或 "https"）。
        ///     此方案决定了与更新服务器通信时使用的协议。
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        ///     用于 API 请求的身份验证令牌。
        ///     在与更新服务器通信时，此令牌会包含在 HTTP 请求头中。
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        ///     包含驱动程序文件的目录路径，用于驱动更新功能。
        ///     当启用驱动更新时，系统会从此目录定位并安装驱动程序文件。
        /// </summary>
        public string DriverDirectory { get; set; }

        /// <summary>
        ///     当前更新角色 — 决定 <see cref="Strategy.AbstractStrategy.StartAppAsync" />
        ///     启动哪个应用程序。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         当 <see cref="AppType.Client" /> 时，启动 <c>UpdateAppName</c>（升级进程）。
        ///     </para>
        ///     <para>
        ///         当 <see cref="AppType.Upgrade" /> 时，启动 <c>MainAppName</c> 主应用和 Bowl 进程。
        ///     </para>
        /// </remarks>
        public AppType? AppType { get; set; }
    }
}
