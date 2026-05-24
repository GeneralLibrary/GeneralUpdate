using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Strategy;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core;

/// <summary>
/// Unified update bootstrap — single entry point for both Client and Upgrade roles.
/// Use <see cref="AppType"/> to select the workflow:
/// <list type="bullet">
///   <item><see cref="AppType.ClientApp"/> — validate versions, download, start upgrade process</item>
///   <item><see cref="AppType.UpgradeApp"/> — receive ProcessInfo, apply updates, start main app</item>
/// </list>
/// </summary>
/// <remarks>
/// <b>Migration from GeneralClientBootstrap:</b>
/// Replace <c>new GeneralClientBootstrap().SetConfig(cfg).LaunchAsync()</c> with
/// <c>new GeneralUpdateBootstrap().Option(UpdateOptions.AppType, AppType.ClientApp).SetConfig(cfg).LaunchAsync()</c>.
/// </remarks>
public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
{
    private GlobalConfigInfo _configInfo = new();
    private IStrategy? _strategy;
    private Func<bool>? _customSkipOption;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private readonly List<Func<bool>> _customOptions = new();

    public GeneralUpdateBootstrap()
    {
        InitializeFromEnvironment();
    }

    // ════════════════════════════════════════════════════════════════
    // Launch — AppType dispatch
    // ════════════════════════════════════════════════════════════════

    public override async Task<GeneralUpdateBootstrap> LaunchAsync()
    {
        int appType = GetOption(UpdateOptions.AppType);
        return appType switch
        {
            AppType.ClientApp  => await LaunchClientAsync(),
            AppType.UpgradeApp => await LaunchUpgradeAsync(),
            _ => await LaunchClientAsync()  // default to Client for backward compatibility
        };
    }

    /// <summary>Client workflow: validate versions, download, start upgrade process.</summary>
    private async Task<GeneralUpdateBootstrap> LaunchClientAsync()
    {
        try
        {
            GeneralTracer.Debug("GeneralUpdateBootstrap.LaunchClientAsync start.");
            CallSmallBowlHome(_configInfo.Bowl);
            ExecuteCustomOptions();
            await ExecuteClientWorkflowAsync();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("LaunchClientAsync threw an exception.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
        return this;
    }

    /// <summary>Upgrade workflow: receive ProcessInfo, apply updates, start main app.</summary>
    private async Task<GeneralUpdateBootstrap> LaunchUpgradeAsync()
    {
        GeneralTracer.Debug("GeneralUpdateBootstrap.LaunchUpgradeAsync start.");
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
                await ExecuteUpgradeWorkflowAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return this;
    }

    // ════════════════════════════════════════════════════════════════
    // Configuration
    // ════════════════════════════════════════════════════════════════

    public GeneralUpdateBootstrap SetConfig(Configinfo configInfo)
    {
        _configInfo = ConfigurationMapper.MapToGlobalConfigInfo(configInfo);

        var appType = GetOption(UpdateOptions.AppType);
        if (appType != AppType.UpgradeApp)
        {
            _configInfo.TempPath = StorageManager.GetTempDirectory("upgrade_temp");
            _configInfo.DriveEnabled = GetOption(UpdateOption.Drive) ?? false;
            _configInfo.PatchEnabled = GetOption(UpdateOption.Patch) ?? true;
            InitBlackList();
        }

        return this;
    }

    public GeneralUpdateBootstrap SetCustomSkipOption(Func<bool>? func)
    {
        _customSkipOption = func;
        return this;
    }

    /// <summary>
    /// Registers a callback invoked when update information is available.
    /// Returns <c>true</c> to skip the update; <c>false</c> to proceed.
    /// Forced-update protection still applies — the callback return value is
    /// ignored if any version is marked as forcibly required.
    /// </summary>
    public GeneralUpdateBootstrap AddListenerUpdatePrecheck(Func<UpdateInfoEventArgs, bool> func)
    {
        _updatePrecheck = func ?? throw new ArgumentNullException(nameof(func));
        return this;
    }

    /// <summary>
    /// Add custom operations to execute before the update workflow.
    /// Recommended for environment checks to ensure dependencies are available after update.
    /// </summary>
    public GeneralUpdateBootstrap AddCustomOption(List<Func<bool>> funcList)
    {
        Debug.Assert(funcList != null && funcList.Any());
        _customOptions.AddRange(funcList);
        return this;
    }

    // ════════════════════════════════════════════════════════════════
    // Client Workflow
    // ════════════════════════════════════════════════════════════════

    private async Task ExecuteClientWorkflowAsync()
    {
        try
        {
            Debug.Assert(_configInfo != null);

            // Silent mode
            if (GetOption(UpdateOption.EnableSilentUpdate))
            {
                GeneralTracer.Info("GeneralUpdateBootstrap.ExecuteClientWorkflowAsync: silent mode, delegating to SilentUpdateMode.");
                await new SilentUpdateMode(
                    _configInfo,
                    GetOption(UpdateOption.Encoding) ?? Encoding.Default,
                    GetOption(UpdateOption.Format) ?? Format.ZIP,
                    GetOption(UpdateOption.DownloadTimeOut) ?? 60,
                    GetOption(UpdateOption.Patch) ?? true,
                    GetOption(UpdateOption.BackUp) ?? true).StartAsync();
                return;
            }

            // Dual version validation
            GeneralTracer.Info($"GeneralUpdateBootstrap.ExecuteClientWorkflowAsync: validating client={_configInfo.ClientVersion}, upgrade={_configInfo.UpgradeClientVersion}");
            var mainResp = await VersionService.Validate(_configInfo.UpdateUrl
                , _configInfo.ClientVersion, AppType.ClientApp, _configInfo.AppSecretKey
                , GetPlatform(), _configInfo.ProductId, _configInfo.Scheme, _configInfo.Token);

            var upgradeResp = await VersionService.Validate(_configInfo.UpdateUrl
                , _configInfo.UpgradeClientVersion, AppType.UpgradeApp, _configInfo.AppSecretKey
                , GetPlatform(), _configInfo.ProductId, _configInfo.Scheme, _configInfo.Token);

            _configInfo.IsUpgradeUpdate = CheckUpgrade(upgradeResp);
            _configInfo.IsMainUpdate = CheckUpgrade(mainResp);
            GeneralTracer.Info($"ExecuteClientWorkflowAsync: IsMainUpdate={_configInfo.IsMainUpdate}, IsUpgradeUpdate={_configInfo.IsUpgradeUpdate}");

            var updateInfoArgs = new UpdateInfoEventArgs(mainResp);
            EventManager.Instance.Dispatch(this, updateInfoArgs);

            var isForcibly = CheckForcibly(mainResp.Body) || CheckForcibly(upgradeResp.Body);
            if (CanSkipClient(isForcibly, updateInfoArgs))
            {
                GeneralTracer.Info("ExecuteClientWorkflowAsync: update skipped by precheck callback.");
                return;
            }

            InitBlackList();
            ApplyRuntimeOptions();

            _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
            _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
                $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

            _configInfo.UpdateVersions = _configInfo.IsUpgradeUpdate
                ? upgradeResp.Body.OrderBy(x => x.ReleaseDate).ToList()
                : new List<VersionInfo>();

            if (_configInfo.IsMainUpdate)
            {
                _configInfo.LastVersion = mainResp.Body.OrderBy(x => x.ReleaseDate).Last().Version;
                GeneralTracer.Info($"ExecuteClientWorkflowAsync: main update, LastVersion={_configInfo.LastVersion}");

                var failed = CheckFail(_configInfo.LastVersion);
                if (failed)
                {
                    GeneralTracer.Warn($"ExecuteClientWorkflowAsync: version {_configInfo.LastVersion} matches known-failed upgrade, aborting.");
                    return;
                }

                var processInfo = ConfigurationMapper.MapToProcessInfo(
                    _configInfo, mainResp.Body,
                    BlackListManager.Instance.BlackFormats.ToList(),
                    BlackListManager.Instance.BlackFiles.ToList(),
                    BlackListManager.Instance.SkipDirectorys.ToList());

                _configInfo.ProcessInfo = JsonSerializer.Serialize(
                    processInfo, ProcessInfoJsonContext.Default.ProcessInfo);
            }

            if (GetOption(UpdateOption.BackUp) ?? true)
            {
                GeneralTracer.Info($"ExecuteClientWorkflowAsync: backing up {_configInfo.InstallPath} -> {_configInfo.BackupDirectory}");
                StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
                    BlackListManager.Instance.SkipDirectorys);
            }

            StrategyFactory();
            GeneralTracer.Info($"ExecuteClientWorkflowAsync: IsUpgradeUpdate={_configInfo.IsUpgradeUpdate}, IsMainUpdate={_configInfo.IsMainUpdate}");

            switch (_configInfo.IsUpgradeUpdate)
            {
                case true when _configInfo.IsMainUpdate:
                    GeneralTracer.Info("ExecuteClientWorkflowAsync: both upgrade+main — downloading and executing.");
                    await DownloadAsync();
                    await _strategy!.ExecuteAsync();
                    _strategy.StartApp();
                    break;
                case true when !_configInfo.IsMainUpdate:
                    GeneralTracer.Info("ExecuteClientWorkflowAsync: upgrade-only — downloading and executing.");
                    await DownloadAsync();
                    await _strategy!.ExecuteAsync();
                    break;
                case false when _configInfo.IsMainUpdate:
                    GeneralTracer.Info("ExecuteClientWorkflowAsync: main-only — starting updater.");
                    _strategy!.StartApp();
                    break;
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("ExecuteClientWorkflowAsync threw an exception.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Upgrade Workflow
    // ════════════════════════════════════════════════════════════════

    private async Task ExecuteUpgradeWorkflowAsync()
    {
        try
        {
            GeneralTracer.Info($"GeneralUpdateBootstrap.ExecuteUpgradeWorkflowAsync: validating version. UpdateUrl={_configInfo.UpdateUrl}, ClientVersion={_configInfo.ClientVersion}");
            var mainResp = await VersionService.Validate(
                _configInfo.UpdateUrl, _configInfo.ClientVersion,
                AppType.ClientApp, _configInfo.AppSecretKey,
                GetPlatform(), _configInfo.ProductId,
                _configInfo.Scheme, _configInfo.Token);

            _configInfo.IsMainUpdate = CheckUpgrade(mainResp);
            GeneralTracer.Info($"ExecuteUpgradeWorkflowAsync: IsMainUpdate={_configInfo.IsMainUpdate}");

            EventManager.Instance.Dispatch(this, new UpdateInfoEventArgs(mainResp));

            if (CanSkip(CheckForcibly(mainResp.Body)))
            {
                GeneralTracer.Info("ExecuteUpgradeWorkflowAsync: update skipped.");
                return;
            }

            InitBlackList();
            ApplyRuntimeOptions();

            _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
            _configInfo.BackupDirectory = Path.Combine(
                _configInfo.InstallPath,
                $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

            _configInfo.UpdateVersions = mainResp.Body!
                .OrderBy(x => x.ReleaseDate).ToList();

            GeneralTracer.Info($"ExecuteUpgradeWorkflowAsync: {_configInfo.UpdateVersions.Count} version(s) queued.");

            if (GetOption(UpdateOption.BackUp) ?? true)
            {
                GeneralTracer.Info($"ExecuteUpgradeWorkflowAsync: backing up {_configInfo.InstallPath} -> {_configInfo.BackupDirectory}");
                StorageManager.Backup(
                    _configInfo.InstallPath, _configInfo.BackupDirectory,
                    BlackListManager.Instance.SkipDirectorys);
            }

            _strategy!.Create(_configInfo);

            if (_configInfo.IsMainUpdate)
            {
                GeneralTracer.Info("ExecuteUpgradeWorkflowAsync: main update required, starting download and execution.");
                await DownloadAsync();
                await _strategy.ExecuteAsync();
            }
            else
            {
                GeneralTracer.Info("ExecuteUpgradeWorkflowAsync: no update needed, starting application.");
                _strategy.StartApp();
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("ExecuteUpgradeWorkflowAsync threw an exception.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Download
    // ════════════════════════════════════════════════════════════════

    private async Task DownloadAsync()
    {
        var manager = new DownloadManager(
            _configInfo.TempPath, _configInfo.Format, _configInfo.DownloadTimeOut);

        manager.MultiAllDownloadCompleted += OnMultiAllDownloadCompleted;
        manager.MultiDownloadCompleted += OnMultiDownloadCompleted;
        manager.MultiDownloadError += OnMultiDownloadError;
        manager.MultiDownloadStatistics += OnMultiDownloadStatistics;

        foreach (var version in _configInfo.UpdateVersions)
            manager.Add(new DownloadTask(manager, version));

        await manager.LaunchTasksAsync();
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    private void InitializeFromEnvironment()
    {
        var json = Environments.GetEnvironmentVariable("ProcessInfo");
        if (string.IsNullOrWhiteSpace(json)) return;

        var processInfo = JsonSerializer.Deserialize(
            json, ProcessInfoJsonContext.Default.ProcessInfo);
        if (processInfo == null) return;

        BlackListManager.Instance.AddBlackFormats(processInfo.BlackFileFormats);
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
            Script = processInfo.Script,
            DriverDirectory = processInfo.DriverDirectory
        };
    }

    private void ApplyRuntimeOptions()
    {
        _configInfo.Encoding = GetOption(UpdateOption.Encoding) ?? Encoding.Default;
        _configInfo.Format = GetOption(UpdateOption.Format) ?? Format.ZIP;
        _configInfo.DownloadTimeOut = GetOption(UpdateOption.DownloadTimeOut) ?? 60;
        _configInfo.DriveEnabled = GetOption(UpdateOption.Drive) ?? false;
        _configInfo.PatchEnabled = GetOption(UpdateOption.Patch) ?? true;
    }

    private void InitBlackList()
    {
        BlackListManager.Instance.AddBlackFiles(_configInfo.BlackFiles);
        BlackListManager.Instance.AddBlackFormats(_configInfo.BlackFormats);
        BlackListManager.Instance.AddSkipDirectorys(_configInfo.SkipDirectorys);
    }

    private bool CanSkip(bool isForcibly)
    {
        if (isForcibly) return false;
        return _customSkipOption?.Invoke() == true;
    }

    private bool CanSkipClient(bool isForcibly, UpdateInfoEventArgs updateInfo)
    {
        if (isForcibly) return false;
        return _updatePrecheck?.Invoke(updateInfo) == true;
    }

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

    /// <summary>Check if the target version matches a known-failed upgrade.</summary>
    private bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;

        var failVersion = new Version(fail);
        var lastVersion = new Version(version);
        return failVersion >= lastVersion;
    }

    /// <summary>Kill existing Bowl watchdog processes before update.</summary>
    private void CallSmallBowlHome(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;

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
            GeneralTracer.Error("CallSmallBowlHome threw an exception.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    /// <summary>Execute all registered custom pre-update operations.</summary>
    private void ExecuteCustomOptions()
    {
        if (!_customOptions.Any()) return;

        foreach (var option in _customOptions)
        {
            if (!option.Invoke())
            {
                var exception = new Exception($"{nameof(option)} execution failure!");
                GeneralTracer.Error("ExecuteCustomOptions failed.", exception);
                EventManager.Instance.Dispatch(this,
                    new ExceptionEventArgs(exception, exception.Message));
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Strategy & Events
    // ════════════════════════════════════════════════════════════════

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

    private GeneralUpdateBootstrap AddListener<TArgs>(Action<object, TArgs> action) where TArgs : EventArgs
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
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

    public GeneralUpdateBootstrap AddListenerUpdateInfo(
        Action<object, UpdateInfoEventArgs> cb) => AddListener(cb);

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
            $"Multi download error {ObjectTranslator.GetPacketHash(e.Version)}.", e.Exception);
        EventManager.Instance.Dispatch(sender, e);
    }

    private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
    {
        GeneralTracer.Info($"Multi all download completed {e.IsAllDownloadCompleted}.");
        EventManager.Instance.Dispatch(sender, e);
    }
}
