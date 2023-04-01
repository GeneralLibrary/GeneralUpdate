using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MutiEventArgs;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
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
        private const string EXECUTABLE_FILE = ".exe";

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
                InitStrategy();
                //When the upgrade stops and does not need to be updated, the client needs to be updated. Start the upgrade assistant directly.
                if (!Packet.IsUpgradeUpdate && Packet.IsMainUpdate) _strategy.StartApp(Packet.AppName, Packet.AppType);
                Packet.Format = $".{GetOption(UpdateOption.Format) ?? Format.ZIP}";
                Packet.Encoding = GetOption(UpdateOption.Encoding) ?? Encoding.Default;
                Packet.DownloadTimeOut = GetOption(UpdateOption.DownloadTimeOut);
                Packet.AppName = $"{Packet.AppName ?? GetOption(UpdateOption.MainApp)}{EXECUTABLE_FILE}";
                Packet.TempPath = $"{FileUtil.GetTempDirectory(Packet.LastVersion)}{Path.DirectorySeparatorChar}";
                var manager = new DownloadManager<VersionInfo>(Packet.TempPath, Packet.Format, Packet.DownloadTimeOut);
                manager.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
                manager.MutiDownloadCompleted += OnMutiDownloadCompleted;
                manager.MutiDownloadError += OnMutiDownloadError;
                manager.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged;
                manager.MutiDownloadStatistics += OnMutiDownloadStatistics;
                Packet.UpdateVersions.ForEach((v) => manager.Add(new DownloadTask<VersionInfo>(manager, v)));
                manager.LaunchTaskAsync();
            }
            catch (Exception ex)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(ex));
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
                _strategy.Create(Packet);
            }
            return _strategy;
        }

        protected string GetPlatform()
        {
            return _strategy.GetPlatform();
        }

        protected IStrategy ExcuteStrategy()
        {
            if (_strategy != null) _strategy.Excute();
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
            Packet.BlackFiles = files ?? new List<string>() { "Newtonsoft.Json.dll" };
            Packet.BlackFormats = fileFormats ?? new List<string>() { ".patch", ".7z", ".zip", ".rar", ".tar", ".json" };
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

        public TBootstrap AddListenerMutiAllDownloadCompleted(Action<object, MutiAllDownloadCompletedEventArgs> callbackAction)
        {
            return AddListener(callbackAction);
        }

        public TBootstrap AddListenerMutiDownloadProgress(Action<object, MutiDownloadProgressChangedEventArgs> callbackAction)
        {
            return AddListener(callbackAction);
        }

        public TBootstrap AddListenerMutiDownloadCompleted(Action<object, MutiDownloadCompletedEventArgs> callbackAction)
        {
            return AddListener(callbackAction);
        }

        public TBootstrap AddListenerMutiDownloadError(Action<object, MutiDownloadErrorEventArgs> callbackAction)
        {
            return AddListener(callbackAction);
        }

        public TBootstrap AddListenerMutiDownloadStatistics(Action<object, MutiDownloadStatisticsEventArgs> callbackAction)
        {
            return AddListener(callbackAction);
        }

        public TBootstrap AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
        {
            return AddListener(callbackAction);
        }

        protected TBootstrap AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
        {
            if (callbackAction != null) EventManager.Instance.AddListener(callbackAction);
            return (TBootstrap)this;
        }

        private void OnMutiDownloadStatistics(object sender, MutiDownloadStatisticsEventArgs e)
        => EventManager.Instance.Dispatch<Action<object, MutiDownloadStatisticsEventArgs>>(sender, e);

        private void OnMutiDownloadProgressChanged(object sender, MutiDownloadProgressChangedEventArgs e)
        => EventManager.Instance.Dispatch<Action<object, MutiDownloadProgressChangedEventArgs>>(sender, e);

        private void OnMutiDownloadCompleted(object sender, MutiDownloadCompletedEventArgs e)
        => EventManager.Instance.Dispatch<Action<object, MutiDownloadCompletedEventArgs>>(sender, e);

        private void OnMutiDownloadError(object sender, MutiDownloadErrorEventArgs e)
        => EventManager.Instance.Dispatch<Action<object, MutiDownloadErrorEventArgs>>(sender, e);

        private void OnMutiAllDownloadCompleted(object sender, MutiAllDownloadCompletedEventArgs e)
        {
            try
            {
                EventManager.Instance.Dispatch<Action<object, MutiAllDownloadCompletedEventArgs>>(sender, e);
                ExcuteStrategy();
            }
            catch (Exception ex)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(ex));
            }
        }

        #endregion Callback event.

        #endregion Methods
    }
}