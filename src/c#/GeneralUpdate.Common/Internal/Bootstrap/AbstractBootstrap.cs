using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using GeneralUpdate.Common.Internal.Strategy;

namespace GeneralUpdate.Common.Internal.Bootstrap
{
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
           where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
           where TStrategy : IStrategy
    {
        #region Private Members

        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> _options;
        private volatile Func<TStrategy> _strategyFactory;
        private IStrategy _strategy;

        #endregion Private Members

        #region Constructors

        protected internal AbstractBootstrap() => this._options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Launch udpate.
        /// </summary>
        /// <returns></returns>
        public virtual TBootstrap LaunchAsync()
        {
            return (TBootstrap)this;
        }

        #region Strategy

        protected IStrategy InitStrategy()
        {
            return _strategy;
        }

        protected string GetPlatform() => _strategy.GetPlatform();

        protected IStrategy ExecuteStrategy()
        {
            if (_strategy != null) _strategy.Execute();
            return _strategy;
        }

        public virtual TBootstrap Validate()
        {
            if (this._strategyFactory == null) throw new InvalidOperationException("Strategy or strategy factory not set.");
            return (TBootstrap)this;
        }

        public virtual TBootstrap Strategy<T>() where T : TStrategy, new() => this.StrategyFactory(() => new T());

        public TBootstrap StrategyFactory(Func<TStrategy> strategyFactory)
        {
            this._strategyFactory = strategyFactory;
            return (TBootstrap)this;
        }

        #endregion Strategy

        #region Config option.

        /// <summary>
        /// Files in the blacklist will skip the update.
        /// </summary>
        /// <param name="files">blacklist file name</param>
        /// <returns></returns>
        public virtual TBootstrap SetBlacklist(List<string> files = null, List<string> fileFormats = null)
        {
            return (TBootstrap)this;
        }

        /// <summary>
        /// Setting update configuration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option">Configuration Action Enumeration.</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public virtual TBootstrap Option<T>(UpdateOption<T> option, T value)
        {
            Contract.Requires(option != null);
            if (value == null)
            {
                this._options.TryRemove(option, out _);
            }
            else
            {
                this._options[option] = new UpdateOptionValue<T>(option, value);
            }
            return (TBootstrap)this;
        }

        public virtual T GetOption<T>(UpdateOption<T> option)
        {
            try
            {
                if (_options == null || _options.Count == 0) return default(T);
                var val = _options[option];
                if (val != null) return (T)val.GetValue();
                return default(T);
            }
            catch
            {
                return default(T);
            }
        }

        #endregion Config option.


        #endregion Methods
    }
}