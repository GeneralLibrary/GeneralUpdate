using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Text;

namespace GeneralUpdate.Core.Bootstrap
{
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
           where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
           where TStrategy : IStrategy
    {
        #region Private Members

        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> _options;
        private volatile Func<TStrategy> _strategyFactory;
        private Packet _packet;
        private IStrategy _strategy;
        protected const string DefaultFormat = "zip";

        public delegate void MutiAllDownloadCompletedEventHandler(object sender, MutiAllDownloadCompletedEventArgs e);

        public event MutiAllDownloadCompletedEventHandler MutiAllDownloadCompleted;

        public delegate void MutiDownloadProgressChangedEventHandler(object sender, MutiDownloadProgressChangedEventArgs e);

        public event MutiDownloadProgressChangedEventHandler MutiDownloadProgressChanged;

        public delegate void MutiAsyncCompletedEventHandler(object sender, MutiDownloadCompletedEventArgs e);

        public event MutiAsyncCompletedEventHandler MutiDownloadCompleted;

        public delegate void MutiDownloadErrorEventHandler(object sender, MutiDownloadErrorEventArgs e);

        public event MutiDownloadErrorEventHandler MutiDownloadError;

        public delegate void MutiDownloadStatisticsEventHandler(object sender, MutiDownloadStatisticsEventArgs e);

        public event MutiDownloadStatisticsEventHandler MutiDownloadStatistics;

        public delegate void ExceptionEventHandler(object sender, ExceptionEventArgs e);

        public event ExceptionEventHandler Exception;

        #endregion Private Members

        #region Constructors

        protected internal AbstractBootstrap() => this._options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();

        #endregion Constructors

        #region Public Properties

        public Packet Packet
        {
            get { return _packet ?? (_packet = new Packet()); }
            set { _packet = value; }
        }

        #endregion Public Properties

        #region Methods

        /// <summary>
        /// Launch udpate.
        /// </summary>
        /// <returns></returns>
        public virtual TBootstrap LaunchAsync()
        {
            try
            {
                Packet.Format = $".{GetOption(UpdateOption.Format) ?? DefaultFormat}";
                Packet.Encoding = GetOption(UpdateOption.Encoding) ?? Encoding.Default;
                Packet.DownloadTimeOut = GetOption(UpdateOption.DownloadTimeOut);
                Packet.AppName = Packet.AppName ?? GetOption(UpdateOption.MainApp);
                Packet.TempPath = $"{ FileUtil.GetTempDirectory(Packet.LastVersion) }\\";
                var manager = new DownloadManager<VersionInfo>(Packet.TempPath, Packet.Format, Packet.DownloadTimeOut);
                //manager.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
                //manager.MutiDownloadCompleted += OnMutiDownloadCompleted;
                //manager.MutiDownloadError += OnMutiDownloadError;
                //manager.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged;
                //manager.MutiDownloadStatistics += OnMutiDownloadStatistics;
                Packet.UpdateVersions.ForEach((v) => manager.Add(new DownloadTask<VersionInfo>(manager, v)));
                manager.LaunchTaskAsync();
            }
            catch (Exception ex)
            {
                this.Exception(this, new ExceptionEventArgs(ex));
                throw new Exception($"Launch error : { ex.Message }.");
            }
            return (TBootstrap)this;
        }

        #region Strategy

        protected IStrategy InitStrategy()
        {
            if (_strategy == null)
            {
                Validate();
                _strategy = _strategyFactory();
                Packet.Platform = _strategy.GetPlatform();
                _strategy.Create(Packet, MutiDownloadProgressAction, ExceptionAction);
            }
            return _strategy;
        }

        protected IStrategy ExcuteStrategy()
        {
            var strategy = InitStrategy();
            strategy.Excute();
            return strategy;
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
                this._options.TryRemove(option, out UpdateOptionValue removed);
            }
            else
            {
                this._options[option] = new UpdateOptionValue<T>(option, value);
            }
            return (TBootstrap)this;
        }

        public virtual T GetOption<T>(UpdateOption<T> option)
        {
            if (_options == null || _options.Count == 0) return default(T);
            var val = _options[option];
            if (val != null) return (T)val.GetValue();
            return default(T);
        }

        #endregion Config option.

        #region Callback event.

        private void OnMutiDownloadStatistics(object sender, MutiDownloadStatisticsEventArgs e)
        {
            if (MutiDownloadStatistics != null)
                MutiDownloadStatistics.Invoke(this, e);
        }

        protected void MutiDownloadProgressAction(object sender, MutiDownloadProgressChangedEventArgs e)
        {
            if (MutiDownloadProgressChanged != null)
                MutiDownloadProgressChanged.Invoke(sender, e);
        }

        protected void ExceptionAction(object sender, ExceptionEventArgs e)
        {
            if (Exception != null)  Exception.Invoke(this, e);
        }

        private void OnMutiDownloadProgressChanged(object sender, MutiDownloadProgressChangedEventArgs e)
        {
            if (MutiDownloadProgressChanged != null)
                MutiDownloadProgressChanged.Invoke(this, e);
        }

        private void OnMutiDownloadCompleted(object sender, MutiDownloadCompletedEventArgs e)
        {
            if (MutiDownloadCompleted != null)
                MutiDownloadCompleted.Invoke(sender, e);
        }

        private void OnMutiDownloadError(object sender, MutiDownloadErrorEventArgs e)
        {
            if (MutiDownloadError != null) MutiDownloadError.Invoke(this, e);
        }

        private void OnMutiAllDownloadCompleted(object sender, MutiAllDownloadCompletedEventArgs e)
        {
            try
            {
                if (MutiAllDownloadCompleted != null)
                    MutiAllDownloadCompleted.Invoke(this, e);
                ExcuteStrategy();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute strategy!", ex);
            }
        }

        #endregion Callback event.

        #endregion Methods
    }
}