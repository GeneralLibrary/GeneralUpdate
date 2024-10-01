using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Internal;
using GeneralUpdate.Core.Strategys;

namespace GeneralUpdate.Core
{
    public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
    {
        private Packet Packet { get; set; }

        private IStrategy _strategy;

        public GeneralUpdateBootstrap() : base()
        {
            try
            {
                //Gets values from system environment variables (ClientParameter object to base64 string).
                var json = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.User);
                var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json);
                Packet.AppType = AppType.UpgradeApp;
                Packet.TempPath = $"{GeneralFileManager.GetTempDirectory(processInfo.LastVersion)}{Path.DirectorySeparatorChar}";
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Client parameter json conversion failed, please check whether the parameter content is legal : {ex.Message},{ex.StackTrace}.");
            }
        }

        public override async Task<GeneralUpdateBootstrap> LaunchAsync()
        {
            var manager = new DownloadManager(Packet.InstallPath, Packet.Format, 30);
            
            foreach (var versionInfo in Packet.UpdateVersions) 
                manager.Add(new DownloadTask(manager, versionInfo));
            
            await manager.LaunchTasksAsync();
            return this;
        }

        #region public method

        public GeneralUpdateBootstrap AddListenerMultiAllDownloadCompleted(
            Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
        => AddListener(callbackAction);

        public GeneralUpdateBootstrap AddListenerMultiDownloadProgress(
            Action<object, MultiDownloadProgressChangedEventArgs> callbackAction)
        => AddListener(callbackAction);

        public GeneralUpdateBootstrap AddListenerMultiDownloadCompleted(
            Action<object, MultiDownloadCompletedEventArgs> callbackAction)
        => AddListener(callbackAction);

        public GeneralUpdateBootstrap AddListenerMultiDownloadError(
            Action<object, MultiDownloadErrorEventArgs> callbackAction)
        => AddListener(callbackAction);

        public GeneralUpdateBootstrap AddListenerMultiDownloadStatistics(
            Action<object, MultiDownloadStatisticsEventArgs> callbackAction)
        => AddListener(callbackAction);

        public GeneralUpdateBootstrap AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
        => AddListener(callbackAction);

        #endregion

        protected override void ExecuteStrategy()
        {
            _strategy.Create(Packet);
            _strategy.Execute();
        }

        protected override GeneralUpdateBootstrap StrategyFactory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _strategy = new WindowsStrategy();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                _strategy = new LinuxStrategy();
            else
                throw new PlatformNotSupportedException("The current operating system is not supported!");

            return this;
        }
        
        private GeneralUpdateBootstrap AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
        {
            Contract.Requires(callbackAction != null);
            EventManager.Instance.AddListener(callbackAction);
            return this;
        }

        private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        {
            EventManager.Instance.Dispatch(sender, e);
            ExecuteStrategy();
        }
    }
}