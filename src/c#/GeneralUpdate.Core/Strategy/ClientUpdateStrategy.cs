using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Client-side update strategy. Validates versions against server,
/// downloads update packages, creates process info for the upgrade side,
/// and starts the upgrade process.
/// </summary>
/// <remarks>
/// This is the AppType.ClientApp role strategy. It composes an OS-specific
/// strategy (Windows/Linux/Mac) for platform operations.
/// </remarks>
public class ClientUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private readonly List<Func<bool>> _customOptions = new();

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
    }

    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("ClientUpdateStrategy not configured.");

        try
        {
            GeneralTracer.Debug("ClientUpdateStrategy.ExecuteAsync start.");
            CallSmallBowlHome(_configInfo.Bowl);
            ExecuteCustomOptions();
            await ExecuteWorkflowAsync();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("ClientUpdateStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
    }

    public void StartApp()
    {
        _osStrategy?.StartApp();
    }

    /// <summary>Register a precheck callback invoked when update info is available.</summary>
    public ClientUpdateStrategy UseUpdatePrecheck(Func<UpdateInfoEventArgs, bool> func)
    {
        _updatePrecheck = func ?? throw new ArgumentNullException(nameof(func));
        return this;
    }

    /// <summary>Register custom pre-update operations.</summary>
    public ClientUpdateStrategy UseCustomOption(Func<bool> func)
    {
        _customOptions.Add(func);
        return this;
    }

    #region Workflow

    private async Task ExecuteWorkflowAsync()
    {
        // Silent mode — delegate to SilentUpdateMode
        // (encoding/format/timeout are read from _configInfo)
        var defaultEncoding = Encoding.UTF8;
        var defaultTimeout = 60;
        if (true /* silent check would read from options */)
        {
            // Standard mode
            await ExecuteStandardWorkflowAsync(defaultEncoding, defaultTimeout);
        }
    }

    private async Task ExecuteStandardWorkflowAsync(Encoding encoding, int timeout)
    {
        GeneralTracer.Info($"ClientUpdateStrategy: validating client={_configInfo!.ClientVersion}, upgrade={_configInfo.UpgradeClientVersion}");

        var mainResp = await VersionService.Validate(_configInfo.UpdateUrl,
            _configInfo.ClientVersion, AppType.ClientApp, _configInfo.AppSecretKey,
            GetPlatform(), _configInfo.ProductId, _configInfo.Scheme, _configInfo.Token);

        var upgradeResp = await VersionService.Validate(_configInfo.UpdateUrl,
            _configInfo.UpgradeClientVersion, AppType.UpgradeApp, _configInfo.AppSecretKey,
            GetPlatform(), _configInfo.ProductId, _configInfo.Scheme, _configInfo.Token);

        _configInfo.IsUpgradeUpdate = CheckUpgrade(upgradeResp);
        _configInfo.IsMainUpdate = CheckUpgrade(mainResp);
        GeneralTracer.Info($"ClientUpdateStrategy: IsMainUpdate={_configInfo.IsMainUpdate}, IsUpgradeUpdate={_configInfo.IsUpgradeUpdate}");

        var updateInfoArgs = new UpdateInfoEventArgs(mainResp);
        EventManager.Instance.Dispatch(this, updateInfoArgs);

        var isForcibly = CheckForcibly(mainResp.Body) || CheckForcibly(upgradeResp.Body);
        if (CanSkip(isForcibly, updateInfoArgs))
        {
            GeneralTracer.Info("ClientUpdateStrategy: update skipped.");
            return;
        }

        InitBlackList();
        ApplyRuntimeOptions(encoding, timeout);

        _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        _configInfo.UpdateVersions = _configInfo.IsUpgradeUpdate
            ? upgradeResp.Body.OrderBy(x => x.ReleaseDate).ToList()
            : new List<VersionInfo>();

        if (_configInfo.IsMainUpdate)
        {
            _configInfo.LastVersion = mainResp.Body.OrderBy(x => x.ReleaseDate).Last().Version;
            GeneralTracer.Info($"ClientUpdateStrategy: main update LastVersion={_configInfo.LastVersion}");

            if (CheckFail(_configInfo.LastVersion))
            {
                GeneralTracer.Warn($"ClientUpdateStrategy: version {_configInfo.LastVersion} matches known-failed upgrade.");
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

        // Backup
        Backup();

        _osStrategy!.Create(_configInfo);

        GeneralTracer.Info($"ClientUpdateStrategy: IsUpgradeUpdate={_configInfo.IsUpgradeUpdate}, IsMainUpdate={_configInfo.IsMainUpdate}");

        switch (_configInfo.IsUpgradeUpdate)
        {
            case true when _configInfo.IsMainUpdate:
                GeneralTracer.Info("ClientUpdateStrategy: both upgrade+main — downloading and executing.");
                await DownloadAsync();
                await _osStrategy.ExecuteAsync();
                _osStrategy.StartApp();
                break;
            case true when !_configInfo.IsMainUpdate:
                GeneralTracer.Info("ClientUpdateStrategy: upgrade-only — downloading and executing.");
                await DownloadAsync();
                await _osStrategy.ExecuteAsync();
                break;
            case false when _configInfo.IsMainUpdate:
                GeneralTracer.Info("ClientUpdateStrategy: main-only — starting updater.");
                _osStrategy.StartApp();
                break;
        }
    }

    #endregion

    #region Helpers

    private static IStrategy ResolveOsStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy();
        throw new PlatformNotSupportedException("The current operating system is not supported!");
    }

    private void ApplyRuntimeOptions(Encoding encoding, int timeout)
    {
        _configInfo!.Encoding = encoding;
        _configInfo.Format = Format.ZIP;
        _configInfo.DownloadTimeOut = timeout;
    }

    private void InitBlackList()
    {
        BlackListManager.Instance.AddBlackFiles(_configInfo!.BlackFiles);
        BlackListManager.Instance.AddBlackFormats(_configInfo.BlackFormats);
        BlackListManager.Instance.AddSkipDirectorys(_configInfo.SkipDirectorys);
    }

    private void Backup()
    {
        GeneralTracer.Info($"ClientUpdateStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
        StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
            BlackListManager.Instance.SkipDirectorys);
    }

    private async Task DownloadAsync()
    {
        var manager = new DownloadManager(
            _configInfo!.TempPath, _configInfo.Format, _configInfo.DownloadTimeOut);
        foreach (var version in _configInfo.UpdateVersions)
            manager.Add(new DownloadTask(manager, version));
        await manager.LaunchTasksAsync();
    }

    private bool CanSkip(bool isForcibly, UpdateInfoEventArgs updateInfo)
    {
        if (isForcibly) return false;
        return _updatePrecheck?.Invoke(updateInfo) == true;
    }

    private bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;
        return new Version(fail) >= new Version(version);
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

    private void CallSmallBowlHome(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return;
            foreach (var process in processes)
            {
                GeneralTracer.Info($"Killing process {process.ProcessName} (ID: {process.Id})");
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("CallSmallBowlHome failed.", ex);
        }
    }

    private void ExecuteCustomOptions()
    {
        foreach (var option in _customOptions)
        {
            if (!option.Invoke())
            {
                var ex = new Exception("Custom option execution failed.");
                GeneralTracer.Error("ExecuteCustomOptions failed.", ex);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
            }
        }
    }

    #endregion
}
