using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     提供配置对象之间的集中映射工具方法。
    ///     确保 <see cref="Configinfo" />、<see cref="GlobalConfigInfo" /> 和 <see cref="ProcessInfo" />
    ///     之间的字段映射保持一致，降低在维护过程中遗漏或错误映射字段的风险。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>ConfigurationMapper</c> 是更新流程中配置转换的核心枢纽，负责以下两个方向的映射：
    ///     </para>
    ///     <para>
    ///         <list type="number">
    ///             <item>
    ///                 <description>
    ///                     <see cref="MapToGlobalConfigInfo" />：将用户提供的 <see cref="Configinfo" />
    ///                     映射为内部运行时配置 <see cref="GlobalConfigInfo" />。此映射在更新流程初始化阶段执行，
    ///                     将外部 API 的配置参数传递到内部工作流。
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     <see cref="MapToProcessInfo" />：将内部运行时配置 <see cref="GlobalConfigInfo" />
    ///                     映射为进程间通信参数 <see cref="ProcessInfo" />。此映射在客户端准备启动升级进程时执行，
    ///                     将计算后的所有运行时状态序列化后传递给升级进程。
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         通过将所有映射逻辑集中在此类中，避免了在引导代码（Bootstrap）各处分散的字段赋值逻辑，
    ///         从而简化了维护工作并降低了引入缺陷的可能性。
    ///     </para>
    /// </remarks>
    /// <seealso cref="Configinfo" />
    /// <seealso cref="GlobalConfigInfo" />
    /// <seealso cref="ProcessInfo" />
    /// <seealso cref="VersionInfo" />
    public static class ConfigurationMapper
    {
        /// <summary>
        ///     将用户提供的配置 (<see cref="Configinfo" />) 映射到内部运行时配置
        ///     (<see cref="GlobalConfigInfo" />)。
        ///     对所有共享的配置属性执行一对一的字段映射。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         此方法执行浅拷贝映射，将 <see cref="Configinfo" /> 中的所有公共属性和基类属性
        ///         逐一赋值到 <see cref="GlobalConfigInfo" /> 实例中。
        ///     </para>
        ///     <para>
        ///         如果 <paramref name="target" /> 为 <c>null</c>，将自动创建一个新的
        ///         <see cref="GlobalConfigInfo" /> 实例。如果 <paramref name="source" /> 为 <c>null</c>，
        ///         则直接返回空的（或新创建的）目标实例，不会抛出异常。
        ///     </para>
        /// </remarks>
        /// <param name="source">
        ///     包含初始设置的用户提供配置对象。可以为 <c>null</c>。
        /// </param>
        /// <param name="target">
        ///     待填充的内部配置对象。如果为 <c>null</c>，将自动创建新实例。
        /// </param>
        /// <returns>
        ///     一个填充了来自 <paramref name="source" /> 的配置值的 <see cref="GlobalConfigInfo" /> 实例。
        /// </returns>
        public static GlobalConfigInfo MapToGlobalConfigInfo(Configinfo source, GlobalConfigInfo target = null)
        {
            // 如果 source 和 target 均未提供，则创建新实例
            if (target == null)
                target = new GlobalConfigInfo();

            // 如果 source 为 null，则直接返回空的 target
            if (source == null)
                return target;

            // 映射基类公共字段
            target.UpdateAppName = source.UpdateAppName;
            target.MainAppName = source.MainAppName;
            target.ClientVersion = source.ClientVersion;
            target.InstallPath = source.InstallPath;
            target.UpdateLogUrl = source.UpdateLogUrl;
            target.AppSecretKey = source.AppSecretKey;
            target.BlackFiles = source.BlackFiles;
            target.BlackFormats = source.BlackFormats;
            target.SkipDirectorys = source.SkipDirectorys;
            target.ReportUrl = source.ReportUrl;
            target.Bowl = source.Bowl;
            target.Scheme = source.Scheme;
            target.Token = source.Token;
            target.DriverDirectory = source.DriverDirectory;
            target.AppType = source.AppType;
            target.UpdatePath = source.UpdatePath;

            // 映射 GlobalConfigInfo 特有字段
            target.UpdateUrl = source.UpdateUrl;
            target.UpgradeClientVersion = source.UpgradeClientVersion;
            target.ProductId = source.ProductId;

            return target;
        }

        /// <summary>
        ///     将内部运行时配置 (<see cref="GlobalConfigInfo" />) 映射到进程间通信参数
        ///     (<see cref="ProcessInfo" />)。
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         此方法将之前分散在引导代码各处的复杂参数传递逻辑集中到一处管理。
        ///         生成的 <see cref="ProcessInfo" /> 对象将被序列化为 JSON 字符串，
        ///         通过命令行参数或标准输入传递给升级进程。
        ///     </para>
        ///     <para>
        ///         映射过程中需要注意以下几点：
        ///         <list type="bullet">
        ///             <item>
        ///                 <description>
        ///                     <c>MainAppName</c> 映射到 <c>ProcessInfo.AppName</c>（字段名不同以保持向后兼容）。
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     <c>ClientVersion</c> 映射到 <c>ProcessInfo.CurrentVersion</c>。
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     压缩编码 (<see cref="Encoding" />) 和压缩格式 (<see cref="Format" />) 是管道阶段计算后的值。
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     黑名单参数来自 <c>BlackListManager</c>，在映射过程中作为独立参数传入。
        ///                 </description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </remarks>
        /// <param name="source">
        ///     包含所有运行时状态的内部配置对象。不能为 <c>null</c>。
        /// </param>
        /// <param name="updateVersions">
        ///     来自更新服务器响应的版本信息对象列表。
        /// </param>
        /// <param name="blackFileFormats">
        ///     来自 <c>BlackListManager</c> 的黑名单文件格式列表。
        /// </param>
        /// <param name="blackFiles">
        ///     来自 <c>BlackListManager</c> 的黑名单文件列表。
        /// </param>
        /// <param name="skipDirectories">
        ///     来自 <c>BlackListManager</c> 的需跳过目录列表。
        /// </param>
        /// <returns>
        ///     一个准备序列化用于进程间通信的 <see cref="ProcessInfo" /> 对象。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     当 <paramref name="source" /> 为 <c>null</c> 时抛出。
        /// </exception>
        public static ProcessInfo MapToProcessInfo(
            GlobalConfigInfo source,
            List<VersionInfo> updateVersions,
            List<string> blackFileFormats,
            List<string> blackFiles,
            List<string> skipDirectories)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source), "GlobalConfigInfo source cannot be null");

            // 在单一位置创建 ProcessInfo，包含所有必需参数
            // 集中管理 ProcessInfo 的参数映射逻辑
            return new ProcessInfo(
                appName: source.MainAppName,                    // MainAppName 映射到 ProcessInfo.UpdateAppName
                installPath: source.InstallPath,
                currentVersion: source.ClientVersion,           // ClientVersion 映射到 ProcessInfo.CurrentVersion
                lastVersion: source.LastVersion,                // 调用此方法前已计算好的值
                updateLogUrl: source.UpdateLogUrl,
                compressEncoding: source.Encoding,               // 调用此方法前已计算好的值
                compressFormat: source.Format.ToExtension(),     // 调用此方法前已计算好的值
                downloadTimeOut: source.DownloadTimeOut,         // 调用此方法前已计算好的值
                appSecretKey: source.AppSecretKey,
                updateVersions: updateVersions,                  // 来自 API 响应
                reportUrl: source.ReportUrl,
                backupDirectory: source.BackupDirectory,         // 调用此方法前已计算好的值
                bowl: source.Bowl,
                scheme: source.Scheme,
                token: source.Token,
                driverDirectory: source.DriverDirectory,         // 驱动更新所需的目录
                tempPath: source.TempPath,                       // 客户端的下载临时路径，供升级进程定位包文件
                blackFileFormats: blackFileFormats,              // 来自 BlackListManager
                blackFiles: blackFiles,                          // 来自 BlackListManager
                skipDirectories: skipDirectories,                // 来自 BlackListManager
                upgradePath: source.UpdatePath,                  // 自定义升级目录
                launchClient: source.LaunchClientAfterUpdate
            );
        }
    }
}
