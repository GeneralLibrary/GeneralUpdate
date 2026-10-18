using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.SelfCheck;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Upgrade-side update strategy. Receives process information passed from the client via encrypted IPC,
/// applies updates, and launches the main application.
/// </summary>
/// <remarks>
/// <para>
/// This strategy serves the <c>AppType.Upgrade</c> role and uses a two-layer strategy design:
/// the upper role strategy (this class) handles workflow orchestration,
/// while the lower OS-level strategy (<see cref="WindowsStrategy"/>, <see cref="LinuxStrategy"/>, <see cref="MacStrategy"/>)
/// handles platform-specific operations.
/// </para>
/// <para>
/// <b>Execution Flow:</b>
/// <list type="number">
///   <item><description>Receives the <see cref="UpdateContext"/> passed from the client via the <see cref="Create"/> method,
///   which contains already-downloaded update package paths, hash values, and other metadata.</description></item>
///   <item><description>Calls the <see cref="Hooks.IUpdateHooks.OnBeforeUpdateAsync"/> lifecycle hook,
///   allowing the caller to execute custom logic or cancel the operation before applying updates.</description></item>
///   <item><description>When external monitoring is enabled, publishes the immutable attempt request and waits for verified readiness before file application.</description></item>
///   <item><description>Delegates to the OS strategy to execute the update pipeline: processes each version through the
///   <c>Hash</c> (hash verification) → <c>Decompress</c> (extraction) → <c>Patch</c> (incremental patch) middleware chain.</description></item>
///   <item><description>Calls the <see cref="Hooks.IUpdateHooks.OnAfterUpdateAsync"/> hook to notify the caller that all updates have been applied.</description></item>
///   <item><description>Calls the <see cref="Hooks.IUpdateHooks.OnBeforeStartAppAsync"/> hook,
///   allowing the caller to perform additional operations before launching the main application
///   (such as setting executable permissions or preparing resource files).</description></item>
///   <item><description>Launches the main application and records its identity for an already-ready optional external monitor.
///   When self-check is enabled and NO external monitor is active, the updater instead runs a verified launch
///   (<c>ExecuteVerifiedLaunchAsync</c>): the application is observed inside the startup window and rolled back to
///   the previous version when judged unusable — the external monitor, when present, always owns the verdict.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Design Note:</b> The upgrade side does not perform version validation or download operations.
/// The client has already completed all network requests and downloads, passing results through process information.
/// The upgrade side applies updates and launches the application. Unmonitored status delivery is best-effort;
/// monitored terminal delivery is delegated to the independent Bowl host.
/// </para>
/// </remarks>
public class UpdateStrategy : IStrategy
{
    private UpdateContext? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;
    private int _reportType = 1; // 1=Upgrade(active poll), 2=Push(SignalR push)
    private IReadOnlyList<ISelfCheckRule> _selfCheckRules = Array.Empty<ISelfCheckRule>();
    private IDiagnosticsUploader? _diagnosticsUploader;

    /// <summary>
    /// Gets or sets the lifecycle hooks. Injected by the bootstrap to execute custom logic at key points in the update flow.
    /// </summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// Gets or sets the update status reporter. Injected by the bootstrap to report update progress and results to the server or caller.
    /// </summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.HttpUpdateReporter();

    /// <summary>
    ///     Whether to launch the Bowl helper process after a successful (verified or plain) launch.
    ///     Injected by the bootstrap from <see cref="Option.LaunchBowl"/> (default <c>false</c>):
    ///     since Bowl is independently installed, the main application launches it itself via
    ///     <c>BowlLauncher.EnsureRunningAsync</c>; this flag only keeps the legacy opt-in alive.
    /// </summary>
    public bool LaunchBowl { get; set; }

    /// <summary>
    ///     Sets the host-registered self-check rules (<c>AbstractBootstrap.SelfCheckRule&lt;T&gt;()</c>).
    ///     They run AFTER the built-in rules inside the verification window. The bootstrap calls
    ///     this before <see cref="ExecuteAsync"/>, so no deferred hand-off is needed.
    /// </summary>
    public void SetSelfCheckRules(IEnumerable<ISelfCheckRule> rules)
    {
        _selfCheckRules = rules?.ToArray() ?? Array.Empty<ISelfCheckRule>();
    }

    /// <summary>
    ///     Sets the optional diagnostics uploader used by <see cref="RollbackManager"/> for the
    ///     real-time upload after a self-check failure. Injected by the bootstrap when
    ///     <see cref="Option.DiagnosticsUploadEnabled"/> is on.
    /// </summary>
    public void SetDiagnosticsUploader(IDiagnosticsUploader? uploader) => _diagnosticsUploader = uploader;

    /// <summary>
    /// Sets a custom OS-level strategy (injected via <c>.Strategy&lt;T&gt;()</c>).
    /// When set, replaces the automatic platform detection logic in <see cref="ResolveOsStrategy"/>.
    /// </summary>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>
    /// Sets the report type for status reporting. The caller (ClientStrategy) should pass the same
    /// report type it used, so the server can distinguish push-triggered updates from active polls.
    /// </summary>
    /// <param name="reportType">1 = Upgrade (active poll), 2 = Push (SignalR push). Default is 1.</param>
    public void SetReportType(int reportType) => _reportType = reportType;

    /// <summary>
    /// Initializes the upgrade-side strategy. Receives the global configuration information passed from the client
    /// and resolves the strategy instance for the current operating system.
    /// </summary>
    /// <param name="parameter">Global configuration information containing update package paths, hash values, version information, etc.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is null.</exception>
    public void Create(UpdateContext parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
        if (_osStrategy is AbstractStrategy abs)
        {
            if (_pendingDiffPipeline != null) abs.DiffPipeline = _pendingDiffPipeline;
            abs.Reporter = this.Reporter;
            abs.ReportPackageResults = false;
        }
    }

    /// <summary>
    /// Executes the upgrade-side update flow. Follows the lifecycle order: pre-update hook, OS update pipeline,
    /// post-update hook, pre-start-app hook, and main application launch.
    /// </summary>
    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("UpdateStrategy not configured.");

        var ctx = BuildUpdateContext();
        try
        {
            _configInfo.ReportType = _reportType;
            var attempt = UpdateAttempt.Begin(_configInfo, "update");
            GeneralTracer.Debug("UpdateStrategy.ExecuteAsync start.");

            // Hooks: allow cancellation before applying updates
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("UpdateStrategy: update cancelled by OnBeforeUpdateAsync hook.");
                attempt.Record("cancelled");
                return;
            }

            if (_configInfo.Monitoring?.Enabled == true && _osStrategy is not AbstractStrategy)
                throw new InvalidOperationException("Bowl monitoring requires an AbstractStrategy with the application-start callback.");
            // Preflight hooks may cancel, but must not modify application files. Arm supervision before the apply boundary.
            await attempt.StartMonitoringAsync(_configInfo.UpdateVersions?.FirstOrDefault()?.RecordId ?? 0, _reportType);
            _osStrategy!.Create(_configInfo);
            attempt.BeforeFileChanges();

            // Apply MainApp updates -- Client already applied Upgrade packages, IPC only has MainApp versions
            var pipelineSucceeded = true;
            if (_configInfo.UpdateVersions?.Count > 0)
            {
                GeneralTracer.Info("UpdateStrategy: applying " + _configInfo.UpdateVersions.Count +
                                   " MainApp update(s).");
                await _osStrategy.ExecuteAsync();

                // Only advance the manifest version when every package was applied
                // successfully. AbstractStrategy catches per-package failures and
                // stops the chain, so ExecuteAsync() completing is not a
                // reliable success signal on its own.
                // For custom IStrategy implementations that don't expose
                // AllPackagesSucceeded, assume success (coalesce to true)
                // since no failure was signalled via an exception.
                pipelineSucceeded = (_osStrategy as AbstractStrategy)?.AllPackagesSucceeded ?? true;
                if (pipelineSucceeded)
                {
                    attempt.EnsureMonitorAlive();
                    WriteBackClientVersion();
                }
                else
                {
                    GeneralTracer.Warn("UpdateStrategy: one or more MainApp packages failed, " +
                                       "skipping manifest write and app launch.");
                }
            }
            else
            {
                GeneralTracer.Info("UpdateStrategy: no updates to apply, starting application directly.");
            }

            // When main app updates failed, do NOT launch the client — doing so would
            // restart it with old files, causing it to re-detect the update and loop.
            if (!pipelineSucceeded)
            {
                var failEx = (_osStrategy as AbstractStrategy)?.LastError ??
                    new InvalidOperationException("MainApp pipeline did not complete successfully.");
                attempt.RecordFailure(failEx);
                await SafeOnUpdateErrorAsync(ctx, failEx).ConfigureAwait(false);
                await SafeReportUpdateFailedAsync(ctx, failEx).ConfigureAwait(false);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(failEx, failEx.Message));
                return;
            }

            // Verified launch: when self-check is on and there is a freshly applied main-app
            // update to verify, this path owns manifest write-back, marker lifecycle, reporting,
            // app launch and updater exit — the plain launch below is skipped entirely.
            // LaunchClientAfterUpdate=false (maintenance/silent staging) has no launchable
            // process to verify and keeps the legacy path.
            //
            // Precedence: an ACTIVE external monitor is the post-update arbiter (protocol v1:
            // "Update must suppress its own rollback"), so self-check defers instead of
            // running a second verdict over the same launch.
            if (IsVerifiedLaunchRequested())
            {
                if (attempt.MonitoringActive)
                {
                    GeneralTracer.Warn(
                        "UpdateStrategy: SelfCheckEnabled and Monitoring.Enabled are both on — the external monitor owns the verdict, self-check deferred.");
                }
                else
                {
                    attempt.EnsureMonitorAlive();
                    attempt.Record("filesApplied");
                    await ExecuteVerifiedLaunchAsync(ctx).ConfigureAwait(false);
                    return;
                }
            }

            // Hooks: after all updates applied
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);

            attempt.EnsureMonitorAlive();
            attempt.Record("filesApplied");

            // Hooks: before starting main app (e.g. chmod +x on Linux/macOS)
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            // The external monitor is already ready; only launch the application here.
            // Skip if silent mode requested no-launch (e.g. maintenance windows).
            if (_configInfo.LaunchClientAfterUpdate)
            {
                if (_osStrategy is AbstractStrategy abs2)
                {
                    abs2.LaunchAppName = _configInfo.MainAppName;
                    // The external monitor owns the post-update verdict when active; the
                    // deprecated updater-side watchdog launch (Option.LaunchBowl, default
                    // false) only applies to unmonitored runs — never both arbiters.
#pragma warning disable CS0618 // Type or member is obsolete
                    abs2.LaunchBowl = !attempt.MonitoringActive && LaunchBowl;
#pragma warning restore CS0618 // Type or member is obsolete
                    abs2.OnAppStarted = async process =>
                    {
                        // The OS strategy awaits this before exit: Bowl can recover the exact application identity from disk.
                        attempt.EnsureMonitorAlive();
                        if (attempt.MonitoringActive && _configInfo.Monitoring!.VerifyLaunch)
                            attempt.Record("awaitingHealth", application: ProcessIdentity.From(process));
                        else
                        {
                            // Without launch observation, completion means applied files and successful process creation only.
                            attempt.Record("completed");
                            await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
                        }
                    };
                }

                attempt.EnsureMonitorAlive();
                attempt.Record("launching");
                await _osStrategy.StartAppAsync();
                if (_osStrategy is not AbstractStrategy)
                {
                    attempt.Record("completed");
                    await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
                }
            }
            else
            {
                GeneralTracer.Info("UpdateStrategy: LaunchClientAfterUpdate=false, skipping app launch.");
                attempt.EnsureMonitorAlive();
                attempt.Record("completed");
                await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Persist before invoking user hooks, which may throw or terminate the updater themselves.
            _configInfo.Attempt?.RecordFailure(ex,
                _configInfo.Attempt.Stage == "launching" ? "launchFailure" : "updateFailure");
            await SafeOnUpdateErrorAsync(ctx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(ctx, ex).ConfigureAwait(false);
            GeneralTracer.Error("UpdateStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
        finally
        {
            // Release only our handle; the independent monitor may need to outlive this updater.
            _configInfo.Attempt?.Dispose();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Verified launch (self-check) — updater is the arbiter inside the
    //  observation window; on failure it rolls back to the previous version.
    //  See docs/self-check-design.md §6.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     True when the current run must go through the verification flow: switch on, a
    ///     launchable main app update was applied, and the caller wants the app launched.
    /// </summary>
    private bool IsVerifiedLaunchRequested() =>
        _configInfo!.SelfCheckEnabled == true
        && _configInfo.LaunchClientAfterUpdate
        && _configInfo.UpdateVersions?.Count > 0;

    /// <summary>
    ///     Launches the updated main application under observation. Success: verify marker is
    ///     removed and the updater exits leaving the app running. Failure: the process is shut
    ///     down (by the launch rule), the tree is rolled back to the previous version and the
    ///     old application is relaunched — the user always keeps a working program.
    /// </summary>
    private async Task ExecuteVerifiedLaunchAsync(Hooks.HookContext ctx)
    {
        var config = _configInfo!;
        var installPath = config.InstallPath;
        var targetVersion = config.UpdateVersions?.LastOrDefault()?.Version ?? config.LastVersion;
        var previousVersion = config.ClientVersion;

        GeneralTracer.Info($"UpdateStrategy: verified launch starting (target={targetVersion}, previous={previousVersion}).");

        // The manifest was already advanced by the shared pipeline flow (WriteBackClientVersion
        // runs right after pipeline success); a failed verification rolls it back below.

        // Verify marker + stale-ack cleanup. Ack files MUST be removed before the app
        // starts: a leftover verify-ok from an earlier session would satisfy this session's
        // strict check.
        UpdateVerificationMarker.Delete(installPath);
        UpdateVerificationMarker.DeleteAckFiles(installPath);
        UpdateVerificationMarker.Write(installPath, targetVersion, previousVersion, config.BackupDirectory);

        Process? process = null;
        try
        {
            // Post-update hook (parity with the plain launch path) + pre-start hook
            // (e.g. chmod +x on Linux/macOS).
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            // Start the app WITHOUT Bowl and WITHOUT updater self-exit — the updater must
            // stay alive to render the verdict. The observation window deliberately does not
            // run Bowl, avoiding two arbiters for the same crash.
            process = StartProcessForVerification(config.MainAppName);
            // Observe the window with built-in + host-registered rules.
            var report = await RunSelfCheckAsync(process, targetVersion, previousVersion).ConfigureAwait(false);
            EventManager.Instance.Dispatch(this,
                new SelfCheckCompletedEventArgs(report, targetVersion, previousVersion));

            if (report.Passed)
            {
                UpdateVerificationMarker.Delete(installPath);
                UpdateVerificationMarker.DeleteAckFiles(installPath);
                // Keep the shared evidence chain consistent with the plain launch path.
                config.Attempt?.Record("completed");
                await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
                LaunchBowlCompat();
                GeneralTracer.Info("UpdateStrategy: self-check passed — verified launch completed.");
            }
            else
            {
                await HandleFailedVerificationAsync(ctx, report, targetVersion, previousVersion)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // An infrastructure failure inside the verification flow (most commonly: the app
            // could not be started at all) IS a failed verification — roll back, never leave
            // the installation on the broken version with the manifest already advanced.
            GeneralTracer.Error("UpdateStrategy: verified launch failed.", ex);
            var report = new SelfCheckReport(false, new[]
            {
                new SelfCheckRuleResult(nameof(ProcessLaunchRule), false, $"verified launch failed: {ex.Message}")
            });
            EventManager.Instance.Dispatch(this,
                new SelfCheckCompletedEventArgs(report, targetVersion, previousVersion));
            await HandleFailedVerificationAsync(ctx, report, targetVersion, previousVersion)
                .ConfigureAwait(false);
        }
        finally
        {
            process?.Dispose();
            // Both outcomes end the updater; the (old or new) main app runs independently.
            GeneralTracer.Dispose();
            await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Starts the target executable and returns its process handle (OS-strategy path resolution).</summary>
    private Process StartProcessForVerification(string appName)
    {
        if (_osStrategy is AbstractStrategy abs)
            return abs.StartProcessForVerification(appName);

        // Custom IStrategy: fall back to a direct lookup under the install root.
        var appPath = Path.Combine(_configInfo!.InstallPath, appName);
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"Can't find the app {appName}!");
        var process = Process.Start(appPath);
        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {appPath}");
        GeneralTracer.Info($"UpdateStrategy: verification target started: {appPath} (PID: {process.Id}).");
        return process;
    }

    /// <summary>Builds the rule set and runs one self-check session over the launched process.</summary>
    private async Task<SelfCheckReport> RunSelfCheckAsync(
        Process process, string? targetVersion, string? previousVersion)
    {
        var config = _configInfo!;
        var windowSeconds = config.SelfCheckStartupWindowSeconds > 0 ? config.SelfCheckStartupWindowSeconds : 30;
        var rules = new List<ISelfCheckRule>
        {
            // Decisive rule first: a dead process makes the remaining checks pointless.
            new ProcessLaunchRule(process, TimeSpan.FromSeconds(windowSeconds), config.SelfCheckRequireAck == true),
            new FilePresenceRule(config.MainAppName)
        };
        rules.AddRange(_selfCheckRules);

        var runner = new SelfCheckRunner(rules);
        return await runner.RunAsync(new SelfCheckContext(
            config.InstallPath,
            config.MainAppName,
            targetVersion,
            previousVersion,
            config.BackupDirectory,
            AppType.Upgrade,
            UpdateVerificationMarker.GetPath(config.InstallPath)),
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Failure path: rollback (files, manifest, UpgradeFail marker, Failure report) and
    ///     relaunch of the old version. Never throws — the updater must exit in an orderly way
    ///     even when parts of the recovery fail.
    /// </summary>
    private async Task HandleFailedVerificationAsync(
        Hooks.HookContext ctx, SelfCheckReport report, string? targetVersion, string? previousVersion)
    {
        var config = _configInfo!;
        try
        {
            GeneralTracer.Warn($"UpdateStrategy: self-check failed — rolling back. Reason: {report.FirstFailureReason}");

            // Evidence chain: a failed self-check is a health-check failure in protocol-v1 terms.
            config.Attempt?.RecordFailure(
                new InvalidOperationException(report.FirstFailureReason ?? "self-check failed"),
                "healthCheckFailure");

            var rollback = new RollbackManager(config.InstallPath, Reporter, _diagnosticsUploader);
            var result = await rollback.RollbackAsync(new RollbackRequest(
                FailedVersion: targetVersion ?? "unknown",
                PreviousVersion: previousVersion,
                BackupDirectory: config.BackupDirectory,
                RecordId: config.UpdateVersions?.FirstOrDefault()?.RecordId ?? 0,
                ReportType: _reportType,
                Reason: report.FirstFailureReason ?? "self-check failed"),
                CancellationToken.None).ConfigureAwait(false);

            // Hook + event on top of the report RollbackManager already sent.
            var failEx = new InvalidOperationException(
                $"Self-check failed, rolled back from {targetVersion}: {report.FirstFailureReason}");
            await SafeOnUpdateErrorAsync(ctx, failEx).ConfigureAwait(false);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(failEx, failEx.Message));

            if (!string.IsNullOrEmpty(result.ActiveVersion))
            {
                StartPreviousVersion(config.MainAppName);
            }
            else
            {
                // Restoring failed — the install tree may be inconsistent. Do NOT relaunch the
                // app on a possibly broken state; error reporting + event already fired above.
                GeneralTracer.Error(
                    "UpdateStrategy: rollback did not produce a consistent tree — previous version NOT relaunched (operator inspection required).");
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("UpdateStrategy: failed-verification handling errored.", ex);
        }
    }

    /// <summary>Relaunches the application after the tree was restored to the previous version.</summary>
    private void StartPreviousVersion(string appName)
    {
        try
        {
            if (_osStrategy is AbstractStrategy abs)
            {
                abs.StartProcess(appName);
                return;
            }

            // Custom IStrategy without AbstractStrategy helpers: fall back to a direct lookup.
            var appPath = Path.Combine(_configInfo!.InstallPath, appName);
            if (!File.Exists(appPath))
                throw new FileNotFoundException($"Can't find the app {appName}!");
            using var process = Process.Start(appPath);
            GeneralTracer.Info($"UpdateStrategy: previous version relaunched: {appPath}.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("UpdateStrategy: relaunching the previous version failed.", ex);
        }
    }

    /// <summary>
    ///     Legacy opt-in: launch the (independently installed) Bowl binary after a verified
    ///     pass. Default OFF — the main application self-launches Bowl via
    ///     <c>BowlLauncher.EnsureRunningAsync</c>; kept for one compatibility cycle.
    /// </summary>
    private void LaunchBowlCompat()
    {
        if (!LaunchBowl) return;
        try
        {
            var bowlName = _configInfo!.Bowl;
            if (string.IsNullOrWhiteSpace(bowlName)) return;
            var bowlPath = Path.Combine(_configInfo.InstallPath, bowlName);
            if (!File.Exists(bowlPath)) return;
            using var bowlProcess = Process.Start(bowlPath);
            GeneralTracer.Info($"UpdateStrategy: Bowl launched (PID: {bowlProcess?.Id}).");
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"UpdateStrategy: Bowl launch failed: {ex.Message}");
        }
    }

    private DiffPipeline? _pendingDiffPipeline;

    /// <summary>
    /// Sets the differential patch pipeline on the underlying OS-level strategy for parallel patch application.
    /// </summary>
    /// <param name="diffPipeline">The differential pipeline instance. If <c>null</c>, clears the pending pipeline.</param>
    public void SetDiffPipeline(DiffPipeline? diffPipeline)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.DiffPipeline = diffPipeline;
        else
            _pendingDiffPipeline = diffPipeline;
    }

    /// <summary>
    /// Starts the main application. Delegates to the underlying OS strategy for platform-specific application launch logic.
    /// </summary>
    public async Task StartAppAsync()
    {
        if (_osStrategy != null)
            await _osStrategy.StartAppAsync();
    }

    #region Helpers

    private IStrategy ResolveOsStrategy()
        => OsStrategyResolver.Resolve(_customOsStrategy);

    private Hooks.HookContext BuildUpdateContext()
    {
        return new Hooks.HookContext(
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? AppDomain.CurrentDomain.BaseDirectory,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.Upgrade
        );
    }

    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.HookContext ctx)
    {
        try { return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}"); return false; }
    }

    private async Task SafeOnAfterUpdateAsync(Hooks.HookContext ctx)
    {
        try { await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeOnBeforeStartAppAsync(Hooks.HookContext ctx)
    {
        try { await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeOnUpdateErrorAsync(Hooks.HookContext ctx, Exception error)
    {
        try { await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateAppliedAsync(Hooks.HookContext ctx)
    {
        try
        {
            var recordId = _configInfo?.UpdateVersions?.FirstOrDefault()?.RecordId ?? 0;
            if (_configInfo?.Attempt != null)
                await _configInfo.Attempt.ReportAsync(Reporter, recordId, (int)UpdateStatus.Success, _reportType).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}");
        }
    }

    private async Task SafeReportUpdateFailedAsync(Hooks.HookContext ctx, Exception error)
    {
        try
        {
            var recordId = _configInfo?.UpdateVersions?.FirstOrDefault()?.RecordId ?? 0;
            _configInfo?.Attempt?.RecordFailure(error);
            if (_configInfo?.Attempt != null)
                await _configInfo.Attempt.ReportAsync(Reporter, recordId, (int)UpdateStatus.Failure, _reportType).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}");
        }
    }

    /// <summary>
    /// After the main-app update pipeline completes, writes the new <c>ClientVersion</c>
    /// back to <c>generalupdate.manifest.json</c> in the client's install directory so the
    /// next poll cycle starts from the correct version.
    /// </summary>
    private void WriteBackClientVersion()
    {
        // Use the latest version from the applied update list; fall back to LastVersion
        // (which covers the single-package / full-update case).
        var latestVersion = _configInfo?.UpdateVersions?.LastOrDefault()?.Version
                            ?? _configInfo?.LastVersion;
        if (string.IsNullOrEmpty(latestVersion)) return;

        try
        {
            ManifestInfo.TryUpdateVersion(
                _configInfo!.InstallPath,
                clientVersion: latestVersion);
            GeneralTracer.Info(
                $"UpdateStrategy: ClientVersion updated to {latestVersion} in manifest.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn(
                $"UpdateStrategy: failed to write back ClientVersion: {ex.Message}");
            // A stale manifest can trigger repeated updates; record its path and fail instead of claiming completion.
            ex.Data["UpdateStage"] = "manifest";
            ex.Data["UpdateFailedPath"] = System.IO.Path.Combine(_configInfo!.InstallPath, ManifestInfo.FileName);
            throw;
        }
    }

    #endregion
}