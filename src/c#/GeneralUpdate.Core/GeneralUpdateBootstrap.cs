using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Strategys;

namespace GeneralUpdate.Core
{
    public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
    {
        private readonly GlobalConfigInfo _configInfo;
        private IStrategy? _strategy;

        public GeneralUpdateBootstrap()
        {
            var json = Environments.GetEnvironmentVariable("ProcessInfo");
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("json environment variable is not defined");
                
            var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);
            if (processInfo == null)
                throw new ArgumentException("ProcessInfo object cannot be null!");
                
            BlackListManager.Instance?.AddBlackFileFormats(processInfo.BlackFileFormats);
            BlackListManager.Instance?.AddBlackFiles(processInfo.BlackFiles);
            BlackListManager.Instance?.AddSkipDirectorys(processInfo.SkipDirectorys);
            
            _configInfo = new()
            {
                MainAppName = processInfo.AppName,
                InstallPath = processInfo.InstallPath,
                ClientVersion = processInfo.CurrentVersion,
                LastVersion = processInfo.LastVersion,
                UpdateLogUrl = processInfo.UpdateLogUrl,
                Encoding = Encoding.GetEncoding(processInfo.CompressEncoding),
                Format = processInfo.CompressFormat,
                DownloadTimeOut = processInfo.DownloadTimeOut,
                AppSecretKey = processInfo.AppSecretKey,
                UpdateVersions = processInfo.UpdateVersions,
                TempPath = StorageManager.GetTempDirectory("upgrade_temp"),
                ReportUrl = processInfo.ReportUrl,
                BackupDirectory = processInfo.BackupDirectory,
                Scheme = processInfo.Scheme,
                Token = processInfo.Token
            };
        }

        public override async Task<GeneralUpdateBootstrap> LaunchAsync()
        {
            try
            {
                StrategyFactory();
                var manager =
                    new DownloadManager(_configInfo.TempPath, _configInfo.Format, _configInfo.DownloadTimeOut);
                manager.MultiAllDownloadCompleted += OnMultiAllDownloadCompleted;
                manager.MultiDownloadCompleted += OnMultiDownloadCompleted;
                manager.MultiDownloadError += OnMultiDownloadError;
                manager.MultiDownloadStatistics += OnMultiDownloadStatistics;
                foreach (var versionInfo in _configInfo.UpdateVersions)
                {
                    manager.Add(new DownloadTask(manager, versionInfo));
                }

                await manager.LaunchTasksAsync();
            }
            catch (Exception exception)
            {
                GeneralTracer.Error("The LaunchAsync method in the GeneralUpdateBootstrap class throws an exception.", exception);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(exception, exception.Message));
            }
            return this;
        }

        #region public method

        public GeneralUpdateBootstrap SetFieldMappings(Dictionary<string, string> fieldMappings)
        {
            _configInfo.FieldMappings = fieldMappings;
            return this;
        }

        public GeneralUpdateBootstrap AddListenerMultiAllDownloadCompleted(
            Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
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

        protected override Task ExecuteStrategyAsync()=> throw new NotImplementedException();
        
        protected override void ExecuteStrategy()
        {
            _strategy?.Create(_configInfo);
            _strategy?.Execute();
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
            Debug.Assert(callbackAction != null);
            EventManager.Instance.AddListener(callbackAction);
            return this;
        }

        private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

        private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        {
            GeneralTracer.Info($"Multi all download completed {e.IsAllDownloadCompleted}.");
            EventManager.Instance.Dispatch(sender, e);
            ExecuteStrategy();
        }
    }
}