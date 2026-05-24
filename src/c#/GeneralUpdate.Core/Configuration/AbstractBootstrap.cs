using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Core.Strategy;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;

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
        protected abstract void ExecuteStrategy();
        protected abstract Task ExecuteStrategyAsync();
        protected abstract TBootstrap StrategyFactory();

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
        { _extensions[typeof(IStrategy)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap Hooks<T>() where T : Hooks.IUpdateHooks, new()
        { _extensions[typeof(Hooks.IUpdateHooks)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap SslPolicy<T>() where T : Security.ISslValidationPolicy, new()
        { _extensions[typeof(Security.ISslValidationPolicy)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap BinaryDiffer<T>() where T : Differential.IBinaryDiffer, new()
        { _extensions[typeof(Differential.IBinaryDiffer)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap PipelineFactory<T>() where T : Pipeline.IUpdatePipelineFactory, new()
        { _extensions[typeof(Pipeline.IUpdatePipelineFactory)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap DownloadPolicy<T>() where T : Download.Abstractions.IDownloadPolicy, new()
        { _extensions[typeof(Download.Abstractions.IDownloadPolicy)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap DownloadExecutor<T>() where T : Download.Abstractions.IDownloadExecutor, new()
        { _extensions[typeof(Download.Abstractions.IDownloadExecutor)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap DownloadSource<T>() where T : Download.Abstractions.IDownloadSource, new()
        { _extensions[typeof(Download.Abstractions.IDownloadSource)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap DownloadPipeline<T>() where T : Download.Abstractions.IDownloadPipeline, new()
        { _extensions[typeof(Download.Abstractions.IDownloadPipeline)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap UpdateReporter<T>() where T : Download.Reporting.IUpdateReporter, new()
        { _extensions[typeof(Download.Reporting.IUpdateReporter)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap UpdateAuth<T>() where T : Security.IHttpAuthProvider, new()
        { _extensions[typeof(Security.IHttpAuthProvider)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap DownloadOrchestrator<T>() where T : Download.Abstractions.IDownloadOrchestrator, new()
        { _extensions[typeof(Download.Abstractions.IDownloadOrchestrator)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap CleanStrategy<T>() where T : Differential.ICleanStrategy, new()
        { _extensions[typeof(Differential.ICleanStrategy)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap DirtyStrategy<T>() where T : Differential.IDirtyStrategy, new()
        { _extensions[typeof(Differential.IDirtyStrategy)] = typeof(T); return (TBootstrap)this; }

        public TBootstrap ConfigureBlackList(BlackListConfig config)
        {
            _instances[typeof(BlackListConfig)] = config ?? BlackListConfig.Empty;
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
    }
}
