using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Core.Strategy;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Provides the bootstrap base class supporting a generic self-referencing (CRTP) pattern
    /// for configuring and launching the update workflow.
    /// </summary>
    /// <typeparam name="TBootstrap">The derived bootstrap type; must inherit from <see cref="AbstractBootstrap{TBootstrap, TStrategy}"/>.</typeparam>
    /// <typeparam name="TStrategy">The update strategy type; must implement <see cref="IStrategy"/>.</typeparam>
    /// <remarks>
    /// <para>This class uses an extension-point registration/resolution pattern to manage
    /// replaceable components in the update workflow. The core mechanisms are:</para>
    /// <para>
    /// - The <c>_extensions</c> dictionary stores Type→Type mappings (interface type → implementation type).
    ///   Extensions are registered via fluent methods such as <c>.Strategy&lt;T&gt;()</c> or
    ///   <c>.DownloadSource&lt;T&gt;()</c> and are lazily instantiated on demand through
    ///   <see cref="ResolveExtension{TExtension}"/>.<br/>
    /// - The <c>_instances</c> dictionary stores already-instantiated singleton objects
    ///   (e.g., <c>BlackPolicy</c>). These take precedence over lazy registrations
    ///   in <c>_extensions</c>.<br/>
    /// - The <see cref="Option{T}(Option{T}, T)"/> method provides fluent configuration
    ///   options, read via <see cref="GetOption{T}(Option{T}?)"/>, with a default-value
    ///   fallback mechanism.
    /// </para>
    /// <para>Typical usage: chain extension registrations together, then call
    /// <see cref="LaunchAsync"/> to start the update workflow.</para>
    /// </remarks>
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
        where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
        where TStrategy : IStrategy
    {
        private readonly ConcurrentDictionary<Option, OptionValue> _options;

        /// <summary>User-registered extension type mappings (interface type → implementation type), used for lazy instantiation.</summary>
        private readonly Dictionary<Type, Type> _extensions = new();

        /// <summary>Registered singleton instances (e.g., <c>BlackPolicy</c>).</summary>
        private readonly Dictionary<Type, object> _instances = new();

        protected internal AbstractBootstrap()
        {
            _options = new ConcurrentDictionary<Option, OptionValue>();
        }

        public abstract Task<TBootstrap> LaunchAsync();

        /// <summary>
        /// Sets an update option value, supporting fluent chaining.
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        /// <param name="option">The option key to set.</param>
        /// <param name="value">The value to set. If <c>null</c>, the option entry is removed
        /// from the dictionary so that subsequent reads fall back to the default value.</param>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Option are stored in a <c>ConcurrentDictionary</c> to guarantee thread safety.
        /// When <paramref name="value"/> is <c>null</c>, the entry is removed, causing
        /// <see cref="GetOption{T}(Option{T}?)"/> to return
        /// <see cref="Option{T}.DefaultValue"/>.
        /// </remarks>
        public TBootstrap SetOption<T>(Option<T> option, T value)
        {
            if (value == null)
                _options.TryRemove(option, out _);
            else
                _options[option] = new OptionValue<T>(option, value);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Gets the value of the specified option. Returns the default value when the option
        /// is <c>null</c> or has not been registered.
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        /// <param name="option">The option key to retrieve; can be <c>null</c>.</param>
        /// <returns>
        /// The registered value if found; otherwise, <see cref="Option{T}.DefaultValue"/>.
        /// </returns>
        /// <remarks>
        /// First attempts to look up the option in the <c>_options</c> dictionary.
        /// If not found, falls back to <see cref="Option{T}.DefaultValue"/>.
        /// This is the companion read method for <see cref="Option{T}(Option{T}, T)"/>.
        /// </remarks>
        protected T GetOption<T>(Option<T>? option)
        {
            if (option == null) return default!;
            if (_options.TryGetValue(option, out var val) && val != null)
                return (T)val.GetValue();
            return option.DefaultValue;
        }

        // ═══════════ Extension point registration ═══════════
        
        /// <summary>
        /// Registers an update status reporter for reporting update progress and results
        /// to a server (e.g., GeneralSpacestation).
        /// </summary>
        /// <typeparam name="T">The reporter implementation type; must implement
        /// <c>IUpdateReporter</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>The reporter is invoked at key points in the update workflow:</para>
        /// <para>- Update starts: <c>ReportAsync(Updating)</c>.</para>
        /// <para>- Download completes: <c>ReportAsync(Updating)</c>.</para>
        /// <para>- Update applied successfully: <c>ReportAsync(Success)</c>.</para>
        /// <para>- Update failed: <c>ReportAsync(Failure)</c>.</para>
        /// <para>The default implementation is <c>HttpUpdateReporter</c>. All reporter
        /// invocations are wrapped in try-catch; a single failure does not block the workflow.</para>
        /// </remarks>
        public TBootstrap UpdateReporter<T>() where T : Download.Reporting.IUpdateReporter, new()
        {
            _extensions[typeof(Download.Reporting.IUpdateReporter)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom OS-level strategy implementation (e.g.,
        /// <see cref="Strategy.WindowsStrategy"/>, <see cref="Strategy.LinuxStrategy"/>,
        /// <see cref="Strategy.MacStrategy"/>).
        /// </summary>
        /// <typeparam name="T">The strategy implementation type; must implement <see cref="IStrategy"/>
        /// and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Once registered, when <c>ClientStrategy.ResolveOsStrategy()</c> is called, the
        /// type registered here is used instead of auto-detecting the current operating system.
        /// This extension point takes effect when <see cref="LaunchAsync"/> executes.
        /// </remarks>
        public TBootstrap Strategy<T>() where T : IStrategy, new()
        {
            _extensions[typeof(IStrategy)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers update lifecycle hook implementations for injecting custom logic at key
        /// points in the update workflow.
        /// </summary>
        /// <typeparam name="T">The hook implementation type; must implement <c>IUpdateHooks</c>
        /// and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>Hook callbacks are invoked at the following points in the workflow:</para>
        /// <para>- <c>OnBeforeUpdateAsync</c>: Called before the download starts; can cancel the update.</para>
        /// <para>- <c>OnDownloadCompletedAsync</c>: Called after all files have been downloaded.</para>
        /// <para>- <c>OnAfterUpdateAsync</c>: Called after the upgrade packages have been applied.</para>
        /// <para>- <c>OnBeforeStartAppAsync</c>: Called before launching the upgrade process.</para>
        /// <para>- <c>OnUpdateErrorAsync</c>: Called when an exception occurs during the update.</para>
        /// <para>The default implementation is <c>NoOpUpdateHooks</c> (no-op). All hook invocations
        /// are wrapped in try-catch; a single hook failure does not block the workflow.</para>
        /// </remarks>
        public TBootstrap Hooks<T>() where T : Hooks.IUpdateHooks, new()
        {
            _extensions[typeof(Hooks.IUpdateHooks)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers an SSL validation policy implementation for customising HTTPS certificate
        /// validation behaviour.
        /// </summary>
        /// <typeparam name="T">The SSL policy implementation type; must implement
        /// <c>ISslValidationPolicy</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Can be used to bypass self-signed certificate validation, use custom root certificate
        /// authorities, or implement certificate pinning. This extension point is read by
        /// <c>HttpClientProvider</c> and applied to the <c>HttpClientHandler</c> when initiating
        /// HTTP download requests.
        /// </remarks>
        public TBootstrap SslPolicy<T>() where T : Security.ISslValidationPolicy, new()
        {
            _extensions[typeof(Security.ISslValidationPolicy)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom download retry/timeout policy for controlling how the system
        /// behaves when downloads fail.
        /// </summary>
        /// <typeparam name="T">The download policy implementation type; must implement
        /// <c>IDownloadPolicy</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Only takes effect when using the default download orchestrator
        /// (<c>DefaultDownloadOrchestrator</c>). If a custom orchestrator is also registered
        /// via <see cref="DownloadOrchestrator{T}"/>, this setting is ignored.
        /// The download policy determines the wait interval after each failure and the
        /// maximum retry count.
        /// </remarks>
        public TBootstrap DownloadPolicy<T>() where T : Download.Abstractions.IDownloadPolicy, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadPolicy)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom single-file download executor for supporting protocols other
        /// than HTTP/HTTPS.
        /// </summary>
        /// <typeparam name="T">The download executor implementation type; must implement
        /// <c>IDownloadExecutor</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Only takes effect when using the default download orchestrator
        /// (<c>DefaultDownloadOrchestrator</c>). If a custom orchestrator is also registered
        /// via <see cref="DownloadOrchestrator{T}"/>, this setting is ignored.
        /// Can be used to implement FTP, SFTP, or private-protocol file downloads.
        /// </remarks>
        public TBootstrap DownloadExecutor<T>() where T : Download.Abstractions.IDownloadExecutor, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadExecutor)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a download data source implementation for retrieving version information
        /// and update file manifests from the server.
        /// </summary>
        /// <typeparam name="T">The download source implementation type; must implement
        /// <c>IDownloadSource</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>Built-in implementations include <c>HttpDownloadSource</c> (HTTP/HTTPS) and
        /// SignalR Hub download sources.</para>
        /// <para>Use this method to register custom data sources, such as a local file system,
        /// FTP server, or private cloud storage for fetching update manifests.</para>
        /// <para>This extension point takes effect when
        /// <c>ClientStrategy.ExecuteStandardWorkflowAsync()</c> calls
        /// <c>downloadSource.ListAsync()</c>.</para>
        /// </remarks>
        public TBootstrap DownloadSource<T>() where T : Download.Abstractions.IDownloadSource, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadSource)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom post-download processing pipeline for performing hash verification,
        /// decryption, virus scanning, or other post-processing on downloaded files.
        /// </summary>
        /// <typeparam name="T">The download pipeline implementation type; must implement
        /// <c>IDownloadPipeline</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Only takes effect when using the default download orchestrator
        /// (<c>DefaultDownloadOrchestrator</c>). If a custom orchestrator is also registered
        /// via <see cref="DownloadOrchestrator{T}"/>, this setting is ignored.
        /// The download pipeline receives the path of each completed download and can perform
        /// integrity checks, decrypt encrypted files, or scan for malware.
        /// </remarks>
        public TBootstrap DownloadPipeline<T>() where T : Download.Abstractions.IDownloadPipeline, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadPipeline)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers an HTTP authentication provider for attaching authentication credentials
        /// to download requests.
        /// </summary>
        /// <typeparam name="T">The authentication provider implementation type; must implement
        /// <c>IHttpAuthProvider</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// Can be used to support servers that require token authentication (Bearer Token),
        /// basic authentication (Basic Auth), API key authentication, HMAC-SHA256 signature
        /// authentication, or custom authentication headers. This extension
        /// point is read by <c>HttpClientProvider</c> and injected into request headers when
        /// creating HTTP download requests.
        /// </remarks>
        public TBootstrap HttpAuth<T>() where T : Security.IHttpAuthProvider, new()
        {
            _extensions[typeof(Security.IHttpAuthProvider)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// [Obsolete] Use <see cref="HttpAuth{T}"/> instead.
        /// </summary>
        /// <typeparam name="T">The authentication provider implementation type.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        [Obsolete("Use HttpAuth<T>() instead.")]
        public TBootstrap UpdateAuth<T>() where T : Security.IHttpAuthProvider, new()
            => HttpAuth<T>();

        /// <summary>
        /// Registers a custom download orchestrator for end-to-end handling of batch
        /// download tasks.
        /// </summary>
        /// <typeparam name="T">The orchestrator implementation type; must implement
        /// <c>IDownloadOrchestrator</c> and have a parameterless constructor.</typeparam>
        /// <returns>The current <typeparamref name="TBootstrap"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>This is the highest-level download abstraction, owning full control of the
        /// download pipeline.</para>
        /// <para>When a custom orchestrator is set, registrations via
        /// <see cref="DownloadPolicy{T}"/>, <see cref="DownloadExecutor{T}"/>, and
        /// <see cref="DownloadPipeline{T}"/> are ignored, because the custom orchestrator
        /// assumes full control of the download workflow.</para>
        /// <para>Suitable for advanced scenarios requiring fully customised download behaviour
        /// (e.g., third-party download libraries, chunked download, multi-threaded download).</para>
        /// </remarks>
        public TBootstrap DownloadOrchestrator<T>() where T : Download.Abstractions.IDownloadOrchestrator, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadOrchestrator)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Resolves and instantiates an extension of the specified interface type using a
        /// two-phase lookup strategy.
        /// </summary>
        /// <typeparam name="TExtension">The extension interface type to resolve.</typeparam>
        /// <returns>The extension instance; <c>null</c> if no corresponding implementation
        /// type has been registered.</returns>
        /// <remarks>
        /// <para><b>Two-phase lookup:</b></para>
        /// <para>
        /// <b>Phase 1</b> — Looks up the registered implementation <c>Type</c> in the
        /// <c>_extensions</c> dictionary by interface type. If found, a new instance is
        /// created via <c>Activator.CreateInstance</c>.<br/>
        /// <b>Phase 2</b> — If not found in <c>_extensions</c>, looks up an existing
        /// singleton instance in the <c>_instances</c> dictionary.
        /// </para>
        /// <para>Singleton instances in <c>_instances</c> take precedence over lazy
        /// registrations in <c>_extensions</c>.</para>
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
        /// Resolves the registered implementation type for the specified extension interface
        /// without instantiating it.
        /// </summary>
        /// <typeparam name="TExtension">The extension interface type to resolve.</typeparam>
        /// <returns>The registered implementation type; <c>null</c> if not registered.</returns>
        /// <remarks>
        /// Unlike <see cref="ResolveExtension{TExtension}"/>, this method only returns
        /// type information. It is suitable for scenarios where reflection-based type
        /// metadata is needed without creating an instance — for example, checking
        /// whether an extension is registered in order to decide a workflow branch.
        /// </remarks>
        protected Type? ResolveExtensionType<TExtension>() where TExtension : class
        {
            return _extensions.TryGetValue(typeof(TExtension), out var t) ? t : null;
        }
    }
}
