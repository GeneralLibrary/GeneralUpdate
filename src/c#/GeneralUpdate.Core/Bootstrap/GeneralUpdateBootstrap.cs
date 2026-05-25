using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.Download.Reporting;

namespace GeneralUpdate.Core;

/// <summary>
/// Unified update bootstrap — single entry point for Client, Upgrade, and OSS roles.
/// Use <see cref="AppType"/> to select the workflow:
/// <list type="bullet">
///   <item><see cref="AppType.Client"/> — validate versions, download, start upgrade process</item>
///   <item><see cref="AppType.Upgrade"/> — receive ProcessInfo, apply updates, start main app</item>
///   <item><see cref="AppType.OSS"/> — OSS-based cloud storage update</item>
/// </list>
/// </summary>
/// <remarks>
/// For Client mode, use <c>Option(UpdateOptions.AppType, AppType.Client)</c>.
/// </remarks>
public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
{
    private GlobalConfigInfo _configInfo = new();
    private Func<bool>? _customSkipOption;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private CancellationTokenSource? _cts;

    public GeneralUpdateBootstrap()
    {
        InitializeFromEnvironment();
    }

    /// <summary>Cancel the current update operation.</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        GeneralTracer.Info("GeneralUpdateBootstrap: cancellation requested.");
    }

    // ════════════════════════════════════════════════════════════════
    // Launch — AppType dispatch via role strategies
    // ════════════════════════════════════════════════════════════════

    public override async Task<GeneralUpdateBootstrap> LaunchAsync()
    {
        var appType = GetOption(UpdateOptions.AppType);

        // Silent mode: start background poll and return immediately
        if (appType == AppType.Client && GetOption(UpdateOptions.Silent))
        {
            await LaunchSilentAsync().ConfigureAwait(false);
            return this;
        }

        return appType switch
        {
            AppType.Client  => await LaunchWithStrategy(new ClientUpdateStrategy()),
            AppType.Upgrade => await LaunchWithStrategy(new UpgradeUpdateStrategy()),
            AppType.OSS     => await LaunchOssAsync(),
            _ => await LaunchWithStrategy(new ClientUpdateStrategy())
        };
    }

    private async Task<GeneralUpdateBootstrap> LaunchWithStrategy(IStrategy roleStrategy)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            ApplyRuntimeOptions();

            // Resolve hooks and reporter from extensions
            var hooks = ResolveExtension<Hooks.IUpdateHooks>() ?? new Hooks.NoOpUpdateHooks();
            var reporter = ResolveExtension<Download.Reporting.IUpdateReporter>() ?? new Download.Reporting.NoOpUpdateReporter();

            // Configure client-specific callbacks
            if (roleStrategy is ClientUpdateStrategy clientStrat)
            {
                clientStrat.Hooks = hooks;
                clientStrat.Reporter = reporter;
                // Resolve DownloadSource from extension registry (Hub, custom, etc.)
                var resolvedSource = ResolveExtension<Download.Abstractions.IDownloadSource>();

                // Inject SignalR Hub download source if configured (not available in AOT)
#if !AOT
                if (resolvedSource == null)
                {
                    var hubConfig = GetOption(UpdateOptions.Hub);
                    if (hubConfig != null && !string.IsNullOrEmpty(hubConfig.Url))
                    {
                        var hubSource = new Download.Sources.HubDownloadSource(
                            hubConfig.Url, _configInfo.Token, _configInfo.AppSecretKey);
                        await hubSource.StartAsync().ConfigureAwait(false);
                        resolvedSource = hubSource;
                        GeneralTracer.Info("GeneralUpdateBootstrap: HubDownloadSource started from HubConfig.");
                    }
                }
#endif
                clientStrat.DownloadSource = resolvedSource;
                if (_updatePrecheck != null)
                    clientStrat.UseUpdatePrecheck(_updatePrecheck);
                await CallSmallBowlHomeAsync(_configInfo.Bowl).ConfigureAwait(false);
            }
            else if (roleStrategy is UpgradeUpdateStrategy upgradeStrat)
            {
                upgradeStrat.Hooks = hooks;
                upgradeStrat.Reporter = reporter;
            }
            else if (roleStrategy is OSSUpdateStrategy ossStrat)
            {
                ossStrat.Hooks = hooks;
                ossStrat.Reporter = reporter;
            }

            roleStrategy.Create(_configInfo);
            await roleStrategy.ExecuteAsync();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("LaunchWithStrategy failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
        finally
        {
            // Dispose HubDownloadSource if it was started
            if (roleStrategy is ClientUpdateStrategy cs && cs.DownloadSource is IDisposable d)
                d.Dispose();
            _cts?.Dispose();
            _cts = null;
        }
        return this;
    }

    /// <summary>OSS workflow: download packages from cloud storage, apply updates.</summary>
    private async Task<GeneralUpdateBootstrap> LaunchOssAsync()
    {
        try
        {
            GeneralTracer.Debug("LaunchOssAsync start.");

            var json = Environments.GetEnvironmentVariable("GlobalConfigInfoOSS");
            if (!string.IsNullOrWhiteSpace(json))
            {
                var strategy = new OSSUpdateStrategy();
                strategy.Create(_configInfo);
                await strategy.ExecuteAsync();
                return this;
            }

            // Client-side OSS
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.AppName}_versions.json";
            var versionsFilePath = Path.Combine(basePath, versionFileName);

            DownloadOssFile(_configInfo.UpdateUrl, versionsFilePath);
            if (!File.Exists(versionsFilePath)) return this;

            var versions = StorageManager.GetJson<List<VersionOSS>>(versionsFilePath,
                VersionOSSJsonContext.Default.ListVersionOSS);
            if (versions == null || versions.Count == 0) return this;

            versions = versions.OrderByDescending(x => x.PubTime).ToList();
            var newVersion = versions.First();

            if (!IsOssUpgrade(_configInfo.ClientVersion, newVersion.Version))
            {
                GeneralTracer.Info("LaunchOssAsync: no upgrade needed.");
                return this;
            }

            var upgradeAppName = "GeneralUpdate.Upgrade.exe";
            var appPath = Path.Combine(basePath, upgradeAppName);
            if (!File.Exists(appPath))
                throw new Exception($"Upgrade application not found: {upgradeAppName}");

            var ossConfig = new GlobalConfigInfoOSS
            {
                AppName = _configInfo.MainAppName ?? _configInfo.AppName,
                CurrentVersion = _configInfo.ClientVersion,
                VersionFileName = versionFileName,
                Encoding = (_configInfo.Encoding?.CodePage ?? Encoding.UTF8.CodePage).ToString(),
                Url = _configInfo.UpdateUrl
            };

            var serialized = JsonSerializer.Serialize(ossConfig,
                GlobalConfigInfoOSSJsonContext.Default.GlobalConfigInfoOSS);
            Environments.SetEnvironmentVariable("GlobalConfigInfoOSS", serialized);
            Process.Start(appPath);
            await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("LaunchOssAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
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
        if (appType != AppType.Upgrade)
        {
            _configInfo.TempPath = StorageManager.GetTempDirectory("upgrade_temp");
            InitBlackList();
        }

        return this;
    }

    public GeneralUpdateBootstrap SetCustomSkipOption(Func<bool>? func)
    {
        _customSkipOption = func;
        return this;
    }

    public GeneralUpdateBootstrap AddListenerUpdatePrecheck(Func<UpdateInfoEventArgs, bool> func)
    {
        _updatePrecheck = func ?? throw new ArgumentNullException(nameof(func));
        return this;
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
            Script = processInfo.Script,
            DriverDirectory = processInfo.DriverDirectory
        };
    }

    /// <summary>
    /// Applies UpdateOptions to _configInfo.
    /// Uses ??= only for values that InitializeFromEnvironment() may have already
    /// populated on the Upgrade path (Encoding, Format, DownloadTimeOut).
    /// All other options are always applied from UpdateOptions — their defaults
    /// are already functionally reasonable (e.g. MaxConcurrency=3, RetryCount=3).
    /// </summary>
    private void ApplyRuntimeOptions()
    {
        // Preserve Upgrade path values set by InitializeFromEnvironment()
        _configInfo.Encoding ??= GetOption(UpdateOptions.Encoding);
        _configInfo.Format ??= GetOption(UpdateOptions.Format);
        if (_configInfo.DownloadTimeOut <= 0)
            _configInfo.DownloadTimeOut = GetOption(UpdateOptions.DownloadTimeout) ?? 60;

        // bool? options: use ??= so user-configured false is preserved
        _configInfo.PatchEnabled ??= GetOption(UpdateOptions.PatchEnabled);
        _configInfo.BackupEnabled ??= GetOption(UpdateOptions.BackupEnabled);

        // Always apply from UpdateOptions — no other code sets these before
        // ApplyRuntimeOptions() runs. Defaults are functionally reasonable.
        _configInfo.MaxConcurrency = GetOption(UpdateOptions.MaxConcurrency);
        _configInfo.EnableResume = GetOption(UpdateOptions.EnableResume);
        _configInfo.RetryCount = GetOption(UpdateOptions.RetryCount);
        _configInfo.RetryInterval = GetOption(UpdateOptions.RetryInterval);
        _configInfo.VerifyChecksum = GetOption(UpdateOptions.VerifyChecksum);
        _configInfo.DiffMode = GetOption(UpdateOptions.DiffMode);
    }

    /// <summary>
    /// Silent update mode — starts a background poll loop and returns immediately.
    /// The orchestrator checks for updates periodically and prepares them.
    /// When the host process exits, the prepared update is applied.
    /// Not available in AOT builds (SignalR dependency).
    /// </summary>
    private async Task LaunchSilentAsync()
    {
#if !AOT
        GeneralTracer.Info("GeneralUpdateBootstrap: starting silent update mode.");

        var pollMinutes = GetOption(UpdateOptions.SilentPollIntervalMinutes);
        var autoInstall = GetOption(UpdateOptions.SilentAutoInstall);

        var silentOptions = new Silent.SilentOptions
        {
            PollInterval = TimeSpan.FromMinutes(pollMinutes),
            AutoInstall = autoInstall
        };

        var hooks = ResolveExtension<Hooks.IUpdateHooks>() ?? new Hooks.NoOpUpdateHooks();
        var reporter = ResolveExtension<Download.Reporting.IUpdateReporter>() ?? new Download.Reporting.NoOpUpdateReporter();

        var orchestrator = new Silent.SilentPollOrchestrator(_configInfo, silentOptions)
            .WithHooks(hooks)
            .WithReporter(reporter);

        await orchestrator.StartAsync().ConfigureAwait(false);
        GeneralTracer.Info("GeneralUpdateBootstrap: silent update mode started, returning to caller.");
#else
        GeneralTracer.Warn("GeneralUpdateBootstrap: silent update not available in AOT builds.");
        await Task.CompletedTask;
#endif
    }

    private void InitBlackList()
    {
        BlackListManager.Instance.AddBlackFiles(_configInfo.BlackFiles);
        BlackListManager.Instance.AddBlackFormats(_configInfo.BlackFormats);
        BlackListManager.Instance.AddSkipDirectorys(_configInfo.SkipDirectorys);
    }

    private async Task CallSmallBowlHomeAsync(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                GeneralTracer.Info($"Shutting down process {process.ProcessName} (ID: {process.Id})");
                await GracefulExit.ShutdownAsync(process).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("CallSmallBowlHomeAsync failed.", ex);
        }
    }

    private static void DownloadOssFile(string url, string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        using var webClient = new System.Net.WebClient();
        webClient.DownloadFile(new Uri(url), path);
    }

    private static bool IsOssUpgrade(string clientVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion))
            return false;
        return Version.TryParse(clientVersion, out var cv)
            && Version.TryParse(serverVersion, out var sv)
            && cv < sv;
    }

    // ════════════════════════════════════════════════════════════════
    // Strategy & Events
    // ════════════════════════════════════════════════════════════════

    protected override GeneralUpdateBootstrap StrategyFactory()
        => throw new NotImplementedException("Role strategies handle this.");

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

    public GeneralUpdateBootstrap AddListenerProgress(
        Action<object, ProgressEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Batch-register an event listener implementing <see cref="IUpdateEventListener"/>.
    /// All 7 event handlers are registered at once.
    /// </summary>
    public GeneralUpdateBootstrap AddEventListener<TListener>() where TListener : IUpdateEventListener, new()
    {
        var listener = new TListener();
        AddListener<MultiAllDownloadCompletedEventArgs>((s, e) => listener.OnAllDownloadCompleted(e));
        AddListener<MultiDownloadCompletedEventArgs>((s, e) => listener.OnDownloadCompleted(e));
        AddListener<MultiDownloadErrorEventArgs>((s, e) => listener.OnDownloadError(e));
        AddListener<MultiDownloadStatisticsEventArgs>((s, e) => listener.OnDownloadStatistics(e));
        AddListener<UpdateInfoEventArgs>((s, e) => listener.OnUpdateInfo(e));
        AddListener<ExceptionEventArgs>((s, e) => listener.OnException(e));
        AddListener<ProgressEventArgs>((s, e) => listener.OnProgress(e));
        return this;
    }
}
