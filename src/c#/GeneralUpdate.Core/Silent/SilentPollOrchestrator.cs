using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Strategy;

namespace GeneralUpdate.Core.Silent;

/// <summary>
/// Silent update polling orchestrator -- periodically checks for updates,
/// silently downloads update packages in the background, and defers application
/// update execution until the process exits.
/// </summary>
/// <remarks>
/// <para>
/// Follows the same AppType dispatch pattern as <see cref="ClientUpdateStrategy"/>:
/// </para>
/// <list type="bullet">
///   <item><description><b>Upgrade (AppType=2)</b>: Update packages are applied in-place
///   during the polling cycle. They operate on <c>UpdatePath</c> rather than
///   the running application's <c>InstallPath</c>.</description></item>
///   <item><description><b>Client (AppType=1)</b>: Update packages are deferred -- stored in
///   <c>ProcessInfo</c> and handed off to the Upgrade process via IPC when the
///   current process exits.</description></item>
/// </list>
/// <para>
/// Core workflow:
/// <list type="number">
///   <item><description>Starts a background polling loop that checks the server for new versions
///   at intervals specified by <see cref="SilentOptions.PollInterval"/>.</description></item>
///   <item><description>When a new version is found, silently downloads all update packages
///   in the background.</description></item>
///   <item><description>Upgrade packages are applied immediately; Client packages are staged
///   into <c>ProcessInfo</c>.</description></item>
///   <item><description>Listens to the <see cref="AppDomain.ProcessExit"/> event and launches the
///   upgrade program when the process exits.</description></item>
/// </list>
/// </para>
/// </remarks>
public class SilentPollOrchestrator : IDisposable
{
    private readonly GlobalConfigInfo _configInfo;
    private readonly SilentOptions _options;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private int _prepared;
    private int _updaterStarted;
    private IUpdateHooks? _hooks;
    private IUpdateReporter? _reporter;
    private IStrategy? _customOsStrategy;
    private Configuration.ProcessInfo? _preparedProcessInfo;
    private List<VersionInfo> _clientVersions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SilentPollOrchestrator"/> class.
    /// </summary>
    /// <param name="configInfo">The global configuration information, including update URL,
    /// version numbers, product information, etc.</param>
    /// <param name="options">The silent update options, including polling interval and
    /// client launch behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configInfo"/>
    /// or <paramref name="options"/> is <c>null</c>.</exception>
    public SilentPollOrchestrator(GlobalConfigInfo configInfo, SilentOptions options)
    {
        _configInfo = configInfo ?? throw new ArgumentNullException(nameof(configInfo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Sets the update lifecycle hooks (fluent method).
    /// </summary>
    /// <param name="hooks">An <see cref="IUpdateHooks"/> implementation for injecting
    /// custom logic at various update stages.</param>
    /// <returns>The current <see cref="SilentPollOrchestrator"/> instance, enabling fluent chaining.</returns>
    public SilentPollOrchestrator WithHooks(IUpdateHooks? hooks) { _hooks = hooks; return this; }

    /// <summary>
    /// Sets the update progress reporter (fluent method).
    /// </summary>
    /// <param name="reporter">An <see cref="IUpdateReporter"/> implementation for reporting
    /// update status.</param>
    /// <returns>The current <see cref="SilentPollOrchestrator"/> instance, enabling fluent chaining.</returns>
    public SilentPollOrchestrator WithReporter(IUpdateReporter? reporter) { _reporter = reporter; return this; }

    /// <summary>
    /// Sets a custom operating system strategy (fluent method), overriding the default
    /// platform-specific update strategy (Windows/Linux/macOS).
    /// </summary>
    /// <param name="strategy">A custom <see cref="IStrategy"/> implementation.</param>
    /// <returns>The current <see cref="SilentPollOrchestrator"/> instance, enabling fluent chaining.</returns>
    public SilentPollOrchestrator WithOsStrategy(IStrategy? strategy) { _customOsStrategy = strategy; return this; }

    /// <summary>
    /// Starts the silent polling orchestrator, beginning background update checks.
    /// </summary>
    /// <returns>A task representing the start operation. Note: this task completes as soon as
    /// the polling loop is launched, not when the polling loop ends.</returns>
    /// <remarks>
    /// <para>
    /// Start flow:
    /// <list type="number">
    ///   <item><description>Registers the <see cref="AppDomain.CurrentDomain.ProcessExit"/> event handler.</description></item>
    ///   <item><description>Creates a <see cref="CancellationTokenSource"/>.</description></item>
    ///   <item><description>Launches the polling loop <see cref="PollLoopAsync"/> in a background task.</description></item>
    ///   <item><description>Attaches an exception handler to the polling task to log unhandled exceptions.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public Task StartAsync()
    {
        GeneralTracer.Info($"SilentPollOrchestrator: starting. PollInterval={_options.PollInterval.TotalMinutes}min");

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        _cts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token));

        _pollingTask.ContinueWith(task =>
        {
            if (task.Exception != null)
                GeneralTracer.Error("SilentPollOrchestrator: polling exception.", task.Exception);
        }, TaskContinuationOptions.OnlyOnFaulted);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the silent polling orchestrator, cancelling in-progress polling and download operations.
    /// </summary>
    /// <remarks>
    /// Calling this method will:
    /// <list type="bullet">
    ///   <item><description>Cancel the background polling loop.</description></item>
    ///   <item><description>Unregister the <see cref="AppDomain.CurrentDomain.ProcessExit"/> event handler.</description></item>
    /// </list>
    /// Note: This method does not cancel downloads that are already in progress,
    /// but it prevents new polling cycles from starting.
    /// </remarks>
    public void Stop()
    {
        _cts?.Cancel();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    }

    /// <summary>
    /// Background polling loop that periodically checks for updates and performs
    /// update preparation at the configured interval.
    /// </summary>
    /// <param name="token">A cancellation token for stopping the polling loop.</param>
    /// <returns>A task representing the polling loop. Ends when preparation is complete
    /// or cancellation is requested.</returns>
    /// <remarks>
    /// <para>
    /// Loop logic:
    /// <list type="number">
    ///   <item><description>Calls <see cref="PrepareUpdateIfNeededAsync"/> to check for and prepare updates.</description></item>
    ///   <item><description>If preparation is complete (<c>_prepared == 1</c>), exits the loop.</description></item>
    ///   <item><description>Otherwise, waits for <see cref="SilentOptions.PollInterval"/> before checking again.</description></item>
    /// </list>
    /// Individual exceptions within the loop do not terminate the entire loop;
    /// errors are logged and polling continues on the next cycle.
    /// </para>
    /// </remarks>
    private async Task PollLoopAsync(CancellationToken token)
    {
        GeneralTracer.Info("SilentPollOrchestrator: polling loop started.");
        while (!token.IsCancellationRequested && Volatile.Read(ref _prepared) == 0)
        {
            try
            {
                await PrepareUpdateIfNeededAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("SilentPollOrchestrator: poll cycle failed.", ex);
            }

            if (Volatile.Read(ref _prepared) == 1) break;

            try { await Task.Delay(_options.PollInterval, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Checks for available updates and, if found, performs the full update preparation
    /// workflow including download, backup, Upgrade package application, and ProcessInfo staging.
    /// </summary>
    /// <param name="token">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This is the core method of the silent update flow, executing the complete workflow:
    /// </para>
    /// <list type="number">
    ///   <item><description>Queries the server for available updates via <see cref="HttpDownloadSource"/>.</description></item>
    ///   <item><description>Builds a download plan using <see cref="DownloadPlanBuilder"/>.</description></item>
    ///   <item><description>Checks version failure records to skip known-failed versions.</description></item>
    ///   <item><description>Calls hooks' <c>OnBeforeUpdateAsync</c> to allow external cancellation of the update.</description></item>
    ///   <item><description>Initializes the blacklist, creates a temp directory, and performs backup.</description></item>
    ///   <item><description>Downloads all update packages using
    ///   <see cref="Download.Orchestrators.DefaultDownloadOrchestrator"/>.</description></item>
    ///   <item><description>Dispatches by AppType: Upgrade packages are executed immediately via the
    ///   platform strategy; Client packages are staged in ProcessInfo.</description></item>
    ///   <item><description>Calls hooks' <c>OnAfterUpdateAsync</c> to notify that the update is complete.</description></item>
    /// </list>
    /// </remarks>
    private async Task PrepareUpdateIfNeededAsync(CancellationToken token)
    {
        GeneralTracer.Info($"SilentPollOrchestrator: checking for updates. Url={_configInfo.UpdateUrl}");

        var downloadSource = new HttpDownloadSource(
            _configInfo.UpdateUrl,
            _configInfo.ClientVersion,
            _configInfo.UpgradeClientVersion,
            _configInfo.AppSecretKey,
            GetPlatform(),
            _configInfo.ProductId,
            _configInfo.Scheme,
            _configInfo.Token);

        var sourceResult = await downloadSource.ListAsync(token).ConfigureAwait(false);
        var plan = DownloadPlanBuilder.Build(sourceResult.Assets, _configInfo.ClientVersion);

        if (!plan.HasAssets)
        {
            GeneralTracer.Info("SilentPollOrchestrator: no update available.");
            return;
        }

        var latestVersion = plan.Assets.LastOrDefault()?.Version;
        if (CheckFail(latestVersion))
        {
            GeneralTracer.Warn($"SilentPollOrchestrator: version {latestVersion} is a known-failed upgrade, skipping.");
            return;
        }

        // Hooks: allow cancellation before starting update
        var updateCtx = new UpdateContext(
            _configInfo.MainAppName ?? _configInfo.UpdateAppName,
            _configInfo.InstallPath,
            _configInfo.ClientVersion,
            latestVersion,
            AppType.Client);

        if (_hooks != null)
        {
            try
            {
                if (!await _hooks.OnBeforeUpdateAsync(updateCtx).ConfigureAwait(false))
                {
                    GeneralTracer.Info("SilentPollOrchestrator: update cancelled by hooks.");
                    return;
                }
            }
            catch (Exception ex) { GeneralTracer.Warn($"Hook OnBeforeUpdateAsync failed: {ex.Message}"); }
        }

        InitBlackList();

        _configInfo.LastVersion = latestVersion;
        _configInfo.TempPath = StorageManager.GetTempDirectory("silent_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        // Backup
        if (_configInfo.BackupEnabled != false)
        {
            StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
                _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);
        }

        // Reporter: update started
        if (_reporter != null)
        {
            try { await _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Updating, 1), token).ConfigureAwait(false); }
            catch (Exception ex) { GeneralTracer.Warn($"Reporter UpdateStarted failed: {ex.Message}"); }
        }

        // Download all packages in background
        GeneralTracer.Info($"SilentPollOrchestrator: downloading {plan.Assets.Count} asset(s).");
        var httpClient = GeneralUpdate.Core.Network.HttpClientProvider.Shared;
        try
        {
            var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(httpClient);
            var report = await orchestrator.ExecuteAsync(plan, _configInfo.TempPath, token: token).ConfigureAwait(false);
            GeneralTracer.Info($"SilentPollOrchestrator: download complete. Success={report.SuccessCount}, Failed={report.FailedCount}");

            if (report.FailedCount > 0)
            {
                GeneralTracer.Error($"SilentPollOrchestrator: download had {report.FailedCount} failures, aborting update.");
                return;
            }

            if (_hooks != null)
            {
                try
                {
                    var downloadCtx = new DownloadContext(
                        plan.Assets.FirstOrDefault()?.Name ?? "update", latestVersion ?? "",
                        report.TotalBytes, report.TotalDuration,
                        _configInfo.TempPath, report.FailedCount == 0);
                    await _hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
                }
                catch (Exception ex) { GeneralTracer.Warn($"Hook OnDownloadCompletedAsync failed: {ex.Message}"); }
            }
            if (_reporter != null)
            {
                try { await _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Updating, 1), token).ConfigureAwait(false); }
                catch (Exception ex) { GeneralTracer.Warn($"Reporter DownloadCompleted failed: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: download failed.", ex);
            TryReportError(updateCtx, ex);
            return;
        }

        // Split packages by AppType -- mirrors ClientUpdateStrategy
        var downloadVersions = plan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = _configInfo.Format.ToExtension(),
            AppType = a.AppType ?? (int)AppType.Client
        }).ToList();

        var upgradeVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Upgrade).ToList();
        _clientVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Client).ToList();
        GeneralTracer.Info($"SilentPollOrchestrator: Upgrade packages={upgradeVersions.Count}, Client packages={_clientVersions.Count}");

        // Apply Upgrade packages in place -- safe because they target UpdatePath
        if (upgradeVersions.Count > 0)
        {
            GeneralTracer.Info("SilentPollOrchestrator: applying Upgrade packages.");
            try
            {
                _configInfo.UpdateVersions = upgradeVersions;
                var strategy = CreateStrategy();
                strategy.Create(_configInfo);
                await strategy.ExecuteAsync().ConfigureAwait(false);
                GeneralTracer.Info("SilentPollOrchestrator: Upgrade packages applied.");
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("SilentPollOrchestrator: Upgrade package application failed.", ex);
                TryReportError(updateCtx, ex);
                return;
            }
        }

        // Build ProcessInfo with Client packages for IPC delivery on process exit
        if (_clientVersions.Count > 0)
        {
            _configInfo.LaunchClientAfterUpdate = _options.LaunchClientAfterUpdate;
            _preparedProcessInfo = ConfigurationMapper.MapToProcessInfo(
                _configInfo, _clientVersions,
                _configInfo.BlackFormats ?? BlackListDefaults.DefaultBlackFormats,
                _configInfo.BlackFiles ?? BlackListDefaults.DefaultBlackFiles,
                _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);
            _configInfo.ProcessInfo = JsonSerializer.Serialize(_preparedProcessInfo, ProcessInfoJsonContext.Default.ProcessInfo);

            Interlocked.Exchange(ref _prepared, 1);
            GeneralTracer.Info("SilentPollOrchestrator: update prepared, waiting for process exit.");
        }
        else if (upgradeVersions.Count > 0)
        {
            // Upgrade-only: packages already applied, no handoff needed
            Interlocked.Exchange(ref _prepared, 1);
            GeneralTracer.Info("SilentPollOrchestrator: upgrade-only update applied, no client handoff needed.");
        }

        if (_hooks != null)
        {
            try { await _hooks.OnAfterUpdateAsync(updateCtx).ConfigureAwait(false); }
            catch (Exception ex) { GeneralTracer.Warn($"Hook OnAfterUpdateAsync failed: {ex.Message}"); }
        }
        if (_reporter != null)
        {
            try { await _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Success, 1), token).ConfigureAwait(false); }
            catch (Exception ex) { GeneralTracer.Warn($"Reporter UpdateApplied failed: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Process exit event handler -- when the process is about to exit, sends the prepared
    /// Client package information to the upgrade program via IPC and launches the upgrade.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    /// <remarks>
    /// <para>
    /// This method is invoked when the <see cref="AppDomain.CurrentDomain.ProcessExit"/>
    /// event fires. It only executes when an update has been prepared (<c>_prepared == 1</c>)
    /// and the upgrade program has not yet been started.
    /// </para>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    ///   <item><description>Uses <c>Interlocked.Exchange</c> to ensure the upgrade program is only
    ///   started once.</description></item>
    ///   <item><description>Resolves the upgrade program path (prefers UpdatePath, falls back to InstallPath).</description></item>
    ///   <item><description>If Client packages exist, sends the encrypted ProcessInfo via
    ///   <see cref="EncryptedFileProcessInfoProvider"/>.</description></item>
    ///   <item><description>Starts the upgrade process (uses ShellExecute for privilege elevation).</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _prepared) != 1 || Interlocked.Exchange(ref _updaterStarted, 1) == 1) return;

        try
        {
            // Resolve updater location -- prefers UpdatePath, falls back to InstallPath
            var updaterDir = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath)
                ? (Path.IsPathRooted(_configInfo.UpdatePath)
                    ? _configInfo.UpdatePath
                    : Path.Combine(_configInfo.InstallPath, _configInfo.UpdatePath))
                : _configInfo.InstallPath;
            var updaterPath = Path.Combine(updaterDir, _configInfo.UpdateAppName);

            if (!File.Exists(updaterPath))
            {
                GeneralTracer.Warn($"SilentPollOrchestrator: updater not found at {updaterPath}, cannot launch.");
                return;
            }

            // Send ProcessInfo with Client packages via encrypted file IPC BEFORE starting Upgrade
            if (_preparedProcessInfo != null && _clientVersions.Count > 0)
            {
                new EncryptedFileProcessInfoProvider().Send(_preparedProcessInfo);
                GeneralTracer.Info($"SilentPollOrchestrator: ProcessInfo sent with {_clientVersions.Count} Client package(s).");
            }

            Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = updaterPath });
            GeneralTracer.Info($"SilentPollOrchestrator: launched updater {updaterPath}");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: OnProcessExit failed.", ex);
        }
    }

    /// <summary>
    /// Attempts to report an update error through hooks and the reporter.
    /// </summary>
    /// <param name="ctx">The update context containing application information.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <remarks>
    /// This method makes a best-effort attempt to call <c>_hooks.OnUpdateErrorAsync</c> and
    /// <c>_reporter.ReportAsync</c>. Even if these calls themselves throw exceptions,
    /// the exceptions are not propagated (only a warning is logged), ensuring that a
    /// failure in error reporting does not mask the original error.
    /// </remarks>
    private void TryReportError(UpdateContext ctx, Exception ex)
    {
        if (_hooks != null)
        {
            try { _hooks.OnUpdateErrorAsync(ctx, ex).GetAwaiter().GetResult(); }
            catch (Exception hookEx) { GeneralTracer.Warn($"Hook OnUpdateErrorAsync failed: {hookEx.Message}"); }
        }
        if (_reporter != null)
        {
            try { _reporter.ReportAsync(new UpdateReport(0, (int)UpdateStatus.Failure, 1)).GetAwaiter().GetResult(); }
            catch (Exception reporterEx) { GeneralTracer.Warn($"Reporter UpdateFailed failed: {reporterEx.Message}"); }
        }
    }

    /// <summary>
    /// Initializes the <see cref="StorageManager.BlackListMatcher"/> using the configured
    /// blacklist information. Falls back to <see cref="BlackListDefaults"/> if the
    /// configured lists are empty.
    /// </summary>
    private void InitBlackList()
    {
        var effectiveConfig = new BlackListConfig(
            _configInfo.BlackFiles?.Count > 0 ? _configInfo.BlackFiles : BlackListDefaults.DefaultBlackFiles,
            _configInfo.BlackFormats?.Count > 0 ? _configInfo.BlackFormats : BlackListDefaults.DefaultBlackFormats,
            _configInfo.SkipDirectorys?.Count > 0 ? _configInfo.SkipDirectorys : BlackListDefaults.DefaultSkipDirectories
        );
        StorageManager.BlackListMatcher = new DefaultBlackListMatcher(effectiveConfig);
    }

    /// <summary>
    /// Creates the appropriate update strategy instance based on the current operating system.
    /// Uses the custom strategy (<c>_customOsStrategy</c>) if one has been set.
    /// </summary>
    /// <returns>An <see cref="IStrategy"/> implementation matching the current platform.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is
    /// not Windows, Linux, or macOS.</exception>
    private IStrategy CreateStrategy()
    {
        if (_customOsStrategy != null) return _customOsStrategy;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return new MacStrategy();
        throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// Detects the current operating system platform.
    /// </summary>
    /// <returns>A <see cref="PlatformType"/> enum value representing the current operating system.</returns>
    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    /// <summary>
    /// Checks whether the specified version is a known failed version (recorded in the
    /// <c>UpgradeFail</c> environment variable).
    /// </summary>
    /// <param name="version">The version string to check.</param>
    /// <returns><c>true</c> if the version is not null and is less than or equal to the
    /// failed version recorded in the environment variable.</returns>
    /// <remarks>
    /// This feature prevents repeatedly attempting known-failed version updates.
    /// The <c>UpgradeFail</c> environment variable stores a previously failed version number;
    /// if the candidate version is less than or equal to that value, it is skipped.
    /// </remarks>
    private static bool CheckFail(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail)) return false;
        return new Version(fail) >= new Version(version);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="SilentPollOrchestrator"/>.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="Stop"/> to halt polling, then disposes the
    /// <see cref="CancellationTokenSource"/>.
    /// </remarks>
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// Configuration options for silent updates.
/// </summary>
/// <remarks>
/// <see cref="SilentOptions"/> controls the polling behavior and client restart policy
/// for silent updates.
/// </remarks>
public sealed class SilentOptions
{
    /// <summary>
    /// Gets or sets the polling interval. The default is 1 hour.
    /// </summary>
    /// <value>The time interval between update checks. The recommended minimum is
    /// 5 minutes to avoid excessive network requests.</value>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets a value indicating whether the client application should be
    /// automatically launched after an update completes.
    /// </summary>
    /// <value>
    /// <c>true</c> (default): Automatically start the client after the upgrade completes;
    /// <c>false</c>: The caller controls restart timing manually (suitable for maintenance
    /// windows, orchestrated deployments, etc.).
    /// </value>
    public bool LaunchClientAfterUpdate { get; set; } = true;
}
