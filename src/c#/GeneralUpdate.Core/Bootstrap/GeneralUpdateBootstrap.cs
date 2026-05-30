using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Strategy;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Differential.Differ;

namespace GeneralUpdate.Core;

/// <summary>
/// Unified update entry point for all application update scenarios.
/// Configure the update via fluent methods, then call <see cref="LaunchAsync"/> to execute.
/// </summary>
/// <remarks>
/// <para><b>Core flow:</b></para>
/// <para>
/// 1. <b>Configuration</b> — <see cref="SetConfig(Configinfo)"/> loads parameters (versions, paths, URLs).<br/>
/// 2. <b>Extension resolution</b> — <see cref="LaunchWithStrategy"/> resolves all registered
///    extension points (strategy, hooks, download components, network policies) and injects
///    them into the role strategy.<br/>
/// 3. <b>Role dispatch</b> — <see cref="AppType"/> selects the role strategy:<br/>
///    • <see cref="AppType.Client"/> — validates against server, downloads packages,
///      applies upgrade packages in-place, serializes client packages to IPC, and launches
///      the upgrade process.<br/>
///    • <see cref="AppType.Upgrade"/> — reads IPC data, applies client packages via the
///      OS pipeline, then starts the main application.<br/>
///    • <see cref="AppType.OssClient"/>/<see cref="AppType.OssUpgrade"/> — Oss-based
///      workflow for cloud storage (AliYun, AWS S3, MinIO).<br/>
/// 4. <b>Platform resolution</b> — <see cref="ClientStrategy.ResolveOsStrategy"/>
///    detects the OS and creates <c>WindowsStrategy</c>, <c>LinuxStrategy</c>, or
///    <c>MacStrategy</c> unless overridden via <c>Strategy&lt;T&gt;()</c>.<br/>
/// 5. <b>Pipeline execution</b> — the OS strategy runs <c>HashMiddleware → CompressMiddleware
///    → PatchMiddleware</c> for each update version.<br/>
/// 6. <b>App launch</b> — the OS strategy starts the updated main application.
/// </para>
/// <para><b>Silent mode:</b> when <c>Option(UpdateOptions.Silent, true)</c> is set on
/// <see cref="AppType.Client"/>, launches a background poll loop via
/// <see cref="Silent.SilentPollOrchestrator"/> and returns immediately. Updates are
/// prepared on process exit.</para>
/// <para><b>Extension points:</b> <see cref="AbstractBootstrap{TBootstrap, TStrategy}"/>
/// provides fluent methods for injecting custom implementations of hooks, download
/// components, network policies, and OS strategy.</para>
/// </remarks>
/// <example>
/// <code>
/// var result = await new GeneralUpdateBootstrap()
///     .SetConfig(new Configinfo {
///         UpdateUrl = "https://api.example.com",
///         ClientVersion = "1.0.0",
///         InstallPath = @"C:\MyApp",
///         AppSecretKey = "my-key"
///     })
///     .Option(UpdateOptions.AppType, AppType.Client)
///     .Hooks&lt;MyCustomHooks&gt;()
///     .LaunchAsync();
/// </code>
/// </example>
public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
{
    private GlobalConfigInfo _configInfo = new();
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private CancellationTokenSource? _cts;
    private DiffPipelineBuilder? _diffPipelineBuilder;

    public GeneralUpdateBootstrap()
    {
        InitializeFromEnvironment();
    }

    /// <summary>Cancel the current update operation.</summary>
    /// <remarks>
    /// Signals the internal <see cref="CancellationTokenSource"/> to request cancellation
    /// of the ongoing update workflow. After calling this method, the running strategy
    /// (e.g., <see cref="ClientStrategy"/>) will observe the cancellation
    /// token and terminate the current operation at the next safe checkpoint.
    /// A cancellation message is also written to the trace log.
    /// </remarks>
    public void Cancel()
    {
        _cts?.Cancel();
        GeneralTracer.Info("GeneralUpdateBootstrap: cancellation requested.");
    }

    /// <summary>
    /// Dispatches the update workflow based on <see cref="AppType"/>.
    /// <see cref="AppType.Client"/> with silent mode returns immediately after starting a
    /// background poll; all other modes run synchronously.
    /// </summary>
    /// <returns>This bootstrap instance for chaining.</returns>
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
            AppType.Client => await LaunchWithStrategy(new ClientStrategy()),
            AppType.Upgrade => await LaunchWithStrategy(new UpdateStrategy()),
            AppType.OssClient => await LaunchWithStrategy(new OssStrategy(AppType.OssClient)),
            AppType.OssUpgrade => await LaunchWithStrategy(new OssStrategy(AppType.OssUpgrade)),
            _ => await LaunchWithStrategy(new ClientStrategy())
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

            ConfigureStrategy(roleStrategy);

            if (roleStrategy is ClientStrategy cs)
                await CallSmallBowlHomeAsync(_configInfo.Bowl).ConfigureAwait(false);

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
    
    /// <summary>
    /// Applies the primary configuration object. Validates required fields, maps to
    /// the internal <see cref="GlobalConfigInfo"/>, and initialises the blacklist
    /// matcher for file exclusion during update operations.
    /// </summary>
    /// <param name="configInfo">User-facing configuration. All required fields must
    /// be set explicitly — no auto-discovery is performed. Use
    /// <see cref="SetSource"/> for the zero-config path.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
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

    /// <summary>
    /// Load configuration from a local JSON file.
    /// </summary>
    /// <param name="filePath">
    /// Config file path.
    /// If just a filename (no directory separator), resolves relative to the current directory.
    /// Relative or absolute paths are used as-is.
    /// </param>
    /// <returns>This bootstrap instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the JSON file cannot be deserialised into a valid <see cref="Configinfo"/>.</exception>
    /// <remarks>
    /// Reads the file content as UTF-8 JSON, deserialises it into a <see cref="Configinfo"/>
    /// using the source-generated JSON serialisation context (<see cref="JsonContext.HttpParameterJsonContext"/>),
    /// then delegates to <see cref="SetConfig(Configinfo)"/> for validation and mapping.
    /// </remarks>
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
    ///     Zero-config entry-point: supply only secrets. Identity metadata is auto-discovered
    ///     from <c>generalupdate.manifest.json</c> and hard-coded defaults.
    /// </summary>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap SetSource(
        string updateUrl,
        string appSecretKey,
        string? reportUrl = null,
        string? scheme = null,
        string? token = null)
    {
        var config = new Configinfo
        {
            UpdateUrl = updateUrl,
            AppSecretKey = appSecretKey,
            Token = token,
            Scheme = scheme,
            ReportUrl = reportUrl
        };
        config = AppMetadataDiscoverer.Discover(config);
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

    /// <summary>
    /// Registers a pre-check callback that is invoked after update information is retrieved
    /// but before downloads begin. The callback can inspect the metadata and decide whether
    /// to proceed with or abort the update.
    /// </summary>
    /// <param name="func">A predicate that receives <see cref="UpdateInfoEventArgs"/>
    /// and returns <c>true</c> to continue or <c>false</c> to abort the update.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The pre-check function is called during the client update strategy's standard workflow,
    /// immediately after version comparison and before any packages are downloaded.
    /// This allows conditional update logic, such as skipping updates under certain
    /// network conditions or user preferences.
    /// </remarks>
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
        // The Upgrade process is only ever launched by the Client — no IPC means
        // there is nothing to do. The Client's manifest.json flows through IPC,
        // so the Upgrade never needs to load one directly.
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
            ReportType = processInfo.ReportType,
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
    /// Uses the same fully-configured <see cref="ClientStrategy"/> as the standard flow;
    /// the orchestrator only adds polling and deferred-upgrade-on-exit on top.
    /// </summary>
    private async Task<GeneralUpdateBootstrap> LaunchSilentAsync()
    {
        GeneralTracer.Info("GeneralUpdateBootstrap: starting silent update mode.");

        ApplyRuntimeOptions();

        // Network-level extensions (global, applied before any HTTP call)
        var sslPolicy = ResolveExtension<Security.ISslValidationPolicy>();
        if (sslPolicy != null) Network.VersionService.SetSslValidationPolicy(sslPolicy);
        var authProvider = ResolveExtension<Security.IHttpAuthProvider>();
        if (authProvider != null) Network.VersionService.SetDefaultAuthProvider(authProvider);

        var strategy = new ClientStrategy();
        ConfigureStrategy(strategy);

        strategy.LaunchAfterPrepare = false;

        var pollMinutes = GetOption(UpdateOptions.SilentPollIntervalMinutes);
        var launchClient = GetOption(UpdateOptions.LaunchClientAfterUpdate);
        var silentOptions = new Silent.SilentOptions
        {
            PollInterval = TimeSpan.FromMinutes(pollMinutes),
            LaunchClientAfterUpdate = launchClient
        };

        var orchestrator = new Silent.SilentPollOrchestrator(strategy, _configInfo, silentOptions);
        await orchestrator.StartAsync().ConfigureAwait(false);
        GeneralTracer.Info("GeneralUpdateBootstrap: silent update mode started, returning to caller.");

        return this;
    }

    /// <summary>
    /// Applies all registered extensions (hooks, reporter, download components,
    /// DiffPipeline, custom OS strategy) to a role strategy. Shared by the standard
    /// and silent boot paths so they are always configured identically.
    /// </summary>
    private void ConfigureStrategy(IStrategy roleStrategy)
    {
        var hooks = ResolveExtension<Hooks.IUpdateHooks>() ?? new Hooks.NoOpUpdateHooks();
        roleStrategy.Hooks = hooks;

        var reporter = ResolveExtension<Download.Reporting.IUpdateReporter>() ??
                       new Download.Reporting.HttpUpdateReporter();
        if (reporter is Download.Reporting.HttpUpdateReporter httpReporter)
            httpReporter.ReportUrl = _configInfo.ReportUrl;
        roleStrategy.Reporter = reporter;

        var diffPipeline = BuildDiffPipeline();

        // Download components
        var downloadOrchestrator = ResolveExtension<Download.Abstractions.IDownloadOrchestrator>();
        var downloadPolicy = ResolveExtension<Download.Abstractions.IDownloadPolicy>();
        var downloadExecutor = ResolveExtension<Download.Abstractions.IDownloadExecutor>();

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

        switch (roleStrategy)
        {
            case ClientStrategy cs:
                cs.DownloadSource = ResolveExtension<Download.Abstractions.IDownloadSource>();
                cs.SetDiffPipeline(diffPipeline);
                if (downloadOrchestrator != null) cs.SetOrchestrator(downloadOrchestrator);
                if (downloadPolicy != null) cs.SetDownloadPolicy(downloadPolicy);
                if (downloadExecutor != null) cs.SetDownloadExecutor(downloadExecutor);
                if (downloadPipelineFactory != null) cs.SetDownloadPipelineFactory(downloadPipelineFactory);
                if (_updatePrecheck != null) cs.UseUpdatePrecheck(_updatePrecheck);
                break;

            case UpdateStrategy us:
                us.SetDiffPipeline(diffPipeline);
                us.SetReportType(_configInfo.ReportType);
                break;
        }

        // Custom OS-level strategy (registered via Strategy<T>())
        var customOsStrategy = ResolveExtension<IStrategy>();
        if (customOsStrategy != null)
        {
            switch (roleStrategy)
            {
                case ClientStrategy cs: cs.SetOsStrategy(customOsStrategy); break;
                case UpdateStrategy us: us.SetOsStrategy(customOsStrategy); break;
            }
        }
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

    /// <summary>
    /// Registers a callback for the multi-all-download-completed event, raised when all
    /// update files across all versions have been downloaded.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap AddListenerMultiAllDownloadCompleted(
        Action<object, MultiAllDownloadCompletedEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Registers a callback for the multi-download-completed event, raised when a single
    /// version's set of files has finished downloading.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap AddListenerMultiDownloadCompleted(
        Action<object, MultiDownloadCompletedEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Registers a callback for the multi-download-error event, raised when an error
    /// occurs during batch download.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap AddListenerMultiDownloadError(
        Action<object, MultiDownloadErrorEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Registers a callback for download statistics events, providing real-time throughput
    /// and progress information during batch downloads.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap AddListenerMultiDownloadStatistics(
        Action<object, MultiDownloadStatisticsEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Registers a callback for exception events raised during the update workflow.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap AddListenerException(
        Action<object, ExceptionEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Registers a callback for update-information events, providing version metadata
    /// retrieved from the server.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
    public GeneralUpdateBootstrap AddListenerUpdateInfo(
        Action<object, UpdateInfoEventArgs> cb) => AddListener(cb);

    /// <summary>
    /// Registers a callback for general progress events during the update process.
    /// </summary>
    /// <param name="cb">The callback to invoke.</param>
    /// <returns>This bootstrap instance for chaining.</returns>
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