using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.DTO.Assembler;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Update;
using GeneralUpdate.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Bootstrap
{
    public abstract class AbstractBootstrap<TBootstrap, TStrategy>
           where TBootstrap : AbstractBootstrap<TBootstrap, TStrategy>
           where TStrategy : IStrategy
    {
        #region Private Members

        private readonly ConcurrentDictionary<UpdateOption, UpdateOptionValue> options;
        private volatile Func<TStrategy> strategyFactory;
        private PacketEntity _packet;
        private IStrategy strategy;
        private const string DefaultFormat = "zip";

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

        protected internal AbstractBootstrap() => this.options = new ConcurrentDictionary<UpdateOption, UpdateOptionValue>();

        #endregion Constructors

        #region Public Properties

        public PacketEntity Packet
        {
            get { return _packet ?? (_packet = new PacketEntity()); }
            set { _packet = value; }
        }

        #endregion Public Properties

        #region Methods

        /// <summary>
        /// Launch udpate.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<TBootstrap> LaunchTaskAsync()
        {
            try
            {
                MutiDownloadProgressChanged.Invoke(this,
                    new MutiDownloadProgressChangedEventArgs(null, ProgressType.Check, "Update checking..."));
                await ValidationVersion();
                Packet.Format = $".{GetOption(UpdateOption.Format) ?? DefaultFormat}";
                Packet.Encoding = GetOption(UpdateOption.Encoding) ?? Encoding.Default;
                Packet.DownloadTimeOut = GetOption(UpdateOption.DownloadTimeOut);
                Packet.AppName = Packet.AppName ?? GetOption(UpdateOption.MainApp);
                Packet.TempPath = $"{ FileUtil.GetTempDirectory(Packet.LastVersion) }\\";
                var manager = new DownloadManager<VersionEntity>(Packet.TempPath, Packet.Format, Packet.DownloadTimeOut);
                manager.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
                manager.MutiDownloadCompleted += OnMutiDownloadCompleted;
                manager.MutiDownloadError += OnMutiDownloadError;
                manager.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged;
                manager.MutiDownloadStatistics += OnMutiDownloadStatistics;
                Packet.UpdateVersions.ForEach((v) => manager.Add(new DownloadTask<VersionEntity>(manager, v)));
                manager.LaunchTaskAsync();
            }
            catch (Exception ex)
            {
                this.Exception(this, new ExceptionEventArgs(ex));
                throw new Exception($"Launch error : { ex.Message }.");
            }
            return (TBootstrap)this;
        }

        public virtual void Launch0() 
        {
            ExcuteStrategy();
        }

        private async Task ValidationVersion() 
        {
            var url = Packet.AppType == AppType.ClientApp ? Packet.MainUpdateUrl : Packet.UpdateUrl;
            var updateResp = await HttpUtil.GetTaskAsync<VersionRespDTO>(url);
            if (updateResp == null) throw new NullReferenceException(nameof(updateResp));
            if (updateResp.Code != HttpStatus.OK) throw new Exception($"Request failed , Code :{updateResp.Code}, Message:{updateResp.Message} !");
            if (updateResp.Code == HttpStatus.OK)
            {
                var body = updateResp.Body;
                //If it is empty and the status code is OK, no update is required.
                if (body == null) return;
                Packet.UpdateVersions = VersionAssembler.ToEntitys(body.Versions);
                if (Packet.UpdateVersions != null && Packet.UpdateVersions.Count != 0)
                    Packet.LastVersion = Packet.UpdateVersions[Packet.UpdateVersions.Count - 1].Version;
            }
            else
            {
                MutiDownloadProgressChanged.Invoke(this,
                    new MutiDownloadProgressChangedEventArgs(null, ProgressType.Check, $"Check update failed : code {updateResp.Code} , meesage {updateResp.Message} !"));
            }
        }

        #region Strategy

        protected IStrategy InitStrategy()
        {
            if (strategy == null)
            {
                Validate();
                strategy = this.strategyFactory();
                strategy.Create(Packet, MutiDownloadProgressAction, ExceptionAction);
            }
            return strategy;
        }

        protected IStrategy ExcuteStrategy()
        {
            var strategy = InitStrategy();
            strategy.Excute();
            return strategy;
        }

        public virtual TBootstrap Validate()
        {
            if (this.strategyFactory == null) throw new InvalidOperationException("Strategy or strategy factory not set.");
            return (TBootstrap)this;
        }

        public virtual TBootstrap Strategy<T>() where T : TStrategy, new() => this.StrategyFactory(() => new T());

        public TBootstrap StrategyFactory(Func<TStrategy> strategyFactory)
        {
            this.strategyFactory = strategyFactory;
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
                this.options.TryRemove(option, out UpdateOptionValue removed);
            }
            else
            {
                this.options[option] = new UpdateOptionValue<T>(option, value);
            }
            return (TBootstrap)this;
        }

        public virtual T GetOption<T>(UpdateOption<T> option)
        {
            if (options == null || options.Count == 0) return default(T);
            var val = options[option];
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