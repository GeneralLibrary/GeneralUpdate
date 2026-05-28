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
/// 静默更新轮询协调器 —— 定期检查更新，在后台静默下载更新包，
/// 并在进程退出时延迟执行应用程序更新。
/// </summary>
/// <remarks>
/// <para>
/// 遵循与 <see cref="ClientUpdateStrategy"/> 相同的 AppType 分流模式：
/// </para>
/// <list type="bullet">
///   <item><description><b>Upgrade（AppType=2）</b>：更新包在轮询周期内就地应用，
///   它们作用于 <c>UpdatePath</c> 而非正在运行的应用的 <c>InstallPath</c>。</description></item>
///   <item><description><b>Client（AppType=1）</b>：更新包被延迟 —— 存储在 <c>ProcessInfo</c> 中，
///   在进程退出时通过 IPC 交给 Upgrade 进程执行实际更新。</description></item>
/// </list>
/// <para>
/// 核心工作流程：
/// <list type="number">
///   <item><description>启动后台轮询循环，按 <see cref="SilentOptions.PollInterval"/> 间隔检查服务器是否有新版本。</description></item>
///   <item><description>发现新版本后，在后台静默下载所有更新包。</description></item>
///   <item><description>Upgrade 包立即应用，Client 包暂存到 <c>ProcessInfo</c> 中。</description></item>
///   <item><description>监听 <see cref="AppDomain.ProcessExit"/> 事件，在进程退出时启动升级程序。</description></item>
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
    /// 初始化 <see cref="SilentPollOrchestrator"/> 的新实例。
    /// </summary>
    /// <param name="configInfo">全局配置信息，包含更新 URL、版本号、产品信息等。</param>
    /// <param name="options">静默更新选项，包含轮询间隔等配置。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="configInfo"/> 或 <paramref name="options"/> 为 <c>null</c> 时抛出。</exception>
    public SilentPollOrchestrator(GlobalConfigInfo configInfo, SilentOptions options)
    {
        _configInfo = configInfo ?? throw new ArgumentNullException(nameof(configInfo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 设置更新生命周期钩子（链式调用方法）。
    /// </summary>
    /// <param name="hooks">实现 <see cref="IUpdateHooks"/> 接口的钩子实例，用于在更新各阶段插入自定义逻辑。</param>
    /// <returns>当前 <see cref="SilentPollOrchestrator"/> 实例，支持链式调用。</returns>
    public SilentPollOrchestrator WithHooks(IUpdateHooks? hooks) { _hooks = hooks; return this; }

    /// <summary>
    /// 设置更新进度报告器（链式调用方法）。
    /// </summary>
    /// <param name="reporter">实现 <see cref="IUpdateReporter"/> 接口的报告器实例。</param>
    /// <returns>当前 <see cref="SilentPollOrchestrator"/> 实例，支持链式调用。</returns>
    public SilentPollOrchestrator WithReporter(IUpdateReporter? reporter) { _reporter = reporter; return this; }

    /// <summary>
    /// 设置自定义操作系统策略（链式调用方法），用于覆盖默认的平台特定更新策略（Windows/Linux/macOS）。
    /// </summary>
    /// <param name="strategy">自定义的操作系统策略实例。</param>
    /// <returns>当前 <see cref="SilentPollOrchestrator"/> 实例，支持链式调用。</returns>
    public SilentPollOrchestrator WithOsStrategy(IStrategy? strategy) { _customOsStrategy = strategy; return this; }

    /// <summary>
    /// 启动静默轮询协调器，开始后台检查更新。
    /// </summary>
    /// <returns>表示启动操作的任务。注意：此任务在轮询循环启动后立即完成，不代表轮询循环已结束。</returns>
    /// <remarks>
    /// <para>
    /// 启动流程：
    /// <list type="number">
    ///   <item><description>注册 <see cref="AppDomain.CurrentDomain.ProcessExit"/> 事件处理器。</description></item>
    ///   <item><description>创建 <see cref="CancellationTokenSource"/>。</description></item>
    ///   <item><description>在后台任务中启动轮询循环 <see cref="PollLoopAsync"/>。</description></item>
    ///   <item><description>为轮询任务附加异常处理器以记录未处理的异常。</description></item>
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
    /// 停止静默轮询协调器，取消正在进行的轮询和下载操作。
    /// </summary>
    /// <remarks>
    /// 调用此方法会：
    /// <list type="bullet">
    ///   <item><description>取消后台轮询循环。</description></item>
    ///   <item><description>从 <see cref="AppDomain.CurrentDomain.ProcessExit"/> 注销事件处理器。</description></item>
    /// </list>
    /// 注意：此方法不会取消已经在进行的下载任务，但会阻止新的轮询周期开始。
    /// </remarks>
    public void Stop()
    {
        _cts?.Cancel();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    }

    /// <summary>
    /// 后台轮询循环，按照配置的间隔定期检查更新并执行更新准备。
    /// </summary>
    /// <param name="token">取消令牌，用于停止轮询循环。</param>
    /// <returns>表示轮询循环的任务。当准备完成或取消时结束。</returns>
    /// <remarks>
    /// 循环逻辑：
    /// <list type="number">
    ///   <item><description>调用 <see cref="PrepareUpdateIfNeededAsync"/> 检查并准备更新。</description></item>
    ///   <item><description>如果准备完成（<c>_prepared == 1</c>），退出循环。</description></item>
    ///   <item><description>否则等待 <see cref="SilentOptions.PollInterval"/> 后再次检查。</description></item>
    /// </list>
    /// 循环中的单个异常不会终止整个循环，而是记录错误后继续下一次轮询。
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
    /// 检查是否有可用更新，若有则执行完整的更新准备工作（下载、备份、应用 Upgrade 包、准备 ProcessInfo）。
    /// </summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// <para>
    /// 这是静默更新的核心方法，执行完整的工作流：
    /// </para>
    /// <list type="number">
    ///   <item><description>通过 <see cref="HttpDownloadSource"/> 向服务器查询可用更新。</description></item>
    ///   <item><description>使用 <see cref="DownloadPlanBuilder"/> 构建下载计划。</description></item>
    ///   <item><description>检查版本失败记录，跳过已知失败的版本。</description></item>
    ///   <item><description>调用钩子（hooks）的 <c>OnBeforeUpdateAsync</c> 允许外部取消更新。</description></item>
    ///   <item><description>初始化黑名单，创建临时目录，执行备份。</description></item>
    ///   <item><description>使用 <see cref="Download.Orchestrators.DefaultDownloadOrchestrator"/> 下载所有更新包。</description></item>
    ///   <item><description>按 AppType 分流：Upgrade 包立即通过策略执行，Client 包暂存到 ProcessInfo。</description></item>
    ///   <item><description>调用钩子的 <c>OnAfterUpdateAsync</c> 通知更新完成。</description></item>
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

        // Split packages by AppType — mirrors ClientUpdateStrategy
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

        // Apply Upgrade packages in place — safe because they target UpdatePath
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
    /// 进程退出事件处理器 —— 当进程即将退出时，将准备好的 Client 包信息通过 IPC 发送给升级程序并启动升级。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    /// <remarks>
    /// <para>
    /// 此方法在 <see cref="AppDomain.CurrentDomain.ProcessExit"/> 事件触发时被调用。
    /// 仅在更新已准备好（<c>_prepared == 1</c>）且升级程序尚未启动时执行。
    /// </para>
    /// <para>
    /// 执行流程：
    /// <list type="number">
    ///   <item><description>通过 <c>Interlocked.Exchange</c> 确保升级程序只启动一次。</description></item>
    ///   <item><description>解析升级程序的路径（优先使用 UpdatePath，回退到 InstallPath）。</description></item>
    ///   <item><description>如果存在 Client 包，通过 <see cref="EncryptedFileProcessInfoProvider"/> 发送加密的 ProcessInfo。</description></item>
    ///   <item><description>启动升级进程（使用 ShellExecute 以提升权限）。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _prepared) != 1 || Interlocked.Exchange(ref _updaterStarted, 1) == 1) return;

        try
        {
            // Resolve updater location — prefers UpdatePath, falls back to InstallPath
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
    /// 尝试通过钩子和报告器报告更新错误。
    /// </summary>
    /// <param name="ctx">更新上下文，包含应用信息。</param>
    /// <param name="ex">发生的异常。</param>
    /// <remarks>
    /// 此方法会尽力调用 <c>_hooks.OnUpdateErrorAsync</c> 和 <c>_reporter.ReportAsync</c>，
    /// 即使这些调用本身抛出异常也不会传播（仅记录警告日志），确保不会因错误报告失败而掩盖原始错误。
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
    /// 初始化 <see cref="StorageManager.BlackListMatcher"/>，使用配置中的黑名单列表。
    /// 如果配置中的列表为空，则使用 <see cref="BlackListDefaults"/> 中的默认值。
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
    /// 根据当前操作系统创建对应的更新策略实例。
    /// 如果设置了自定义策略（<c>_customOsStrategy</c>），则优先使用。
    /// </summary>
    /// <returns>与当前平台匹配的 <see cref="IStrategy"/> 实现。</returns>
    /// <exception cref="PlatformNotSupportedException">当操作系统不是 Windows、Linux 或 macOS 时抛出。</exception>
    private IStrategy CreateStrategy()
    {
        if (_customOsStrategy != null) return _customOsStrategy;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return new MacStrategy();
        throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// 检测当前运行的操作系统平台。
    /// </summary>
    /// <returns><see cref="PlatformType"/> 枚举值，表示当前操作系统。</returns>
    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    /// <summary>
    /// 检查指定版本是否为已知的失败版本（已记录在环境变量 <c>UpgradeFail</c> 中）。
    /// </summary>
    /// <param name="version">要检查的版本号字符串。</param>
    /// <returns>如果版本号不为空且小于等于环境变量中记录的失败版本号则返回 <c>true</c>。</returns>
    /// <remarks>
    /// 此功能用于避免反复尝试已知失败的版本更新。
    /// 通过环境变量 <c>UpgradeFail</c> 记录曾经失败的版本号，如果待更新的版本号小于等于该值则跳过。
    /// </remarks>
    private static bool CheckFail(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail)) return false;
        return new Version(fail) >= new Version(version);
    }

    /// <summary>
    /// 释放由 <see cref="SilentPollOrchestrator"/> 占用的资源。
    /// </summary>
    /// <remarks>
    /// 调用 <see cref="Stop"/> 停止轮询，然后释放 <see cref="CancellationTokenSource"/>。
    /// </remarks>
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// 静默更新的配置选项。
/// </summary>
/// <remarks>
/// 通过 <see cref="SilentOptions"/> 可以控制静默更新的轮询行为和客户端重启策略。
/// </remarks>
public sealed class SilentOptions
{
    /// <summary>
    /// 获取或设置轮询间隔时间。默认为 1 小时。
    /// </summary>
    /// <value>两次更新检查之间的时间间隔。最小建议值不应低于 5 分钟以避免频繁的网络请求。</value>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// 获取或设置一个值，指示更新完成后是否自动启动客户端应用程序。
    /// </summary>
    /// <value>
    /// <c>true</c>（默认值）：升级完成后自动启动客户端；
    /// <c>false</c>：由调用方手动控制重启时机（适用于维护窗口、编排式发布等场景）。
    /// </value>
    public bool LaunchClientAfterUpdate { get; set; } = true;
}
