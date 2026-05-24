using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Upgrade-side update strategy. Receives process info from the client side,
/// applies updates via the pipeline, and starts the main application.
/// </summary>
/// <remarks>
/// This is the AppType.UpgradeApp role strategy. It composes an OS-specific
/// strategy for platform operations (Windows/Linux/Mac).
/// </remarks>
public class UpgradeUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
    }

    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("UpgradeUpdateStrategy not configured.");

        try
        {
            GeneralTracer.Debug("UpgradeUpdateStrategy.ExecuteAsync start.");
            StrategyFactory();

            var mode = UpdateMode.Default; // should come from config
            switch (mode)
            {
                case UpdateMode.Default:
                    ApplyRuntimeOptions();
                    _osStrategy!.Create(_configInfo);
                    await DownloadAsync();
                    await _osStrategy.ExecuteAsync();
                    break;
                case UpdateMode.Scripts:
                    await ExecuteScriptsWorkflowAsync();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("UpgradeUpdateStrategy.ExecuteAsync failed.", ex);
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

    #region Scripts Mode Workflow

    private async Task ExecuteScriptsWorkflowAsync()
    {
        GeneralTracer.Info($"UpgradeUpdateStrategy: validating version. Url={_configInfo!.UpdateUrl}, Version={_configInfo.ClientVersion}");

        var mainResp = await VersionService.Validate(
            _configInfo.UpdateUrl, _configInfo.ClientVersion,
            AppType.ClientApp, _configInfo.AppSecretKey,
            GetPlatform(), _configInfo.ProductId,
            _configInfo.Scheme, _configInfo.Token);

        _configInfo.IsMainUpdate = CheckUpgrade(mainResp);
        EventManager.Instance.Dispatch(this, new UpdateInfoEventArgs(mainResp));

        if (CheckForcibly(mainResp.Body) == false && ShouldSkip())
        {
            GeneralTracer.Info("UpgradeUpdateStrategy: update skipped.");
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

        Backup();

        _osStrategy!.Create(_configInfo);

        if (_configInfo.IsMainUpdate)
        {
            GeneralTracer.Info("UpgradeUpdateStrategy: main update required.");
            await DownloadAsync();
            await _osStrategy.ExecuteAsync();
        }
        else
        {
            GeneralTracer.Info("UpgradeUpdateStrategy: no update needed, starting application.");
            _osStrategy.StartApp();
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

    private void StrategyFactory()
    {
        _osStrategy ??= ResolveOsStrategy();
    }

    private void ApplyRuntimeOptions()
    {
        _configInfo!.Encoding = Encoding.UTF8;
        _configInfo.Format = Format.ZIP;
        _configInfo.DownloadTimeOut = 60;
    }

    private void InitBlackList()
    {
        BlackListManager.Instance.AddBlackFiles(_configInfo!.BlackFiles);
        BlackListManager.Instance.AddBlackFormats(_configInfo.BlackFormats);
        BlackListManager.Instance.AddSkipDirectorys(_configInfo.SkipDirectorys);
    }

    private void Backup()
    {
        GeneralTracer.Info($"UpgradeUpdateStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
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

    private static bool ShouldSkip() => false;

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
}
