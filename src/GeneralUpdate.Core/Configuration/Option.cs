using System;
using System.Collections.Concurrent;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Base class for strongly-typed update option keys.
    ///     Uses a simple registry pattern backed by <see cref="ConcurrentDictionary{TKey, TValue}" />,
    ///     replacing the Netty ConstantPool pattern used in earlier versions.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Option</c> implements a global registry for option names, ensuring each name maps to a
    ///         unique option instance. Instances are obtained or created via the
    ///         <see cref="ValueOf{T}(string, T)" /> static method.
    ///     </para>
    ///     <para>
    ///         This class uses double-checked locking (via the <c>_lock</c> object) to ensure thread-safe creation
    ///         and registration of option instances. The underlying <c>_registry</c> dictionary uses
    ///         <see cref="ConcurrentDictionary{TKey, TValue}" /> to support high-concurrency reads.
    ///     </para>
    ///     <para>
    ///         Usage example:
    ///         <code>
    ///             var option = Option.ValueOf&lt;int&gt;("MAXCONCURRENCY", 3);
    ///             int defaultValue = option.DefaultValue; // 3
    ///         </code>
    ///     </para>
    /// </remarks>
    /// <seealso cref="Option{T}" />
    public class Option
    {
        /// <summary>
        ///     The global option registry storing all created option instances.
        ///     Keys are option names (strings), values are <see cref="Option" /> instances.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Option> _registry = new();

        /// <summary>
        ///     The unique name identifier for this option.
        ///     Remains unique throughout the application lifetime.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Protected constructor to prevent direct instantiation.
        ///     Instances should be created via the <see cref="ValueOf{T}(string, T)" /> static method.
        /// </summary>
        /// <param name="name">The unique name identifier for the option.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
        protected Option(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        ///     Gets or creates an <see cref="Option{T}" /> instance for the specified name.
        ///     If an option with the specified name already exists, the existing instance is returned;
        ///     otherwise, a new instance is created with the provided default value.
        ///     Subsequent calls with the same name will always return the same instance (singleton pattern).
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        /// <param name="name">The unique name identifier for the option.</param>
        /// <param name="defaultValue">The default value used when the option is not explicitly set.</param>
        /// <returns>
        ///     The <see cref="Option{T}" /> instance corresponding to <paramref name="name" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
        public static Option<T> ValueOf<T>(string name, T defaultValue = default!)
        {
            // GetOrAdd is lock-free on ConcurrentDictionary. Under contention the
            // factory may run multiple times, but only one instance is stored and
            // returned by all racing callers. The factory must be side-effect-free.
            var raw = _registry.GetOrAdd(name, _ => new Option<T>(name, defaultValue));

            // If the existing entry was registered with a different type T, create a new one.
            // This is the same "last writer wins" behavior as the original lock-based implementation.
            if (raw is Option<T> typed)
                return typed;

            var replacement = new Option<T>(name, defaultValue);
            _registry[name] = replacement;
            return replacement;
        }

        /// <summary>
        ///     Returns the string representation of the option name.
        /// </summary>
        /// <returns>The unique name identifier of the option.</returns>
        public override string ToString() => Name;

        /// <summary>
        ///     Returns the hash code based on the option name.
        /// </summary>
        /// <returns>The hash code of the option name.</returns>
        public override int GetHashCode() => Name.GetHashCode();

        /// <summary>
        ///     Determines whether two options are equal based on their names.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>
        ///     <c>true</c> if <paramref name="obj" /> is an <see cref="Option" /> with the same name;
        ///     otherwise <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj)
            => obj is Option other && Name == other.Name;

        // ──────────────────────────────────────
        //  Predefined framework option keys
        // ──────────────────────────────────────

        public static Option<AppType> AppType { get; } = ValueOf<AppType>("APPTYPE", Configuration.AppType.Client);

        public static Option<DiffMode> DiffMode { get; } = ValueOf<DiffMode>("DIFFMODE", Configuration.DiffMode.Serial);

        public static Option<Encoding> Encoding { get; } = ValueOf<Encoding>("COMPRESSENCODING", System.Text.Encoding.UTF8);

        public static Option<Format> Format { get; } = ValueOf<Format>("COMPRESSFORMAT", Configuration.Format.Zip);

        public static Option<int?> DownloadTimeout { get; } = ValueOf<int?>("DOWNLOADTIMEOUT", 30);

        public static Option<bool?> PatchEnabled { get; } = ValueOf<bool?>("PATCH", true);

        public static Option<bool?> BackupEnabled { get; } = ValueOf<bool?>("BACKUP", false);

        public static Option<bool> Silent { get; } = ValueOf<bool>("ENABLESILENTUPDATE", false);

        public static Option<int> SilentPollIntervalMinutes { get; } = ValueOf<int>("SILENTPOLLINTERVALMINUTES", 60);

        public static Option<bool> LaunchClientAfterUpdate { get; } = ValueOf<bool>("LAUNCHCLIENTAFTERUPDATE", true);

        /// <summary>
        ///     自检总开关。默认 <c>false</c> —— 关闭时更新流程与历史版本完全一致。
        ///     开启后，updater 在主程序更新完成后进入观察窗口验证应用可用性，
        ///     不可用则自动回滚（见 <c>SelfCheck</c> 命名空间与 docs/self-check-design.md）。
        /// </summary>
        /// <remarks>
        ///     <c>bool?</c> 语义与 <see cref="PatchEnabled"/>/<see cref="BackupEnabled"/> 一致：
        ///     updater 侧 <c>??=</c> 保留 IPC 传入值，用户显式 <c>SetOption</c> 仍然优先。
        /// </remarks>
        public static Option<bool?> SelfCheckEnabled { get; } = ValueOf<bool?>("SELFCHECKENABLED", false);

        /// <summary>
        ///     进程启动观察窗口（秒）。自检开启时，主程序启动后在该窗口内裁决其是否可用。
        /// </summary>
        public static Option<int> SelfCheckStartupWindowSeconds { get; } = ValueOf<int>("SELFCHECKSTARTUPWINDOWSECONDS", 30);

        /// <summary>
        ///     严格模式：要求主程序在窗口内写入 verify-ok 应答文件，超时未应答视为失败。
        ///     默认宽松：窗口内进程存活即视为可用（兼容未接入确认协议的宿主）。
        /// </summary>
        public static Option<bool> SelfCheckRequireAck { get; } = ValueOf<bool>("SELFCHECKREQUIREACK", false);

        /// <summary>
        ///     更新完成后是否由 updater 拉起 Bowl 守护进程。
        ///     默认 <c>false</c> —— Bowl 独立安装后的主入口是主程序自拉起
        ///     （<c>BowlLauncher.EnsureRunningAsync</c>），updater 拉起仅为兼容路径。
        /// </summary>
        public static Option<bool> LaunchBowl { get; } = ValueOf<bool>("LAUNCHBOWL", false);

        /// <summary>
        ///     诊断上报总开关。默认 <c>false</c>。开启后 Core 在主程序启动时后台扫描
        ///     fail 目录 outbox 补传崩溃采证，并在自检失败/回滚时实时上传诊断包。
        ///     Core 是唯一网络出口；Bowl 本身无网络依赖。
        /// </summary>
        public static Option<bool> DiagnosticsUploadEnabled { get; } = ValueOf<bool>("DIAGNOSTICSUPLOADENABLED", false);

        /// <summary>
        ///     诊断上报服务端地址。独立于状态上报地址（<c>UpdateRequest.ReportUrl</c> 只承载
        ///     UpdateStatus 状态上报，二者互不影响），为空时视为未配置诊断通道。
        /// </summary>
        public static Option<string> DiagnosticsReportUrl { get; } = ValueOf<string>("DIAGNOSTICSREPORTURL", string.Empty);

        public static Option<int> MaxConcurrency { get; } = ValueOf<int>("MAXCONCURRENCY", 3);

        public static Option<bool> EnableResume { get; } = ValueOf<bool>("ENABLERESUME", true);

        public static Option<int> RetryCount { get; } = ValueOf<int>("RETRYCOUNT", 3);

        public static Option<bool> VerifyChecksum { get; } = ValueOf<bool>("VERIFYCHECKSUM", true);

        public static Option<TimeSpan> RetryInterval { get; } = ValueOf<TimeSpan>("RETRYINTERVAL", TimeSpan.FromSeconds(1));
    }

    /// <summary>
    ///     A strongly-typed update option key with a default value.
    ///     Instances are obtained via <see cref="Option.ValueOf{T}(string, T)" />.
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <remarks>
    ///     <para>
    ///         <c>Option{T}</c> is a sealed class that inherits from <see cref="Option" />,
    ///         adding type-safe default value storage on top of the base class.
    ///     </para>
    ///     <para>
    ///         This class works together with <see cref="OptionValue{T}" /> —
    ///         <c>Option{T}</c> defines the option's "key" and default value,
    ///         while <c>OptionValue{T}</c> stores the option's runtime "value".
    ///     </para>
    /// </remarks>
    /// <seealso cref="Option" />
    /// <seealso cref="OptionValue{T}" />
    public sealed class Option<T> : Option
    {
        /// <summary>
        ///     The default value used when the option is not explicitly set.
        /// </summary>
        public T DefaultValue { get; }

        /// <summary>
        ///     Internal constructor called by <see cref="Option.ValueOf{T}(string, T)" />.
        /// </summary>
        /// <param name="name">The unique name identifier for the option.</param>
        /// <param name="defaultValue">The default value for the option.</param>
        internal Option(string name, T defaultValue) : base(name)
        {
            DefaultValue = defaultValue;
        }
    }
}
