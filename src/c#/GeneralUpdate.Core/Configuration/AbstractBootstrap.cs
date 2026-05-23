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

        protected internal AbstractBootstrap()
        {
            _options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();
            PopulateDefaults();
        }

        /// <summary>
        /// Populate all UpdateOptions with their best-practice defaults.
        /// Subclasses can override to customize.
        /// </summary>
        protected virtual void PopulateDefaults()
        {
            Option(UpdateOptions.MaxConcurrency, 3);
            Option(UpdateOptions.RetryCount, 3);
            Option(UpdateOptions.EnableResume, true);
            Option(UpdateOptions.VerifyChecksum, true);
            Option(UpdateOptions.SilentAutoInstall, false);
        }

        public abstract Task<TBootstrap> LaunchAsync();
        protected abstract void ExecuteStrategy();
        protected abstract Task ExecuteStrategyAsync();
        protected abstract TBootstrap StrategyFactory();

        /// <summary>
        /// Setting update configuration.
        /// </summary>
        public TBootstrap Option<T>(UpdateOption<T> option, T value)
        {
            if (value == null)
                _options.TryRemove(option, out _);
            else
                _options[option] = new UpdateOptionValue<T>(option, value);
            return (TBootstrap)this;
        }

        protected T? GetOption<T>(UpdateOption<T>? option)
        {
            try
            {
                Debug.Assert(option != null && _options.Count != 0);
                var val = _options[option];
                if (val != null) return (T)val.GetValue();
                return default;
            }
            catch
            {
                return default;
            }
        }

        // ═══════════ Extension point registration ═══════════

        /// <summary>Register a custom update strategy.</summary>
        public TBootstrap Strategy<T>() where T : IStrategy, new()
        { _extensions[typeof(IStrategy)] = typeof(T); return (TBootstrap)this; }

        /// <summary>Resolve a registered extension type, or null if not registered.</summary>
        protected TExtension? ResolveExtension<TExtension>() where TExtension : class
        {
            if (_extensions.TryGetValue(typeof(TExtension), out var t))
                return Activator.CreateInstance(t) as TExtension;
            return null;
        }
    }
}
