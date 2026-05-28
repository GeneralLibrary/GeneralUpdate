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
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
        where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
        where TStrategy : IStrategy
    {
        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> _options;

        /// <summary>User-registered extension types for lazy instantiation.</summary>
        private readonly Dictionary<Type, Type> _extensions = new();

        /// <summary>Registered singleton instances (e.g., BlackListConfig).</summary>
        private readonly Dictionary<Type, object> _instances = new();

        protected internal AbstractBootstrap()
        {
            _options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();
        }

        public abstract Task<TBootstrap> LaunchAsync();

        public TBootstrap Option<T>(UpdateOption<T> option, T value)
        {
            if (value == null)
                _options.TryRemove(option, out _);
            else
                _options[option] = new UpdateOptionValue<T>(option, value);
            return (TBootstrap)this;
        }

        protected T GetOption<T>(UpdateOption<T>? option)
        {
            if (option == null) return default!;
            if (_options.TryGetValue(option, out var val) && val != null)
                return (T)val.GetValue();
            return option.DefaultValue;
        }

        // ═══════════ Extension point registration ═══════════

        public TBootstrap Strategy<T>() where T : IStrategy, new()
        {
            _extensions[typeof(IStrategy)] = typeof(T);
            return (TBootstrap)this;
        }

        public TBootstrap Hooks<T>() where T : Hooks.IUpdateHooks, new()
        {
            _extensions[typeof(Hooks.IUpdateHooks)] = typeof(T);
            return (TBootstrap)this;
        }

        public TBootstrap SslPolicy<T>() where T : Security.ISslValidationPolicy, new()
        {
            _extensions[typeof(Security.ISslValidationPolicy)] = typeof(T);
            return (TBootstrap)this;
        }
        
        /// <summary>
        /// Registers a custom retry/timeout policy for download operations.
        /// Only effective when using the default download orchestrator.
        /// If <see cref="DownloadOrchestrator{T}"/> is also set, this is ignored.
        /// </summary>
        public TBootstrap DownloadPolicy<T>() where T : Download.Abstractions.IDownloadPolicy, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadPolicy)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom single-file download executor (e.g. for non-HTTP protocols).
        /// Only effective when using the default download orchestrator.
        /// If <see cref="DownloadOrchestrator{T}"/> is also set, this is ignored.
        /// </summary>
        public TBootstrap DownloadExecutor<T>() where T : Download.Abstractions.IDownloadExecutor, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadExecutor)] = typeof(T);
            return (TBootstrap)this;
        }

        public TBootstrap DownloadSource<T>() where T : Download.Abstractions.IDownloadSource, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadSource)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom post-download processing pipeline (hash verification, decryption, virus scan).
        /// Only effective when using the default download orchestrator.
        /// If <see cref="DownloadOrchestrator{T}"/> is also set, this is ignored.
        /// </summary>
        public TBootstrap DownloadPipeline<T>() where T : Download.Abstractions.IDownloadPipeline, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadPipeline)] = typeof(T);
            return (TBootstrap)this;
        }

        public TBootstrap UpdateReporter<T>() where T : Download.Reporting.IUpdateReporter, new()
        {
            _extensions[typeof(Download.Reporting.IUpdateReporter)] = typeof(T);
            return (TBootstrap)this;
        }

        public TBootstrap UpdateAuth<T>() where T : Security.IHttpAuthProvider, new()
        {
            _extensions[typeof(Security.IHttpAuthProvider)] = typeof(T);
            return (TBootstrap)this;
        }

        /// <summary>
        /// Registers a custom download orchestrator that handles batch downloads end-to-end.
        /// This is the top-level download abstraction. When set, <see cref="DownloadPolicy{T}"/>,
        /// <see cref="DownloadExecutor{T}"/>, and <see cref="DownloadPipeline{T}"/> are ignored
        /// — the custom orchestrator owns the entire download pipeline.
        /// </summary>
        public TBootstrap DownloadOrchestrator<T>() where T : Download.Abstractions.IDownloadOrchestrator, new()
        {
            _extensions[typeof(Download.Abstractions.IDownloadOrchestrator)] = typeof(T);
            return (TBootstrap)this;
        }

        protected TExtension? ResolveExtension<TExtension>() where TExtension : class
        {
            if (_extensions.TryGetValue(typeof(TExtension), out var t))
                return Activator.CreateInstance((Type)t) as TExtension;
            if (_instances.TryGetValue(typeof(TExtension), out var instance))
                return instance as TExtension;
            return null;
        }

        /// <summary>Resolves the registered extension type without instantiating it.</summary>
        protected Type? ResolveExtensionType<TExtension>() where TExtension : class
        {
            return _extensions.TryGetValue(typeof(TExtension), out var t) ? t : null;
        }
    }
}