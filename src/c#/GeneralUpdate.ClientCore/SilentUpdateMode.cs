using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Strategys;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.ClientCore;

internal sealed class SilentUpdateMode
{
    private const string ProcessInfoEnvironmentKey = "ProcessInfo";
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(20);
    private readonly GlobalConfigInfo _configInfo;
    private readonly Encoding _encoding;
    private readonly string _format;
    private readonly int _downloadTimeOut;
    private readonly bool _patchEnabled;
    private readonly bool _backupEnabled;
    private Task? _pollingTask;
    private int _prepared;
    private int _updaterStarted;

    public SilentUpdateMode(GlobalConfigInfo configInfo, Encoding encoding, string format, int downloadTimeOut, bool patchEnabled, bool backupEnabled)
    {
        _configInfo = configInfo;
        _encoding = encoding;
        _format = format;
        _downloadTimeOut = downloadTimeOut;
        _patchEnabled = patchEnabled;
        _backupEnabled = backupEnabled;
    }

    public Task StartAsync()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        _pollingTask = Task.Run(PollLoopAsync);
        _pollingTask.ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                GeneralTracer.Error("The StartAsync method in SilentUpdateMode captured a polling exception.", task.Exception);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
        return Task.CompletedTask;
    }

    private async Task PollLoopAsync()
    {
        while (Volatile.Read(ref _prepared) == 0)
        {
            try
            {
                await PrepareUpdateIfNeededAsync();
            }
            catch (Exception exception)
            {
                GeneralTracer.Error("The PollLoopAsync method in SilentUpdateMode throws an exception.", exception);
            }

            if (Volatile.Read(ref _prepared) == 1)
                break;

            await Task.Delay(PollingInterval);
        }
    }

    private async Task PrepareUpdateIfNeededAsync()
    {
        var mainResp = await VersionService.Validate(_configInfo.UpdateUrl
            , _configInfo.ClientVersion
            , AppType.ClientApp
            , _configInfo.AppSecretKey
            , GetPlatform()
            , _configInfo.ProductId
            , _configInfo.Scheme
            , _configInfo.Token);

        if (mainResp?.Code != 200 || mainResp.Body == null || mainResp.Body.Count == 0)
            return;

        var versions = mainResp.Body.OrderBy(x => x.ReleaseDate).ToList();
        var latestVersion = versions.Last().Version;
        if (CheckFail(latestVersion))
            return;

        BlackListManager.Instance?.AddBlackFiles(_configInfo.BlackFiles);
        BlackListManager.Instance?.AddBlackFileFormats(_configInfo.BlackFormats);
        BlackListManager.Instance?.AddSkipDirectorys(_configInfo.SkipDirectorys);

        _configInfo.Encoding = _encoding;
        _configInfo.Format = _format;
        _configInfo.DownloadTimeOut = _downloadTimeOut;
        _configInfo.PatchEnabled = _patchEnabled;
        _configInfo.IsMainUpdate = true;
        _configInfo.LastVersion = latestVersion;
        _configInfo.UpdateVersions = versions;
        _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath, $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        if (_backupEnabled)
        {
            StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory, BlackListManager.Instance.SkipDirectorys);
        }

        var processInfo = ConfigurationMapper.MapToProcessInfo(
            _configInfo,
            versions,
            BlackListManager.Instance.BlackFileFormats.ToList(),
            BlackListManager.Instance.BlackFiles.ToList(),
            BlackListManager.Instance.SkipDirectorys.ToList());
        _configInfo.ProcessInfo = JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);

        var manager = new DownloadManager(_configInfo.TempPath, _configInfo.Format, _configInfo.DownloadTimeOut);
        foreach (var versionInfo in _configInfo.UpdateVersions)
        {
            manager.Add(new DownloadTask(manager, versionInfo));
        }
        await manager.LaunchTasksAsync();

        var strategy = CreateStrategy();
        strategy.Create(_configInfo);
        await strategy.ExecuteAsync();

        Interlocked.Exchange(ref _prepared, 1);
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _prepared) != 1 || Interlocked.Exchange(ref _updaterStarted, 1) == 1)
            return;

        try
        {
            Environments.SetEnvironmentVariable(ProcessInfoEnvironmentKey, _configInfo.ProcessInfo);
            var updaterPath = Path.Combine(_configInfo.InstallPath, _configInfo.AppName);
            if (File.Exists(updaterPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = updaterPath
                });
            }
        }
        catch (Exception exception)
        {
            GeneralTracer.Error("The OnProcessExit method in SilentUpdateMode throws an exception.", exception);
        }
    }

    private IStrategy CreateStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy();
        throw new PlatformNotSupportedException("The current operating system is not supported!");
    }

    private static int GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformType.Linux;
        return -1;
    }

    private static bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;

        var failVersion = new Version(fail);
        var latestVersion = new Version(version);
        return failVersion >= latestVersion;
    }
}
