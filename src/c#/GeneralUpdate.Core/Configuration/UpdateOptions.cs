using System;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     框架级别的更新选项常量定义。
    ///     每个选项均具有唯一的字符串名称和合理的默认值。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOptions</c> 是一个静态类，集中定义了更新框架所有可配置选项的
    ///         <see cref="UpdateOption{T}" /> 实例。每个选项包含一个唯一的字符串名称和框架默认值。
    ///     </para>
    ///     <para>
    ///         与业务相关的配置（URL、密钥、应用名称等）不属于此类，
    ///         应放置在 <see cref="Configinfo" /> 或 <see cref="BaseConfigInfo" /> 中。
    ///         此类仅负责行为层面的选项（如是否启用断点续传、重试次数、并发数等）。
    ///     </para>
    ///     <para>
    ///         选项值的存储和检索通过 <see cref="UpdateOption.ValueOf{T}(string, T)" /> 实现，
    ///         该机制基于 <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}" />
    ///         保证每个名称对应唯一的选项实例（单例模式）。
    ///     </para>
    ///     <para>
    ///         各选项的默认值来源：
    ///         <list type="bullet">
    ///             <item><description>并发下载数默认为 3（<see cref="MaxConcurrency" />）</description></item>
    ///             <item><description>断点续传默认为启用（<see cref="EnableResume" />）</description></item>
    ///             <item><description>下载超时默认为 30 秒（<see cref="DownloadTimeout" />）</description></item>
    ///             <item><description>差异补丁默认为启用（<see cref="PatchEnabled" />）</description></item>
    ///             <item><description>更新前备份默认为启用（<see cref="BackupEnabled" />）</description></item>
    ///             <item><description>静默更新默认为禁用（<see cref="Silent" />）</description></item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOption{T}" />
    /// <seealso cref="Configinfo" />
    /// <seealso cref="BaseConfigInfo" />
    public static class UpdateOptions
    {
        // ════════════════════════════════════════
        //  核心选项
        // ════════════════════════════════════════

        /// <summary>
        ///     应用程序角色类型 — <see cref="AppType.Client" />、<see cref="AppType.Upgrade" />
        ///     或 <see cref="AppType.OSS" />。
        ///     默认值为 <see cref="AppType.Client" />。
        /// </summary>
        public static UpdateOption<AppType> AppType { get; } = UpdateOption.ValueOf<AppType>("APPTYPE", Configuration.AppType.Client);

        // ════════════════════════════════════════
        //  差异模式
        // ════════════════════════════════════════

        /// <summary>
        ///     差异/补丁生成模式 — <see cref="DiffMode.Serial" />（串行）或
        ///     <see cref="DiffMode.Parallel" />（并行）。
        ///     默认值为 <see cref="DiffMode.Serial" />。
        /// </summary>
        public static UpdateOption<DiffMode> DiffMode { get; } = UpdateOption.ValueOf<DiffMode>("DIFFMODE", Configuration.DiffMode.Serial);

        // ════════════════════════════════════════
        //  向后兼容选项
        // ════════════════════════════════════════

        /// <summary>
        ///     更新包使用的压缩编码。
        ///     默认值为 <see cref="System.Text.Encoding.UTF8" />。
        /// </summary>
        public static UpdateOption<Encoding> Encoding { get; } = UpdateOption.ValueOf<Encoding>("COMPRESSENCODING", System.Text.Encoding.UTF8);

        /// <summary>
        ///     更新包使用的压缩格式。
        ///     默认值为 <see cref="Configuration.Format.Zip" />。
        /// </summary>
        public static UpdateOption<Format> Format { get; } = UpdateOption.ValueOf<Format>("COMPRESSFORMAT", Configuration.Format.Zip);

        /// <summary>
        ///     下载操作的超时时间（秒）。
        ///     默认值为 30 秒。
        /// </summary>
        public static UpdateOption<int?> DownloadTimeout { get; } = UpdateOption.ValueOf<int?>("DOWNLOADTIMEOUT", 30);

        /// <summary>
        ///     是否启用差异补丁更新。
        ///     默认值为 <c>true</c>。
        /// </summary>
        public static UpdateOption<bool?> PatchEnabled { get; } = UpdateOption.ValueOf<bool?>("PATCH", true);

        /// <summary>
        ///     更新前是否启用备份。
        ///     默认值为 <c>true</c>。
        /// </summary>
        public static UpdateOption<bool?> BackupEnabled { get; } = UpdateOption.ValueOf<bool?>("BACKUP", true);

        /// <summary>
        ///     是否启用静默后台更新模式。
        ///     默认值为 <c>false</c>。
        /// </summary>
        public static UpdateOption<bool> Silent { get; } = UpdateOption.ValueOf<bool>("ENABLESILENTUPDATE", false);

        // ════════════════════════════════════════
        //  静默模式选项
        // ════════════════════════════════════════

        /// <summary>
        ///     静默更新模式下检查更新的轮询间隔（分钟）。
        ///     默认值为 60 分钟。
        /// </summary>
        public static UpdateOption<int> SilentPollIntervalMinutes { get; } = UpdateOption.ValueOf<int>("SILENTPOLLINTERVALMINUTES", 60);

        // ════════════════════════════════════════
        //  并发与断点续传选项
        // ════════════════════════════════════════

        /// <summary>
        ///     最大并发下载操作数。
        ///     默认值为 3。
        /// </summary>
        public static UpdateOption<int> MaxConcurrency { get; } = UpdateOption.ValueOf<int>("MAXCONCURRENCY", 3);

        /// <summary>
        ///     是否启用 HTTP 断点续传（基于 Range 请求头）。
        ///     默认值为 <c>true</c>。
        /// </summary>
        public static UpdateOption<bool> EnableResume { get; } = UpdateOption.ValueOf<bool>("ENABLERESUME", true);

        // ════════════════════════════════════════
        //  容错与重试选项
        // ════════════════════════════════════════

        /// <summary>
        ///     下载失败时的最大重试次数。
        ///     默认值为 3。
        /// </summary>
        public static UpdateOption<int> RetryCount { get; } = UpdateOption.ValueOf<int>("RETRYCOUNT", 3);

        /// <summary>
        ///     下载完成后是否执行 SHA256 校验和验证。
        ///     默认值为 <c>true</c>。
        /// </summary>
        public static UpdateOption<bool> VerifyChecksum { get; } = UpdateOption.ValueOf<bool>("VERIFYCHECKSUM", true);

        /// <summary>
        ///     指数退避策略的初始重试间隔。
        ///     默认值为 1 秒。
        /// </summary>
        public static UpdateOption<TimeSpan> RetryInterval { get; } = UpdateOption.ValueOf<TimeSpan>("RETRYINTERVAL", TimeSpan.FromSeconds(1));
    }
}
