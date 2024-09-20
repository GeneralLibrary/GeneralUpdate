using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Strategy;

namespace GeneralUpdate.Common.Internal.Bootstrap
{
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
           where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
           where TStrategy : IStrategy
    {
        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> _options;

        protected internal AbstractBootstrap() => _options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();

        /// <summary>
        /// Launch async udpate.
        /// </summary>
        /// <returns></returns>
        public abstract Task<TBootstrap> LaunchAsync();

        protected abstract void ExecuteStrategy();

        protected abstract TBootstrap StrategyFactory();

        /// <summary>
        /// Setting update configuration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option">Configuration Action Enumeration.</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public virtual TBootstrap Option<T>(UpdateOption<T> option, T value)
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

        public virtual T? GetOption<T>(UpdateOption<T> option)
        {
            Contract.Requires(option != null);
            if (_options.Count == 0) return default(T);
            var val = _options[option];
            if (val != null) return (T)val.GetValue();
            return default(T);
        }
    }
}