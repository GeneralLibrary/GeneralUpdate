using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Provides centralized mapping utility methods between configuration objects.
    ///     Ensures consistent field mapping across <see cref="UpdateRequest" />, <see cref="UpdateContext" />,
    ///     and <see cref="ProcessContract" />, reducing the risk of missed or incorrectly mapped fields during maintenance.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>ConfigurationMapper</c> is the central hub for configuration transformation in the update workflow,
    ///         responsible for mapping in two directions:
    ///     </para>
    ///     <para>
    ///         <list type="number">
    ///             <item>
    ///                 <description>
    ///                     <see cref="MapToUpdateContext" />: Maps the user-provided <see cref="UpdateRequest" />
    ///                     to the internal runtime configuration <see cref="UpdateContext" />. This mapping is
    ///                     performed during the update workflow initialization phase, passing external API configuration
    ///                     parameters into the internal workflow.
    ///                 </description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     <see cref="MapToProcessContract" />: Maps the internal runtime configuration
    ///                     <see cref="UpdateContext" /> to the inter-process communication parameters
    ///                     <see cref="ProcessContract" />. This mapping is performed when the client is about to launch
    ///                     the upgrade process, serializing all computed runtime state for the upgrade process.
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         By centralizing all mapping logic in this class, scattered field assignment logic throughout the
    ///         bootstrap code is avoided, simplifying maintenance and reducing the likelihood of introducing defects.
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateRequest" />
    /// <seealso cref="UpdateContext" />
    /// <seealso cref="ProcessContract" />
    /// <seealso cref="VersionEntry" />
    public static class ConfigurationMapper
    {
        /// <summary>
        ///     Maps the user-provided configuration (<see cref="UpdateRequest" />) to the internal runtime configuration
        ///     (<see cref="UpdateContext" />).
        ///     Performs one-to-one field mapping for all shared configuration properties.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method performs a shallow copy mapping, assigning all public and base class properties from
        ///         <see cref="UpdateRequest" /> to a <see cref="UpdateContext" /> instance one by one.
        ///     </para>
        ///     <para>
        ///         If <paramref name="target" /> is <c>null</c>, a new <see cref="UpdateContext" /> instance is
        ///         automatically created. If <paramref name="source" /> is <c>null</c>, the method returns the empty
        ///         (or newly created) target instance without throwing an exception.
        ///     </para>
        /// </remarks>
        /// <param name="source">
        ///     The user-provided configuration object containing initial settings. Can be <c>null</c>.
        /// </param>
        /// <param name="target">
        ///     The internal configuration object to populate. If <c>null</c>, a new instance is automatically created.
        /// </param>
        /// <returns>
        ///     A <see cref="UpdateContext" /> instance populated with configuration values from <paramref name="source" />.
        /// </returns>
        public static UpdateContext MapToUpdateContext(UpdateRequest source, UpdateContext target = null)
        {
            // 如果 source 和 target 均未提供，则创建新实例
            if (target == null)
                target = new UpdateContext();

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
            target.Files = source.Files;
            target.Formats = source.Formats;
            target.Directories = source.Directories;
            target.ReportUrl = source.ReportUrl;
            target.Bowl = source.Bowl;
            target.Scheme = source.Scheme;
            target.Token = source.Token;
            target.DriverDirectory = source.DriverDirectory;
            target.AppType = source.AppType;
            target.UpdatePath = source.UpdatePath;
            target.UpdateUrl = source.UpdateUrl;
            target.UpgradeClientVersion = source.UpgradeClientVersion;
            target.ProductId = source.ProductId;

            return target;
        }

        /// <summary>
        ///     Maps the internal runtime configuration (<see cref="UpdateContext" />) to inter-process communication
        ///     parameters (<see cref="ProcessContract" />).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method centralizes the complex parameter passing logic that was previously scattered across
        ///         the bootstrap code. The resulting <see cref="ProcessContract" /> object is serialized to a JSON string
        ///         and passed to the upgrade process via command-line arguments or standard input.
        ///     </para>
        ///     <para>
        ///         The following points should be noted during mapping:
        ///         <list type="bullet">
        ///             <item>
        ///                 <description>
        ///                     <c>MainAppName</c> maps to <c>ProcessContract.AppName</c> (different field name for backward compatibility).
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     <c>ClientVersion</c> maps to <c>ProcessContract.CurrentVersion</c>.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The compression encoding (<see cref="Encoding" />) and compression format (<see cref="Format" />)
        ///                     are values computed during the pipeline stage.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     Blacklist parameters come from <c>BlackListManager</c> and are passed as separate parameters
        ///                     during mapping.
        ///                 </description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </remarks>
        /// <param name="source">
        ///     The internal configuration object containing all runtime state. Must not be <c>null</c>.
        /// </param>
        /// <param name="updateVersions">
        ///     The list of version information objects from the update server response.
        /// </param>
        /// <param name="blackFileFormats">
        ///     The list of blacklisted file formats from <c>BlackListManager</c>.
        /// </param>
        /// <param name="blackFiles">
        ///     The list of blacklisted files from <c>BlackListManager</c>.
        /// </param>
        /// <param name="skipDirectories">
        ///     The list of directories to skip from <c>BlackListManager</c>.
        /// </param>
        /// <returns>
        ///     A <see cref="ProcessContract" /> object ready for serialization and inter-process communication.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="source" /> is <c>null</c>.
        /// </exception>
        public static ProcessContract MapToProcessContract(
            UpdateContext source,
            List<VersionEntry> updateVersions,
            List<string> blackFileFormats,
            List<string> blackFiles,
            List<string> skipDirectories,
            int reportType = 1)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source), "UpdateContext source cannot be null");

            // 在单一位置创建 ProcessContract，包含所有必需参数
            // 集中管理 ProcessContract 的参数映射逻辑
            var processInfo = new ProcessContract(
                appName: source.MainAppName,                    // MainAppName 映射到 ProcessContract.UpdateAppName
                installPath: source.InstallPath,
                currentVersion: source.ClientVersion,           // ClientVersion 映射到 ProcessContract.CurrentVersion
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
            processInfo.ReportType = reportType;
            return processInfo;
        }
    }
}
