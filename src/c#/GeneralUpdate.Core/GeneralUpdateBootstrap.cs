using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using GeneralUpdate.Common.Shared.Service;
using GeneralUpdate.Core.Strategys;

namespace GeneralUpdate.Core
{
    public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
    {
        private GlobalConfigInfo _configInfo = new();
        private IStrategy? _strategy;
        private Func<bool>? _customSkipOption;

        public GeneralUpdateBootstrap()
        {
            InitializeFromEnvironment();
        }

        #region Launch

        public override async Task<GeneralUpdateBootstrap> LaunchAsync()
        {
            GeneralTracer.Debug("GeneralUpdateBootstrap Launch.");
            StrategyFactory();

            switch (GetOption(UpdateOption.Mode) ?? UpdateMode.Default)
            {
                case UpdateMode.Default:
                    ApplyRuntimeOptions();
                    _strategy!.Create(_configInfo);
                    await DownloadAsync();
                    await _strategy.ExecuteAsync();
                    break;

                case UpdateMode.Scripts:
                    await ExecuteWorkflowAsync();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return this;
        }

        #endregion

        #region Configuration

        public GeneralUpdateBootstrap SetConfig(Configinfo configInfo)
        {
            _configInfo = new GlobalConfigInfo
            {
                MainAppName = configInfo.MainAppName,
                InstallPath = configInfo.InstallPath,
                ClientVersion = configInfo.ClientVersion,
                UpdateLogUrl = configInfo.UpdateLogUrl,
                AppSecretKey = configInfo.AppSecretKey,
                TempPath = StorageManager.GetTempDirectory("upgrade_temp"),
                ReportUrl = configInfo.ReportUrl,
                UpdateUrl = configInfo.UpdateUrl,
                Scheme = configInfo.Scheme,
                Token = configInfo.Token,
                DriveEnabled = GetOption(UpdateOption.Drive) ?? false,
                PatchEnabled = GetOption(UpdateOption.Patch) ?? true,
                Script = configInfo.Script
            };

            InitBlackList();
            return this;
        }

        public GeneralUpdateBootstrap SetFieldMappings(Dictionary<string, string> fieldMappings)
        {
            _configInfo.FieldMappings = fieldMappings;
            return this;
        }

        public GeneralUpdateBootstrap SetCustomSkipOption(Func<bool> func)
        {
            Debug.Assert(func != null);
            _customSkipOption = func;
            return this;
        }

        #endregion

        #region Workflow

        private async Task ExecuteWorkflowAsync()
        {
            try
            {
                var mainResp = await VersionService.Validate(
                    _configInfo.UpdateUrl,
                    _configInfo.ClientVersion,
                    AppType.ClientApp,
                    _configInfo.AppSecretKey,
                    GetPlatform(),
                    _configInfo.ProductId,
                    _configInfo.Scheme,
                    _configInfo.Token);

                _configInfo.IsMainUpdate = CheckUpgrade(mainResp);

                if (CanSkip(CheckForcibly(mainResp.Body)))
                    return;

                InitBlackList();
                ApplyRuntimeOptions();

                _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
                _configInfo.BackupDirectory = Path.Combine(
                    _configInfo.InstallPath,
                    $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

                _configInfo.UpdateVersions = mainResp.Body!
                    .OrderBy(x => x.ReleaseDate)
                    .ToList();

                if (GetOption(UpdateOption.BackUp) ?? true)
                {
                    StorageManager.Backup(
                        _configInfo.InstallPath,
                        _configInfo.BackupDirectory,
                        BlackListManager.Instance.SkipDirectorys);
                }

                _strategy!.Create(_configInfo);

                if (_configInfo.IsMainUpdate)
                {
                    await DownloadAsync();
                    await _strategy.ExecuteAsync();
                }
                else
                {
                    _strategy.StartApp();
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Error(
                    "The ExecuteWorkflowAsync method in the GeneralUpdateBootstrap class throws an exception.",
                    ex);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
            }
        }

        #endregion

        #region Download

        private async Task DownloadAsync()
        {
            var manager = new DownloadManager(
                _configInfo.TempPath,
                _configInfo.Format,
                _configInfo.DownloadTimeOut);

            manager.MultiAllDownloadCompleted += OnMultiAllDownloadCompleted;
            manager.MultiDownloadCompleted += OnMultiDownloadCompleted;
            manager.MultiDownloadError += OnMultiDownloadError;
            manager.MultiDownloadStatistics += OnMultiDownloadStatistics;

            foreach (var version in _configInfo.UpdateVersions)
                manager.Add(new DownloadTask(manager, version));

            await manager.LaunchTasksAsync();
        }

        #endregion

        #region Helpers

        private void InitializeFromEnvironment()
        {
            var json = Environments.GetEnvironmentVariable("ProcessInfo");
            if (string.IsNullOrWhiteSpace(json)) return;

            var processInfo = JsonSerializer.Deserialize(
                json,
                ProcessInfoJsonContext.Default.ProcessInfo);

            if (processInfo == null) return;

            BlackListManager.Instance.AddBlackFileFormats(processInfo.BlackFileFormats);
            BlackListManager.Instance.AddBlackFiles(processInfo.BlackFiles);
            BlackListManager.Instance.AddSkipDirectorys(processInfo.SkipDirectorys);

            _configInfo = new GlobalConfigInfo
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
                Token = processInfo.Token,
                DriveEnabled = GetOption(UpdateOption.Drive) ?? false,
                PatchEnabled = GetOption(UpdateOption.Patch) ?? true,
                Script = processInfo.Script
            };
        }

        private void ApplyRuntimeOptions()
        {
            _configInfo.Encoding = GetOption(UpdateOption.Encoding) ?? Encoding.Default;
            _configInfo.Format = GetOption(UpdateOption.Format) ?? Format.ZIP;
            _configInfo.DownloadTimeOut =
                GetOption(UpdateOption.DownloadTimeOut) == 0
                    ? 60
                    : GetOption(UpdateOption.DownloadTimeOut);
            _configInfo.DriveEnabled = GetOption(UpdateOption.Drive) ?? false;
            _configInfo.PatchEnabled = GetOption(UpdateOption.Patch) ?? true;
        }

        private void InitBlackList()
        {
            BlackListManager.Instance.AddBlackFiles(_configInfo.BlackFiles);
            BlackListManager.Instance.AddBlackFileFormats(_configInfo.BlackFormats);
            BlackListManager.Instance.AddSkipDirectorys(_configInfo.SkipDirectorys);
        }

        private bool CanSkip(bool isForcibly)
            => !isForcibly && _customSkipOption?.Invoke() == true;

        private static bool CheckUpgrade(VersionRespDTO? response)
            => response?.Code == 200 && response.Body?.Count > 0;

        private static bool CheckForcibly(IEnumerable<VersionInfo>? versions)
            => versions?.Any(v => v.IsForcibly == true) == true;

        private static int GetPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
            return -1;
        }

        #endregion

        #region Strategy & Events

        protected override GeneralUpdateBootstrap StrategyFactory()
        {
            _strategy = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new WindowsStrategy()
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? new LinuxStrategy()
                    : throw new PlatformNotSupportedException("The current operating system is not supported!");

            return this;
        }

        protected override Task ExecuteStrategyAsync() => throw new NotImplementedException();
        protected override void ExecuteStrategy() => throw new NotImplementedException();

        private GeneralUpdateBootstrap AddListener<TArgs>(Action<object, TArgs> action)
            where TArgs : EventArgs
        {
            EventManager.Instance.AddListener(action);
            return this;
        }

        public GeneralUpdateBootstrap AddListenerMultiAllDownloadCompleted(
            Action<object, MultiAllDownloadCompletedEventArgs> cb) => AddListener(cb);

        public GeneralUpdateBootstrap AddListenerMultiDownloadCompleted(
            Action<object, MultiDownloadCompletedEventArgs> cb) => AddListener(cb);

        public GeneralUpdateBootstrap AddListenerMultiDownloadError(
            Action<object, MultiDownloadErrorEventArgs> cb) => AddListener(cb);

        public GeneralUpdateBootstrap AddListenerMultiDownloadStatistics(
            Action<object, MultiDownloadStatisticsEventArgs> cb) => AddListener(cb);

        public GeneralUpdateBootstrap AddListenerException(
            Action<object, ExceptionEventArgs> cb) => AddListener(cb);

        private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        {
            GeneralTracer.Info(
                $"Multi download statistics, {ObjectTranslator.GetPacketHash(e.Version)} " +
                $"[BytesReceived]:{e.BytesReceived} [ProgressPercentage]:{e.ProgressPercentage} " +
                $"[Remaining]:{e.Remaining} [TotalBytesToReceive]:{e.TotalBytesToReceive} [Speed]:{e.Speed}");
            EventManager.Instance.Dispatch(sender, e);
        }

        private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        {
            GeneralTracer.Info(
                $"Multi download completed, {ObjectTranslator.GetPacketHash(e.Version)} [IsCompleted]:{e.IsComplated}");
            EventManager.Instance.Dispatch(sender, e);
        }

        private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            GeneralTracer.Error(
                $"Multi download error {ObjectTranslator.GetPacketHash(e.Version)}.",
                e.Exception);
            EventManager.Instance.Dispatch(sender, e);
        }

        private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        {
            GeneralTracer.Info($"Multi all download completed {e.IsAllDownloadCompleted}.");
            EventManager.Instance.Dispatch(sender, e);
        }

        #endregion
    }
}