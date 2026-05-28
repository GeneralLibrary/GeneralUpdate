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
using GeneralUpdate.Core.Security;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;

namespace GeneralUpdate.Core;

/// <summary>
/// Unified update bootstrap — single entry point for Client, Upgrade, and OSS roles.
/// Use <see cref="AppType"/> to select the workflow:
/// <list type="bullet">
///   <item><see cref="AppType.Client"/> — validate versions, download, start upgrade process</item>
///   <item><see cref="AppType.Upgrade"/> — receive ProcessInfo, apply updates, start main app</item>
///   <item><see cref="AppType.OSSClient"/> — OSS client: download version config, start upgrade process</item>
///   <item><see cref="AppType.OSSUpgrade"/> — OSS upgrade: download packages from cloud, start main app</item>
/// </list>
/// </summary>
/// <remarks>
/// For Client mode, use <c>Option(UpdateOptions.AppType, AppType.Client)</c>.
/// </remarks>
public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
{
    private GlobalConfigInfo _configInfo = new();
    private Func<UpdateInfoEventArgs, bool>? _customSkipOption;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private CancellationTokenSource? _cts;
    private DiffPipelineBuilder? _diffPipelineBuilder;

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
        _configInfo.AppType = appType;

        // Silent mode: start background poll and return immediately
        if (appType == AppType.Client && GetOption(UpdateOptions.Silent))
        {
            await LaunchSilentAsync().ConfigureAwait(false);
            return this;
        }

        return appType switch
        {
            AppType.Client => await LaunchWithStrategy(new ClientUpdateStrategy()),
            AppType.Upgrade => await LaunchWithStrategy(new UpgradeUpdateStrategy()),
            AppType.OSSClient => await LaunchWithStrategy(new OSSUpdateStrategy(AppType.OSSClient)),
            AppType.OSSUpgrade => await LaunchWithStrategy(new OSSUpdateStrategy(AppType.OSSUpgrade)),
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

            // ── Network-level extensions (global, applied before any HTTP call) ──
            var sslPolicy = ResolveExtension<Security.ISslValidationPolicy>();
            if (sslPolicy != null) Network.VersionService.SetSslValidationPolicy(sslPolicy);
            var authProvider = ResolveExtension<Security.IHttpAuthProvider>();
            if (authProvider != null) Network.VersionService.SetDefaultAuthProvider(authProvider);

            // Resolve hooks from extensions
            var hooks = ResolveExtension<Hooks.IUpdateHooks>() ?? new Hooks.NoOpUpdateHooks();
            roleStrategy.Hooks = hooks;

            // ── Download components ──
            var downloadOrchestrator = ResolveExtension<Download.Abstractions.IDownloadOrchestrator>();
            var downloadPolicy = ResolveExtension<Download.Abstractions.IDownloadPolicy>();
            var downloadExecutor = ResolveExtension<Download.Abstractions.IDownloadExecutor>();

            // Build download pipeline factory from registered extension type
            Func<string?, Download.Abstractions.IDownloadPipeline>? downloadPipelineFactory = null;
            var pipelineType = ResolveExtensionType<Download.Abstractions.IDownloadPipeline>();
            if (pipelineType != null)
            {
                var stringCtor = pipelineType.GetConstructor([typeof(string)]);
                if (stringCtor != null)
                    downloadPipelineFactory = hash => (Download.Abstractions.IDownloadPipeline)stringCtor.Invoke([hash]);
                else
                    downloadPipelineFactory = _ => (Download.Abstractions.IDownloadPipeline)Activator.CreateInstance(pipelineType);
            }

            var diffPipeline = BuildDiffPipeline();

            switch (roleStrategy)
            {
                case ClientUpdateStrategy cs:
                    cs.DownloadSource = ResolveExtension<Download.Abstractions.IDownloadSource>();

                    if (_updatePrecheck != null)
                        cs.UseUpdatePrecheck(_updatePrecheck);
                    if (_customSkipOption != null)
                        cs.UseCustomSkipOption(_customSkipOption);

                    await CallSmallBowlHomeAsync(_configInfo.Bowl).ConfigureAwait(false);

                    cs.SetDiffPipeline(diffPipeline);
                    if (downloadOrchestrator != null) cs.SetOrchestrator(downloadOrchestrator);
                    if (downloadPolicy != null) cs.SetDownloadPolicy(downloadPolicy);
                    if (downloadExecutor != null) cs.SetDownloadExecutor(downloadExecutor);
                    if (downloadPipelineFactory != null) cs.SetDownloadPipelineFactory(downloadPipelineFactory);
                    break;

                case UpgradeUpdateStrategy us:
                    us.SetDiffPipeline(diffPipeline);
                    break;
            }

            // Inject custom OS-level strategy if registered via Strategy<T>()
            var customOsStrategy = ResolveExtension<IStrategy>();
            if (customOsStrategy != null)
            {
                switch (roleStrategy)
                {
                    case ClientUpdateStrategy cs:
                        cs.SetOsStrategy(customOsStrategy);
                        break;
                    case UpgradeUpdateStrategy us:
                        us.SetOsStrategy(customOsStrategy);
                        break;
                }
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
            _cts?.Dispose();
            _cts = null;
        }

        return this;
    }
    
    // ════════════════════════════════════════════════════════════════
    // Configuration
    // ════════════════════════════════════════════════════════════════

    public GeneralUpdateBootstrap SetConfig(Configinfo configInfo)
    {
        configInfo.Validate();
        _configInfo = ConfigurationMapper.MapToGlobalConfigInfo(configInfo);

        var appType = GetOption(UpdateOptions.AppType);
        if (appType != AppType.Upgrade)
        {
            _configInfo.TempPath = StorageManager.GetTempDirectory("upgrade_temp");
            InitBlackList();
        }

        return this;
    }

    public GeneralUpdateBootstrap SetCustomSkipOption(Func<UpdateInfoEventArgs, bool>? func)
    {
        _customSkipOption = func;
        return this;
    }

    /// <summary>
    /// Load configuration from a local JSON file.
    /// </summary>
    /// <param name="filePath">
    /// Config file path.
    /// If just a filename (no directory separator), resolves relative to the current directory.
    /// Relative or absolute paths are used as-is.
    /// </param>
    public GeneralUpdateBootstrap SetConfig(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        // Resolve filename-only paths to current directory
        var hasPathChar = filePath.Contains(Path.DirectorySeparatorChar)
                          || filePath.Contains(Path.AltDirectorySeparatorChar);
        var fullPath = hasPathChar
            ? Path.GetFullPath(filePath)
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Config file not found: {fullPath}");

        var json = File.ReadAllText(fullPath);
        var config = JsonSerializer.Deserialize(json, JsonContext.HttpParameterJsonContext.Default.Configinfo);
        if (config == null)
            throw new InvalidOperationException($"Failed to parse config file: {fullPath}");

        return SetConfig(config);
    }

    /// <summary>
    /// Configure the <see cref="DiffPipeline"/> via a fluent builder action.
    /// If not called, a default pipeline is built with <see cref="BsdiffDiffer"/>,
    /// <see cref="DefaultDirtyMatcher"/>, <see cref="DefaultCleanMatcher"/>,
    /// and max parallelism of 2.
    /// </summary>
    public GeneralUpdateBootstrap UseDiffPipeline(Action<DiffPipelineBuilder>? configure)
    {
        var builder = new DiffPipelineBuilder();
        configure?.Invoke(builder);
        _diffPipelineBuilder = builder;
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
        // Read ProcessInfo via AES-encrypted file IPC.
        var processInfo = new EncryptedFileProcessInfoProvider().Receive();
        if (processInfo == null) return;

        _configInfo = new GlobalConfigInfo
        {
            MainAppName = processInfo.AppName,
            InstallPath = processInfo.InstallPath,
            ClientVersion = processInfo.CurrentVersion,
            LastVersion = processInfo.LastVersion,
            UpdateLogUrl = processInfo.UpdateLogUrl,
            Encoding = Encoding.GetEncoding(processInfo.CompressEncoding),
            Format = ParseFormat(processInfo.CompressFormat),
            DownloadTimeOut = processInfo.DownloadTimeOut,
            AppSecretKey = processInfo.AppSecretKey,
            UpdateVersions = processInfo.UpdateVersions,
            TempPath = processInfo.TempPath,
            ReportUrl = processInfo.ReportUrl,
            BackupDirectory = processInfo.BackupDirectory,
            Scheme = processInfo.Scheme,
            Token = processInfo.Token,
            DriverDirectory = processInfo.DriverDirectory,
            UpdatePath = processInfo.UpdatePath,
            LaunchClientAfterUpdate = processInfo.LaunchClientAfterUpdate,
            BlackFiles = processInfo.BlackFiles ?? BlackListDefaults.DefaultBlackFiles,
            BlackFormats = processInfo.BlackFileFormats ?? BlackListDefaults.DefaultBlackFormats,
            SkipDirectorys = processInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories
        };

        StorageManager.BlackListMatcher = DefaultBlackListMatcher.FromConfigInfo(_configInfo);
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
        _configInfo.Format = GetOption(UpdateOptions.Format);
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
    /// </summary>
    private async Task LaunchSilentAsync()
    {
        GeneralTracer.Info("GeneralUpdateBootstrap: starting silent update mode.");

        var pollMinutes = GetOption(UpdateOptions.SilentPollIntervalMinutes);

        var silentOptions = new Silent.SilentOptions
        {
            PollInterval = TimeSpan.FromMinutes(pollMinutes)
        };

        var hooks = ResolveExtension<Hooks.IUpdateHooks>() ?? new Hooks.NoOpUpdateHooks();
        var reporter = ResolveExtension<Download.Reporting.IUpdateReporter>() ??
                       new Download.Reporting.NoOpUpdateReporter();

        var sslPolicy = ResolveExtension<Security.ISslValidationPolicy>();
        if (sslPolicy != null) Network.VersionService.SetSslValidationPolicy(sslPolicy);
        var authProvider = ResolveExtension<Security.IHttpAuthProvider>();
        if (authProvider != null) Network.VersionService.SetDefaultAuthProvider(authProvider);

        var orchestrator = new Silent.SilentPollOrchestrator(_configInfo, silentOptions)
            .WithHooks(hooks)
            .WithReporter(reporter)
            .WithOsStrategy(ResolveExtension<IStrategy>());

        await orchestrator.StartAsync().ConfigureAwait(false);
        GeneralTracer.Info("GeneralUpdateBootstrap: silent update mode started, returning to caller.");
    }

    private DiffPipeline BuildDiffPipeline()
    {
        if (_diffPipelineBuilder != null)
            return _diffPipelineBuilder.Build();

        return new DiffPipelineBuilder()
            .UseDiffer(new BsdiffDiffer())
            .UseCleanMatcher(new DefaultCleanMatcher())
            .UseDirtyMatcher(new DefaultDirtyMatcher())
            .WithParallelism(2)
            .WithProgress(new DiffProgressReporter(this))
            .Build();
    }

    private static Format ParseFormat(string? compressFormat)
    {
        if (string.IsNullOrWhiteSpace(compressFormat)) return Format.Zip;
        return compressFormat switch
        {
            ".zip" => Format.Zip,
            _ => Format.Zip
        };
    }

    private void InitBlackList()
    {
        // Build blacklist matcher from GlobalConfigInfo and set on StorageManager.
        // The matcher combines user config with system defaults.
        var effectiveConfig = new BlackListConfig(
            _configInfo.BlackFiles?.Count > 0 ? _configInfo.BlackFiles : BlackListDefaults.DefaultBlackFiles,
            _configInfo.BlackFormats?.Count > 0 ? _configInfo.BlackFormats : BlackListDefaults.DefaultBlackFormats,
            _configInfo.SkipDirectorys?.Count > 0
                ? _configInfo.SkipDirectorys
                : BlackListDefaults.DefaultSkipDirectories
        );
        StorageManager.BlackListMatcher = new DefaultBlackListMatcher(effectiveConfig);
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

    // ════════════════════════════════════════════════════════════════
    // Strategy & Events
    // ════════════════════════════════════════════════════════════════

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