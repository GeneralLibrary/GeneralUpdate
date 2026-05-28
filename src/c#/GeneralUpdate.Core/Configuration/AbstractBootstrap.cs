using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Core.Strategy;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// 提供引导程序基类，支持泛型自引用（CRTP）模式，用于配置和启动更新流程。
    /// </summary>
    /// <typeparam name="TBootstrap">派生引导程序类型，必须继承自 <see cref="AbstractBootstrap{TBootstrap, TStrategy}"/>。</typeparam>
    /// <typeparam name="TStrategy">更新策略类型，必须实现 <see cref="IStrategy"/>。</typeparam>
    /// <remarks>
    /// <para>本类采用扩展点注册/解析模式来管理更新流程中的可替换组件。核心机制如下：</para>
    /// <para>
    /// - <c>_extensions</c> 字典存储 Type→Type 的映射（接口类型→实现类型），通过 <c>.Strategy&lt;T&gt;()</c>、
    ///   <c>.DownloadSource&lt;T&gt;()</c> 等方法注册，在需要时通过 <see cref="ResolveExtension{TExtension}"/> 延迟实例化。
    /// </para>
    /// <para>
    /// - <c>_instances</c> 字典存储已实例化的单例对象（如 <c>BlackListConfig</c>），
    ///   优先于 <c>_extensions</c> 中的延迟注册。
    /// </para>
    /// <para>
    /// - <see cref="Option{T}(UpdateOption{T}, T)"/> 方法提供流式配置选项，通过 <see cref="GetOption{T}(UpdateOption{T}?)"/> 读取，
    ///   支持默认值回退机制。
    /// </para>
    /// <para>典型用法：链式调用注册各种扩展组件，最后调用 <see cref="LaunchAsync"/> 启动更新流程。</para>
    /// </remarks>
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
        where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
        where TStrategy : IStrategy
    {
        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> _options;

        /// <summary>用户注册的扩展类型映射（接口类型→实现类型），用于延迟实例化。</summary>
        private readonly Dictionary<Type, Type> _extensions = new();

        /// <summary>已注册的单例实例（如 <c>BlackListConfig</c>）。</summary>
        private readonly Dictionary<Type, object> _instances = new();

        protected internal AbstractBootstrap()
        {
            _options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();
        }

        public abstract Task<TBootstrap> LaunchAsync();

        /// <summary>
        /// 设置更新选项值，支持流式（fluent）调用。
        /// </summary>
        /// <typeparam name="T">选项值的类型。</typeparam>
        /// <param name="option">要设置的选项键。</param>
        /// <param name="value">要设置的选项值。如果为 <c>null</c>，则从字典中移除该选项，后续读取将回退到默认值。</param>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 使用 <c>ConcurrentDictionary</c> 存储选项，保证线程安全。
        /// 如果 <paramref name="value"/> 为 <c>null</c>，则删除该选项条目，使得 <see cref="GetOption{T}(UpdateOption{T}?)"/> 返回 <see cref="UpdateOption{T}.DefaultValue"/>。
        /// </remarks>
        public TBootstrap Option<T>(UpdateOption<T> option, T value)
        {
            if (value == null)
                _options.TryRemove(option, out _);
            else
                _options[option] = new UpdateOptionValue<T>(option, value);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 获取指定选项的值。如果选项为 <c>null</c> 或未注册，则返回默认值。
        /// </summary>
        /// <typeparam name="T">选项值的类型。</typeparam>
        /// <param name="option">要获取的选项键，可为 <c>null</c>。</param>
        /// <returns>
        /// 如果找到注册的值则返回该值；否则返回 <see cref="UpdateOption{T}.DefaultValue"/>。
        /// </returns>
        /// <remarks>
        /// 先尝试从 <c>_options</c> 字典中查找，如果未找到则回退到 <see cref="UpdateOption{T}.DefaultValue"/>。
        /// 这是 <see cref="Option{T}(UpdateOption{T}, T)"/> 的配套读取方法。
        /// </remarks>
        protected T GetOption<T>(UpdateOption<T>? option)
        {
            if (option == null) return default!;
            if (_options.TryGetValue(option, out var val) && val != null)
                return (T)val.GetValue();
            return option.DefaultValue;
        }

        // ═══════════ Extension point registration ═══════════

        /// <summary>
        /// 注册自定义的 OS 级别策略实现（如 <see cref="Strategy.WindowsStrategy"/>、<see cref="Strategy.LinuxStrategy"/>、<see cref="Strategy.MacStrategy"/>）。
        /// </summary>
        /// <typeparam name="T">策略实现类型，必须实现 <see cref="IStrategy"/> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 注册后，当 <c>ClientUpdateStrategy</c> 调用 <c>ResolveOsStrategy()</c> 时会优先使用此处注册的类型，
        /// 而非自动探测当前操作系统。此扩展点在 <see cref="LaunchAsync"/> 执行时生效。
        /// </remarks>
        public TBootstrap Strategy<T>() where T : IStrategy, new()
        {
            _extensions[typeof(IStrategy)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册更新生命周期钩子实现，用于在更新流程的关键节点注入自定义逻辑。
        /// </summary>
        /// <typeparam name="T">钩子实现类型，必须实现 <c>IUpdateHooks</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// <para>钩子回调在以下节点被调用：</para>
        /// <para>- <c>OnBeforeUpdateAsync</c>：下载开始前，可取消更新；</para>
        /// <para>- <c>OnDownloadCompletedAsync</c>：所有文件下载完成后；</para>
        /// <para>- <c>OnAfterUpdateAsync</c>：升级包应用完成后；</para>
        /// <para>- <c>OnBeforeStartAppAsync</c>：启动升级进程前；</para>
        /// <para>- <c>OnUpdateErrorAsync</c>：更新过程中出现异常时。</para>
        /// <para>默认使用 <c>NoOpUpdateHooks</c>（无操作实现）。所有钩子调用都包裹在 try-catch 中，单个钩子失败不会阻断流程。</para>
        /// </remarks>
        public TBootstrap Hooks<T>() where T : Hooks.IUpdateHooks, new()
        {
            _extensions[typeof(Hooks.IUpdateHooks)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册 SSL 验证策略实现，用于自定义 HTTPS 证书验证行为。
        /// </summary>
        /// <typeparam name="T">SSL 策略实现类型，必须实现 <c>ISslValidationPolicy</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 可用于跳过自签名证书验证、使用自定义根证书颁发机构或实施证书锁定等场景。
        /// 此扩展点在发起 HTTP 下载请求时由 <c>HttpClientProvider</c> 读取并应用于 <c>HttpClientHandler</c>。
        /// </remarks>
        public TBootstrap SslPolicy<T>() where T : Security.ISslValidationPolicy, new()
        {
            _extensions[typeof(Security.ISslValidationPolicy)] = typeof(T);
            return (TBootstrap)this;
        }
        
        /// <summary>
        /// 注册自定义下载重试/超时策略，用于控制下载失败时的重试行为。
        /// </summary>
        /// <typeparam name="T">下载策略实现类型，必须实现 <c>IDownloadPolicy</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 仅在使用默认下载编排器（<c>DefaultDownloadOrchestrator</c>）时生效。
        /// 如果同时通过 <see cref="DownloadOrchestrator{T}"/> 注册了自定义编排器，则此设置被忽略。
        /// 下载策略决定每次失败后的等待时间和最大重试次数。
        /// </remarks>
        public TBootstrap DownloadPolicy<T>() where T : Download.Abstractions.IDownloadPolicy, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadPolicy)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册自定义单文件下载执行器，用于支持非 HTTP/HTTPS 下载协议。
        /// </summary>
        /// <typeparam name="T">下载执行器实现类型，必须实现 <c>IDownloadExecutor</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 仅在使用默认下载编排器（<c>DefaultDownloadOrchestrator</c>）时生效。
        /// 如果同时通过 <see cref="DownloadOrchestrator{T}"/> 注册了自定义编排器，则此设置被忽略。
        /// 可用于实现 FTP、SFTP 或私有协议的文件下载。
        /// </remarks>
        public TBootstrap DownloadExecutor<T>() where T : Download.Abstractions.IDownloadExecutor, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadExecutor)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册下载数据源实现，用于从服务器获取版本信息和更新文件清单。
        /// </summary>
        /// <typeparam name="T">下载源实现类型，必须实现 <c>IDownloadSource</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// <para>内置实现包括 <c>HttpDownloadSource</c>（基于 HTTP/HTTPS）和 SignalR Hub 下载源。</para>
        /// <para>通过此方法可注册自定义数据源，如从本地文件系统、FTP 服务器或私有云存储获取更新清单。</para>
        /// <para>此扩展点在 <c>ClientUpdateStrategy.ExecuteStandardWorkflowAsync()</c> 中调用 <c>downloadSource.ListAsync()</c> 时生效。</para>
        /// </remarks>
        public TBootstrap DownloadSource<T>() where T : Download.Abstractions.IDownloadSource, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadSource)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册自定义下载后处理管道，用于对已下载的文件进行哈希校验、解密、病毒扫描等后处理。
        /// </summary>
        /// <typeparam name="T">下载管道实现类型，必须实现 <c>IDownloadPipeline</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 仅在使用默认下载编排器（<c>DefaultDownloadOrchestrator</c>）时生效。
        /// 如果同时通过 <see cref="DownloadOrchestrator{T}"/> 注册了自定义编排器，则此设置被忽略。
        /// 下载管道接收下载完成的文件路径，可执行完整性校验、解密加密文件或扫描恶意软件等操作。
        /// </remarks>
        public TBootstrap DownloadPipeline<T>() where T : Download.Abstractions.IDownloadPipeline, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadPipeline)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册更新状态报告器，用于向服务器（如 GeneralSpacestation）上报更新进度和结果。
        /// </summary>
        /// <typeparam name="T">报告器实现类型，必须实现 <c>IUpdateReporter</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// <para>报告器在更新流程的关键节点被调用：</para>
        /// <para>- 更新开始：<c>ReportAsync(Updating)</c>；</para>
        /// <para>- 下载完成：<c>ReportAsync(Updating)</c>；</para>
        /// <para>- 更新应用成功：<c>ReportAsync(Success)</c>；</para>
        /// <para>- 更新失败：<c>ReportAsync(Failure)</c>。</para>
        /// <para>默认使用 <c>NoOpUpdateReporter</c>（无操作实现）。所有上报调用都包裹在 try-catch 中，单个上报失败不会阻断流程。</para>
        /// </remarks>
        public TBootstrap UpdateReporter<T>() where T : Download.Reporting.IUpdateReporter, new()
        {
            _extensions[typeof(Download.Reporting.IUpdateReporter)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册 HTTP 身份验证提供程序，用于在下载请求中添加认证信息。
        /// </summary>
        /// <typeparam name="T">认证提供程序实现类型，必须实现 <c>IHttpAuthProvider</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// 可用于支持需要令牌认证（Bearer Token）、基本认证（Basic Auth）或自定义认证头的下载服务器。
        /// 此扩展点在创建 HTTP 下载请求时由 <c>HttpClientProvider</c> 读取并注入请求头。
        /// </remarks>
        public TBootstrap UpdateAuth<T>() where T : Security.IHttpAuthProvider, new()
        {
            _extensions[typeof(Security.IHttpAuthProvider)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 注册自定义下载编排器，端到端处理批量下载任务。
        /// </summary>
        /// <typeparam name="T">编排器实现类型，必须实现 <c>IDownloadOrchestrator</c> 并具有无参构造函数。</typeparam>
        /// <returns>返回当前 <typeparamref name="TBootstrap"/> 实例，支持链式调用。</returns>
        /// <remarks>
        /// <para>这是最高级别的下载抽象，拥有完整的下载管道的所有权。</para>
        /// <para>当设置自定义编排器后，<see cref="DownloadPolicy{T}"/>、<see cref="DownloadExecutor{T}"/> 和 <see cref="DownloadPipeline{T}"/> 的注册将被忽略，
        /// 因为自定义编排器完全接管了下载流程的控制权。</para>
        /// <para>适用于需要完全自定义下载行为（如使用第三方下载库、分块下载、多线程下载等）的高级场景。</para>
        /// </remarks>
        public TBootstrap DownloadOrchestrator<T>() where T : Download.Abstractions.IDownloadOrchestrator, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadOrchestrator)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// 解析并实例化指定接口类型的扩展。采用两阶段查找策略。
        /// </summary>
        /// <typeparam name="TExtension">要解析的扩展接口类型。</typeparam>
        /// <returns>扩展的实例；如果未注册对应的实现类型，则返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>两阶段查找：</para>
        /// <para>第一阶段：在 <c>_extensions</c> 字典中按接口类型查找已注册的实现类型 <c>Type</c>，
        /// 找到后通过 <c>Activator.CreateInstance</c> 创建新实例。</para>
        /// <para>第二阶段：如果在 <c>_extensions</c> 中未找到，则在 <c>_instances</c> 字典中查找已存在的单例实例。</para>
        /// <para><c>_instances</c> 中的单例实例优先于 <c>_extensions</c> 中的延迟注册。</para>
        /// </remarks>
        protected TExtension? ResolveExtension<TExtension>() where TExtension : class
        {
            if (_extensions.TryGetValue(typeof(TExtension), out var t))
                return Activator.CreateInstance((Type)t) as TExtension;
            if (_instances.TryGetValue(typeof(TExtension), out var instance))
                return instance as TExtension;
            return null;
        }

        /// <summary>
        /// 解析指定扩展接口的注册实现类型，但不实例化该类型。
        /// </summary>
        /// <typeparam name="TExtension">要解析的扩展接口类型。</typeparam>
        /// <returns>注册的实现类型；如果未注册则返回 <c>null</c>。</returns>
        /// <remarks>
        /// 不同于 <see cref="ResolveExtension{TExtension}"/>，此方法仅返回类型信息，
        /// 适用于需要反射获取类型元数据但暂时不需要创建实例的场景，
        /// 例如需要提前检查某扩展是否已注册以决定流程分支。
        /// </remarks>
        protected Type? ResolveExtensionType<TExtension>() where TExtension : class
        {
            return _extensions.TryGetValue(typeof(TExtension), out var t) ? t : null;
        }
    }
}