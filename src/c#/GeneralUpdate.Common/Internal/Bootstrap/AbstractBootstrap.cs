using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Strategy;

namespace GeneralUpdate.Common.Internal.Bootstrap
{
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
           where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
           where TStrategy : IStrategy
    {
        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> _options;

        protected internal AbstractBootstrap() => 
            _options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();

        /// <summary>
        /// Launch async udpate.
        /// </summary>
        /// <returns></returns>
        public abstract Task<TBootstrap> LaunchAsync();

        protected abstract void ExecuteStrategy();
        
        protected abstract Task ExecuteStrategyAsync();

        protected abstract TBootstrap StrategyFactory();

        /// <summary>
        /// Setting update configuration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option">Configuration Action Enumeration.</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public TBootstrap Option<T>(UpdateOption<T> option, T value)
        {
            if (value == null)
            {
                _options.TryRemove(option, out _);
            }
            else
            {
                _options[option] = new UpdateOptionValue<T>(option, value);
            }
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
    }
}