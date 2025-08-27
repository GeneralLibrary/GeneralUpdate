using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Strategys;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.ClientCore;

/// <summary>
/// This component is used only for client application bootstrapping classes.
/// </summary>
public class GeneralClientBootstrap : AbstractBootstrap<GeneralClientBootstrap, IStrategy>
{
    /// <summary>
    /// All update actions of the core object for automatic upgrades will be related to the packet object.
    /// </summary>
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _strategy;
    private Func<bool>? _customSkipOption;
    private readonly List<Func<bool>> _customOptions = new();

    #region Public Methods

    /// <summary>
    /// Main function for booting the update startup.
    /// </summary>
    /// <returns></returns>
    public override async Task<GeneralClientBootstrap> LaunchAsync()
    {
        try
        {
            CallSmallBowlHome(_configInfo.Bowl);
            ExecuteCustomOptions();
            await ExecuteWorkflowAsync();
        }
        catch (Exception exception)
        {
            GeneralTracer.Error("The LaunchAsync method in the GeneralClientBootstrap class throws an exception." , exception);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(exception, exception.Message));
        }
        return this;
    }

    /// <summary>
    ///     Configure server address (Recommended Windows,Linux,Mac).
    /// </summary>
    public GeneralClientBootstrap SetConfig(Configinfo configInfo)
    {
        Debug.Assert(configInfo != null, "configInfo should not be null");
        configInfo?.Validate();
        _configInfo = new GlobalConfigInfo
        {
            AppName = configInfo.AppName,
            MainAppName = configInfo.MainAppName,
            ClientVersion = configInfo.ClientVersion,
            InstallPath = configInfo.InstallPath,
            UpdateLogUrl = configInfo.UpdateLogUrl,
            UpdateUrl = configInfo.UpdateUrl,
            ReportUrl = configInfo.ReportUrl,
            AppSecretKey = configInfo.AppSecretKey,
            BlackFormats = configInfo.BlackFormats,
            BlackFiles = configInfo.BlackFiles,
            ProductId = configInfo.ProductId,
            UpgradeClientVersion = configInfo.UpgradeClientVersion,
            Bowl = configInfo.Bowl,
            SkipDirectorys = configInfo.SkipDirectorys,
            Scheme = configInfo.Scheme,
            Token = configInfo.Token
        };
        return this;
    }

    /// <summary>
    ///     Let the user decide whether to update in the state of non-mandatory update.
    /// </summary>
    /// <param name="func">
    ///     Custom function ,Custom actions to let users decide whether to update. true update false do not
    ///     update .
    /// </param>
    /// <returns></returns>
    public GeneralClientBootstrap SetCustomSkipOption(Func<bool> func)
    {
        Debug.Assert(func != null);
        _customSkipOption = func;
        return this;
    }

    /// <summary>
    ///     Add an asynchronous custom operation.
    ///     In theory, any custom operation can be done. It is recommended to register the environment check method to ensure
    ///     that there are normal dependencies and environments after the update is completed.
    /// </summary>
    public GeneralClientBootstrap AddCustomOption(List<Func<bool>> funcList)
    {
        Debug.Assert(funcList != null && funcList.Any());
        _customOptions.AddRange(funcList);
        return this;
    }

    public GeneralClientBootstrap AddListenerMultiAllDownloadCompleted(
        Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadCompleted(
        Action<object, MultiDownloadCompletedEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadError(
        Action<object, MultiDownloadErrorEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadStatistics(
        Action<object, MultiDownloadStatisticsEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
        => AddListener(callbackAction);

    #endregion Public Methods

    #region Private Methods

    private async Task ExecuteWorkflowAsync()
    {
        try
        {
            Debug.Assert(_configInfo != null);
            //Request the upgrade information needed by the client and upgrade end, and determine if an upgrade is necessary.
            var mainResp = await VersionService.Validate(_configInfo.UpdateUrl
                , _configInfo.ClientVersion
                , AppType.ClientApp
                , _configInfo.AppSecretKey
                , GetPlatform()
                , _configInfo.ProductId
                , _configInfo.Scheme
                , _configInfo.Token);

            var upgradeResp = await VersionService.Validate(_configInfo.UpdateUrl
                , _configInfo.UpgradeClientVersion
                , AppType.UpgradeApp
                , _configInfo.AppSecretKey
                , GetPlatform()
                , _configInfo.ProductId
                , _configInfo.Scheme
                , _configInfo.Token);

            _configInfo.IsUpgradeUpdate = CheckUpgrade(upgradeResp);
            _configInfo.IsMainUpdate = CheckUpgrade(mainResp);

            //If the main program needs to be forced to update, the skip will not take effect.
            var isForcibly = CheckForcibly(mainResp.Body) || CheckForcibly(upgradeResp.Body);
            if (CanSkip(isForcibly)) return;

            //black list initialization.
            BlackListManager.Instance?.AddBlackFiles(_configInfo.BlackFiles);
            BlackListManager.Instance?.AddBlackFileFormats(_configInfo.BlackFormats);
            BlackListManager.Instance?.AddSkipDirectorys(_configInfo.SkipDirectorys);

            _configInfo.Encoding = GetOption(UpdateOption.Encoding) ?? Encoding.Default;
            _configInfo.Format = GetOption(UpdateOption.Format) ?? Format.ZIP;
            _configInfo.DownloadTimeOut = GetOption(UpdateOption.DownloadTimeOut) == 0
                ? 60
                : GetOption(UpdateOption.DownloadTimeOut);
            _configInfo.DriveEnabled = GetOption(UpdateOption.Drive) ?? false;
            _configInfo.PatchEnabled = GetOption(UpdateOption.Patch) ?? true;
            _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
            _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
                $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

            if (_configInfo.IsMainUpdate)
            {
                _configInfo.UpdateVersions = upgradeResp.Body.OrderBy(x => x.ReleaseDate).ToList();
                _configInfo.LastVersion = mainResp.Body.OrderBy(x => x.ReleaseDate).Last().Version;

                var failed = CheckFail(_configInfo.LastVersion);
                if (failed) return;

                //Initialize the process transfer parameter object.
                var processInfo = new ProcessInfo(_configInfo.MainAppName
                    , _configInfo.InstallPath
                    , _configInfo.ClientVersion
                    , _configInfo.LastVersion
                    , _configInfo.UpdateLogUrl
                    , _configInfo.Encoding
                    , _configInfo.Format
                    , _configInfo.DownloadTimeOut
                    , _configInfo.AppSecretKey
                    , mainResp.Body
                    , _configInfo.ReportUrl
                    , _configInfo.BackupDirectory
                    , _configInfo.Bowl
                    , _configInfo.Scheme
                    , _configInfo.Token
                    , BlackListManager.Instance.BlackFileFormats.ToList()
                    , BlackListManager.Instance.BlackFiles.ToList()
                    , BlackListManager.Instance.SkipDirectorys.ToList());

                _configInfo.ProcessInfo =
                    JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);
            }

            if (GetOption(UpdateOption.BackUp) ?? true)
            {
                StorageManager.Backup(_configInfo.InstallPath
                    , _configInfo.BackupDirectory
                    , BlackListManager.Instance.SkipDirectorys);
            }
            
            StrategyFactory();
            switch (_configInfo.IsUpgradeUpdate)
            {
                case true when _configInfo.IsMainUpdate:
                    //Both upgrade and main program update.
                    await Download();
                    await _strategy?.ExecuteAsync()!;
                    _strategy?.StartApp();
                    break;
                case true when !_configInfo.IsMainUpdate:
                    //Upgrade program update.
                    await Download();
                    await _strategy?.ExecuteAsync()!;
                    break;
                case false when _configInfo.IsMainUpdate:
                    //Main program update.
                    _strategy?.StartApp();
                    break;
            }
        }
        catch (Exception exception)
        {
            GeneralTracer.Error("The ExecuteWorkflowAsync method in the GeneralClientBootstrap class throws an exception." , exception);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(exception, exception.Message));
        }
    }

    private async Task Download()
    {
        var manager = new DownloadManager(_configInfo.TempPath, _configInfo.Format, _configInfo.DownloadTimeOut);
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

    private int GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return PlatformType.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return PlatformType.Linux;
        }

        return -1;
    }

    /// <summary>
    /// Check if there has been a recent update failure.(only windows)
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    private bool CheckFail(string version)
    {
        /*
          Read the version number of the last failed upgrade from the system environment variables, then compare it with the version number of the current request.
          If it is less than or equal to the failed version number, do not perform the update.
          */
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;

        var failVersion = new Version(fail);
        var lastVersion = new Version(version);
        return failVersion >= lastVersion;
    }

    /// <summary>
    /// Determine whether the current version verification result indicates that an update is needed.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    private bool CheckUpgrade(VersionRespDTO? response)
    {
        if (response == null)
            return false;

        if (response.Code == 200)
            return response.Body.Count > 0;

        return false;
    }

    /// <summary>
    /// During the iteration process, if any version requires a mandatory update, all the update content from this request should be updated.
    /// </summary>
    /// <param name="versions"></param>
    /// <returns></returns>
    private bool CheckForcibly(List<VersionInfo>? versions)
    {
        if (versions == null)
            return false;

        foreach (var item in versions)
        {
            if (item.IsForcibly == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// User decides if update is required.
    /// </summary>
    /// <returns>is false to continue execution.</returns>
    private bool CanSkip(bool isForcibly)
    {
        if (isForcibly) return false;
        return _customSkipOption?.Invoke() == true;
    }

    private void CallSmallBowlHome(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return;

        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                GeneralTracer.Info($"No process named {processName} found.");
                return;
            }

            foreach (var process in processes)
            {
                GeneralTracer.Info($"Killing process {process.ProcessName} (ID: {process.Id})");
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("The CallSmallBowlHome method in the GeneralClientBootstrap class throws an exception.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    /// <summary>
    /// Performs all injected custom operations.
    /// </summary>
    /// <returns></returns>
    private void ExecuteCustomOptions()
    {
        if (!_customOptions.Any()) return;

        foreach (var option in _customOptions)
        {
            if (!option.Invoke())
            {
                var exception = new Exception($"{nameof(option)}Execution failure!");
                var args = new ExceptionEventArgs(exception, exception.Message);
                GeneralTracer.Error("The ExecuteCustomOptions method in the GeneralClientBootstrap class throws an exception.", exception);
                EventManager.Instance.Dispatch(this, args);
            }
        }
    }

    protected override void ExecuteStrategy() => throw new NotImplementedException();

    protected override Task ExecuteStrategyAsync() => throw new NotImplementedException();

    protected override GeneralClientBootstrap StrategyFactory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _strategy = new WindowsStrategy();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _strategy = new LinuxStrategy();
        else
            throw new PlatformNotSupportedException("The current operating system is not supported!");

        _strategy?.Create(_configInfo!);
        return this;
    }

    private GeneralClientBootstrap AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
    {
        Debug.Assert(callbackAction != null);
        EventManager.Instance.AddListener(callbackAction);
        return this;
    }

    private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
    {
        var message = GetPacketHash(e.Version);
        GeneralTracer.Info($"Multi download statistics, {message}[BytesReceived]:{e.BytesReceived} [ProgressPercentage]:{e.ProgressPercentage} [Remaining]:{e.Remaining} [TotalBytesToReceive]:{e.TotalBytesToReceive} [Speed]:{e.Speed}");
        EventManager.Instance.Dispatch(sender, e);
    }

    private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
    {
        var message = GetPacketHash(e.Version);
        GeneralTracer.Info($"Multi download completed, {message}[IsComplated]:{e.IsComplated}.");
        EventManager.Instance.Dispatch(sender, e);
    }

    private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
    {
        var message = GetPacketHash(e.Version);
        GeneralTracer.Error($"Multi download error {message}.", e.Exception);
        EventManager.Instance.Dispatch(sender, e);
    }

    private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
    {
        GeneralTracer.Info($"Multi all download completed {e.IsAllDownloadCompleted}.");
        EventManager.Instance.Dispatch(sender, e);
    }

    #endregion Private Methods
}