using System;
using System.Collections.Generic;
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

internal sealed class SilentUpdateOptions
{
    public Encoding FileEncoding { get; set; } = Encoding.Default;
    public string Format { get; set; } = global::GeneralUpdate.Common.Shared.Object.Format.ZIP;
    public int DownloadTimeOut { get; set; } = 60;
    public bool DriveEnabled { get; set; }
    public bool PatchEnabled { get; set; } = true;
    public bool BackUpEnabled { get; set; } = true;
}

internal sealed class SilentUpdateMode : IDisposable
{
    private readonly GlobalConfigInfo _configInfo;
    private readonly SilentUpdateOptions _options;
    private readonly TimeSpan _pollingInterval;
    private readonly Action<Exception> _onError;
    private readonly object _launchLock = new();
    private IStrategy? _strategy;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private int _workflowExecuting;
    private int _isPrepared;
    private int _isPollingStarted;
    private int _isDisposed;
    private int _isLaunchTriggered;
    private int _isExitHookRegistered;
    private bool _silentUpdaterLaunched;

    public SilentUpdateMode(
        GlobalConfigInfo configInfo,
        SilentUpdateOptions options,
        TimeSpan pollingInterval,
        Action<Exception> onError)
    {
        _configInfo = configInfo;
        _options = options;
        _pollingInterval = pollingInterval;
        _onError = onError;
    }

    public async Task EnterAsync()
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            return;
        }

        await ExecuteWorkflowWithReentryGuardAsync();
        StartPolling();
    }

    private async Task ExecuteWorkflowWithReentryGuardAsync()
    {
        if (Interlocked.CompareExchange(ref _workflowExecuting, 1, 0) != 0)
        {
            GeneralTracer.Debug("Skip silent workflow execution because previous execution is still running.");
            return;
        }

        try
        {
            if (await DetectSilentMainUpdateAsync())
            {
                await PrepareAndScheduleAsync();
            }
        }
        finally
        {
            Volatile.Write(ref _workflowExecuting, 0);
        }
    }

    private async Task PrepareAndScheduleAsync()
    {
        if (Interlocked.CompareExchange(ref _isPrepared, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await PrepareSilentMainUpdateAsync();
            StopPolling();
            RegisterExitHook();
        }
        catch
        {
            Interlocked.Exchange(ref _isPrepared, 0);
            throw;
        }
    }

    private void RegisterExitHook()
    {
        if (Interlocked.CompareExchange(ref _isExitHookRegistered, 1, 0) == 0)
        {
            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainProcessExit;
        }
    }

    private void UnregisterExitHook()
    {
        if (Interlocked.CompareExchange(ref _isExitHookRegistered, 0, 1) == 1)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnCurrentDomainProcessExit;
        }
    }

    private void OnCurrentDomainProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _isPrepared) == 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isLaunchTriggered, 1, 0) != 0)
        {
            return;
        }

        try
        {
            LaunchSilentUpdater();
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
    }

    private void StartPolling()
    {
        if (Volatile.Read(ref _isPrepared) == 1 ||
            Volatile.Read(ref _isDisposed) == 1 ||
            Interlocked.CompareExchange(ref _isPollingStarted, 1, 0) != 0)
        {
            return;
        }

        _pollingCts = new CancellationTokenSource();
        _pollingTask = RunPollingLoopAsync(_pollingCts.Token);
    }

    private void StopPolling()
    {
        var cts = Interlocked.Exchange(ref _pollingCts, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _isPrepared) == 0)
            {
                await ExecuteWorkflowWithReentryGuardAsync();
                await Task.Delay(_pollingInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation.
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
        finally
        {
            Interlocked.Exchange(ref _isPollingStarted, 0);
            _pollingTask = null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        StopPolling();
        UnregisterExitHook();
    }

    private async Task<bool> DetectSilentMainUpdateAsync()
    {
        var mainResp = await VersionService.Validate(_configInfo.UpdateUrl
            , _configInfo.ClientVersion
            , AppType.ClientApp
            , _configInfo.AppSecretKey
            , GetPlatform()
            , _configInfo.ProductId
            , _configInfo.Scheme
            , _configInfo.Token);

        _configInfo.IsMainUpdate = CheckUpgrade(mainResp);
        if (!_configInfo.IsMainUpdate)
        {
            return false;
        }

        var orderedMainVersions = mainResp.Body.OrderBy(x => x.ReleaseDate).ToList();
        if (!orderedMainVersions.Any())
        {
            return false;
        }

        _configInfo.LastVersion = orderedMainVersions.Last().Version;
        if (CheckFail(_configInfo.LastVersion))
        {
            return false;
        }

        BlackListManager.Instance?.AddBlackFiles(_configInfo.BlackFiles);
        BlackListManager.Instance?.AddBlackFileFormats(_configInfo.BlackFormats);
        BlackListManager.Instance?.AddSkipDirectorys(_configInfo.SkipDirectorys);
        _configInfo.Encoding = _options.FileEncoding;
        _configInfo.Format = _options.Format;
        _configInfo.DownloadTimeOut = _options.DownloadTimeOut;
        _configInfo.DriveEnabled = _options.DriveEnabled;
        _configInfo.PatchEnabled = _options.PatchEnabled;
        _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");
        _configInfo.UpdateVersions = orderedMainVersions;

        var processInfo = ConfigurationMapper.MapToProcessInfo(
            _configInfo,
            orderedMainVersions,
            BlackListManager.Instance.BlackFileFormats.ToList(),
            BlackListManager.Instance.BlackFiles.ToList(),
            BlackListManager.Instance.SkipDirectorys.ToList());
        _configInfo.ProcessInfo = JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);

        return true;
    }

    private async Task PrepareSilentMainUpdateAsync()
    {
        if (_options.BackUpEnabled)
        {
            StorageManager.Backup(_configInfo.InstallPath
                , _configInfo.BackupDirectory
                , BlackListManager.Instance.SkipDirectorys);
        }

        StrategyFactory();
        await DownloadAsync();
        if (_strategy != null)
        {
            await _strategy.ExecuteAsync();
        }
        GeneralTracer.Info("Silent update package prepared; updater launch deferred until process exit.");
    }

    private void LaunchSilentUpdater()
    {
        lock (_launchLock)
        {
            if (_silentUpdaterLaunched)
            {
                return;
            }

            var appPath = Path.Combine(_configInfo.InstallPath, _configInfo.AppName);
            if (!File.Exists(appPath))
            {
                GeneralTracer.Error($"Silent update failed because updater was not found: {appPath}.");
                return;
            }

            Environments.SetEnvironmentVariable("ProcessInfo", _configInfo.ProcessInfo);
            Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = appPath
            });
            _silentUpdaterLaunched = true;
        }
    }

    private async Task DownloadAsync()
    {
        var manager = new DownloadManager(_configInfo.TempPath, _configInfo.Format, _configInfo.DownloadTimeOut);
        foreach (var versionInfo in _configInfo.UpdateVersions)
        {
            manager.Add(new DownloadTask(manager, versionInfo));
        }

        await manager.LaunchTasksAsync();
    }

    private void StrategyFactory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _strategy = new WindowsStrategy();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _strategy = new LinuxStrategy();
        }
        else
        {
            throw new PlatformNotSupportedException("The current operating system is not supported!");
        }

        _strategy.Create(_configInfo);
    }

    private static int GetPlatform()
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

    private static bool CheckUpgrade(VersionRespDTO? response)
    {
        if (response is null || response.Body is null)
        {
            return false;
        }

        return response.Code == 200 && response.Body.Count > 0;
    }

    private static bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
        {
            return false;
        }

        var failVersion = new Version(fail);
        var lastVersion = new Version(version);
        return failVersion >= lastVersion;
    }
}
