using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     内部运行时配置类，将用户提供的配置与计算后的运行时状态相结合。
///     作为更新工作流执行过程中的核心配置持有者，同时继承 <see cref="BaseConfigInfo" />
///     的公共字段并添加运行时特有的属性。
/// </summary>
/// <remarks>
///     <para>
///         <c>GlobalConfigInfo</c> 是更新流程的内部枢纽配置对象。它通过
///         <see cref="ConfigurationMapper.MapToGlobalConfigInfo" /> 从 <see cref="Configinfo" />
///         映射而来，并在更新管道的各个阶段（下载、解压、差异更新等）被逐步填充运行时计算值。
///     </para>
///     <para>
///         主要职责包括：
///         <list type="bullet">
///             <item>
///                 <description>存储用户提供的配置（继承自 <see cref="Configinfo" />）</description>
///             </item>
///             <item>
///                 <description>维护计算后的运行时值（编码、格式、路径、版本号等）</description>
///             </item>
///             <item>
///                 <description>跟踪更新工作流状态（<see cref="IsMainUpdate" />、<see cref="IsUpgradeUpdate" />）</description>
///             </item>
///         </list>
///     </para>
///     <para>
///         在更新管道执行完毕后，<c>GlobalConfigInfo</c> 会通过
///         <see cref="ConfigurationMapper.MapToProcessInfo" /> 映射为 <see cref="ProcessInfo" />，
///         序列化为 JSON 后通过 IPC 传递给升级进程。
///     </para>
/// </remarks>
/// <seealso cref="BaseConfigInfo" />
/// <seealso cref="Configinfo" />
/// <seealso cref="ConfigurationMapper" />
/// <seealso cref="ProcessInfo" />
public class GlobalConfigInfo : BaseConfigInfo
{
    // ──────────────────────────────
    //  用户配置字段（从 Configinfo 映射而来）
    // ──────────────────────────────

    /// <summary>
    ///     用于检查可用更新的 API 端点 URL。
    ///     继承自用户配置，用于向服务器发起版本校验请求。
    /// </summary>
    /// <remarks>
    ///     此值由 <see cref="ConfigurationMapper.MapToGlobalConfigInfo" /> 从
    ///     <see cref="Configinfo.UpdateUrl" /> 映射而来。
    /// </remarks>
    public string UpdateUrl { get; set; }

    /// <summary>
    ///     升级程序（更新器自身）的当前版本号。
    ///     与 <see cref="BaseConfigInfo.ClientVersion" /> 分开管理，用于实现更新器的独立升级。
    /// </summary>
    /// <remarks>
    ///     通过比较此版本号与服务端响应，可以确定 <see cref="IsUpgradeUpdate" /> 的值。
    /// </remarks>
    public string UpgradeClientVersion { get; set; }

    /// <summary>
    ///     当前应用程序的唯一产品标识符。
    ///     用于在共享同一更新服务器的多个产品之间进行区分。
    /// </summary>
    public string ProductId { get; set; }

    // ──────────────────────────────
    //  运行时计算字段（在管道执行过程中计算）
    // ──────────────────────────────

    /// <summary>
    ///     更新包使用的压缩格式。
    ///     从 <see cref="UpdateOptions.Format" /> 计算得出，默认为 ZIP。
    /// </summary>
    public Format Format { get; set; }

    /// <summary>
    ///     指示升级程序（更新器自身）是否需要更新。
    ///     通过比较 <see cref="UpgradeClientVersion" /> 与服务端返回的最新版本号计算得出。
    /// </summary>
    public bool IsUpgradeUpdate { get; set; }

    /// <summary>
    ///     指示主应用程序是否需要更新。
    ///     通过比较 <see cref="BaseConfigInfo.ClientVersion" /> 与服务端返回的最新版本号计算得出。
    /// </summary>
    public bool IsMainUpdate { get; set; }

    /// <summary>
    ///     升级过程完成后是否启动客户端应用程序。
    ///     默认为 <c>true</c>。在静默模式下可设置为 <c>false</c>，
    ///     由调用方自行控制重启时机。
    /// </summary>
    public bool LaunchClientAfterUpdate { get; set; } = true;

    /// <summary>
    ///     需要更新的版本信息对象列表。
    ///     根据 <see cref="IsUpgradeUpdate" /> 和 <see cref="IsMainUpdate" /> 的取值，
    ///     从更新服务器响应中填充。
    /// </summary>
    public List<VersionInfo> UpdateVersions { get; set; }

    /// <summary>
    ///     文件操作所使用的编码格式（例如 UTF-8、ASCII）。
    ///     从 <see cref="UpdateOptions.Encoding" /> 计算得出，默认为系统默认编码。
    /// </summary>
    public Encoding Encoding { get; set; }

    /// <summary>
    ///     下载操作的超时时间（秒）。
    ///     从 <see cref="UpdateOptions.DownloadTimeout" /> 计算得出，默认为 60 秒。
    /// </summary>
    public int DownloadTimeOut { get; set; }

    /// <summary>
    ///     更新服务器上的最新可用版本号。
    ///     在版本校验完成后从服务器响应体中提取。
    /// </summary>
    public string LastVersion { get; set; }

    /// <summary>
    ///     用于下载和提取更新文件的临时目录路径。
    ///     通过 <c>StorageManager.GetTempDirectory("main_temp")</c> 计算得出。
    /// </summary>
    public string TempPath { get; set; }

    /// <summary>
    ///     序列化为 JSON 字符串的 <see cref="ProcessInfo" /> 对象。
    ///     包含升级进程所需的所有参数，用于进程间通信（IPC）。
    /// </summary>
    public string ProcessInfo { get; set; }

    /// <summary>
    ///     是否启用差异补丁更新。
    ///     从 <see cref="UpdateOptions.PatchEnabled" /> 计算得出，默认为 <c>true</c>。
    /// </summary>
    public bool? PatchEnabled { get; set; }

    /// <summary>
    ///     在应用更新前是否备份当前版本。
    ///     从 <see cref="UpdateOptions.BackupEnabled" /> 计算得出，默认为 <c>true</c>。
    /// </summary>
    public bool? BackupEnabled { get; set; }

    /// <summary>
    ///     当前版本文件在更新前备份到的目录路径。
    ///     通过组合 <see cref="BaseConfigInfo.InstallPath" /> 和带版本号的目录名计算得出。
    /// </summary>
    public string BackupDirectory { get; set; }

    // ──────────────────────────────
    //  下载/更新行为选项（从 UpdateOptions 注入）
    // ──────────────────────────────

    /// <summary>
    ///     最大并发下载操作数。
    ///     从 <see cref="UpdateOptions.MaxConcurrency" /> 计算得出，默认为 2。
    /// </summary>
    /// <remarks>
    ///     有效范围为 1 到 <see cref="Environment.ProcessorCount" /> * 2。
    ///     超出此范围的取值会被自动限制到边界值。
    /// </remarks>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    ///     是否通过 HTTP Range 请求支持断点续传。
    ///     从 <see cref="UpdateOptions.EnableResume" /> 计算得出，默认为 <c>true</c>。
    /// </summary>
    public bool EnableResume { get; set; } = true;

    /// <summary>
    ///     下载失败时的最大重试次数。
    ///     从 <see cref="UpdateOptions.RetryCount" /> 计算得出，默认为 3。
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    ///     指数退避策略的初始重试间隔。
    ///     从 <see cref="UpdateOptions.RetryInterval" /> 计算得出，默认为 1 秒。
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     下载完成后是否执行 SHA256 校验和验证。
    ///     从 <see cref="UpdateOptions.VerifyChecksum" /> 计算得出，默认为 <c>true</c>。
    /// </summary>
    public bool VerifyChecksum { get; set; } = true;

    /// <summary>
    ///     差异/补丁生成模式 — 串行 (<see cref="Configuration.DiffMode.Serial" />) 或并行
    ///     (<see cref="Configuration.DiffMode.Parallel" />)。
    ///     从 <see cref="UpdateOptions.DiffMode" /> 计算得出，默认为 <see cref="Configuration.DiffMode.Serial" />。
    /// </summary>
    public DiffMode DiffMode { get; set; } = DiffMode.Serial;
}
