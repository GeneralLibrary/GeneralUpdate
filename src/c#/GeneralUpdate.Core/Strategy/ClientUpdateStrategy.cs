using GeneralUpdate.Core.Differential;
using GeneralUpdate.Differential.Abstractions;
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
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Network;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// 客户端更新策略。负责与服务器校验版本、下载更新包、构造升级端所需的进程信息并启动升级进程。
/// </summary>
/// <remarks>
/// <para>本策略是 <c>AppType.Client</c> 角色策略，完整更新流程如下：</para>
/// <para>
/// 1. <b>版本校验</b>：通过 <see cref="Download.Abstractions.IDownloadSource.ListAsync"/> 向服务器发送版本信息，
///    根据返回值设置 <c>IsMainUpdate</c> 和 <c>IsUpgradeUpdate</c> 标志，确定更新场景。
/// </para>
/// <para>
/// 2. <b>事件分发</b>：构造 <see cref="UpdateInfoEventArgs"/> 并通过 <c>EventManager</c> 分发到订阅者。
/// </para>
/// <para>
/// 3. <b>跳过检查</b>：如果非强制更新且预检查回调返回 <c>true</c>，则跳过本次更新。
/// </para>
/// <para>
/// 4. <b>更新前钩子</b>：调用 <c>Hooks.OnBeforeUpdateAsync</c>，允许外部逻辑取消本次更新。
/// </para>
/// <para>
/// 5. <b>备份</b>：将当前安装目录备份到临时目录（可通过 <c>BackupEnabled</c> 禁用）。
/// </para>
/// <para>
/// 6. <b>下载</b>：通过下载编排器下载所有更新包，支持自定义策略、执行器和处理管道。
/// </para>
/// <para>
/// 7. <b>场景分发</b>：根据服务器校验结果执行不同流程：
///    - <c>UpgradeOnly</c>：仅原地应用升级程序更新包；
///    - <c>MainOnly</c>：序列化主程序更新包为 <c>ProcessInfo</c> 并通过 IPC 发送，启动升级进程；
///    - <c>Both</c>：先应用升级程序更新包，再发送主程序信息并启动升级进程。
/// </para>
/// <para>
/// 8. <b>升级应用</b>：通过 <c>ApplyUpgradePackagesAsync</c> 调用 OS 策略管道原地应用增量/全量更新包。
/// </para>
/// <para>
/// 9. <b>IPC 通信</b>：通过 <c>SendProcessIpc</c> 将主程序更新信息序列化为 JSON 并通过加密 IPC 发送给升级端。
/// </para>
/// <para>
/// 10. <b>启动升级进程</b>：通过 <c>LaunchUpgradeProcessAsync</c> 委托 OS 策略启动升级端可执行文件。</para>
/// <para>
/// 本类采用<b>双层策略设计</b>：<c>ClientUpdateStrategy</c> 作为"角色"策略，负责编排更新流程；
/// 内部组合一个 OS 特定的平台策略（<see cref="WindowsStrategy"/>、<see cref="LinuxStrategy"/>、<see cref="MacStrategy"/>）
/// 处理平台相关操作（文件操作、进程管理、安装路径判断等）。
/// </para>
/// </remarks>
public class ClientUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;
    private IStrategy? _customOsStrategy;
    private Func<UpdateInfoEventArgs, bool>? _updatePrecheck;
    private Download.Abstractions.IDownloadOrchestrator? _orchestrator;
    private Download.Abstractions.IDownloadPolicy? _customDownloadPolicy;
    private Download.Abstractions.IDownloadExecutor? _customDownloadExecutor;
    private Func<string?, Download.Abstractions.IDownloadPipeline>? _customDownloadPipelineFactory;
    private int _mainRecordId;

    /// <summary>
    /// 由服务器验证结果确定的更新场景，表示需要更新的目标。
    /// </summary>
    private enum UpdateScenario
    {
        None,
        UpgradeOnly,
        MainOnly,
        Both
    }

    /// <summary>
    /// 获取或设置更新生命周期钩子。由引导程序通过 <c>.Hooks&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <value>实现了 <c>IUpdateHooks</c> 的钩子实例。默认为 <c>NoOpUpdateHooks</c>（无操作实现）。</value>
    /// <remarks>
    /// 钩子回调在更新流程的关键节点被安全调用（包裹在 try-catch 中），单个钩子失败不会阻断流程。
    /// 参见 <see cref="SafeOnBeforeUpdateAsync"/>、<see cref="SafeOnAfterUpdateAsync"/> 等方法。
    /// </remarks>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// 获取或设置更新状态报告器。由引导程序通过 <c>.UpdateReporter&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <value>实现了 <c>IUpdateReporter</c> 的报告器实例。默认为 <c>NoOpUpdateReporter</c>（无操作实现）。</value>
    /// <remarks>
    /// 报告器在更新开始、下载完成、更新应用成功或失败时向服务器上报状态。
    /// 所有上报调用都包裹在 try-catch 中，上报失败不会阻断流程。
    /// 参见 <see cref="SafeReportUpdateStartedAsync"/>、<see cref="SafeReportDownloadCompletedAsync"/> 等方法。
    /// </remarks>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

    /// <summary>
    /// 获取或设置下载数据源。由引导程序通过 <c>.DownloadSource&lt;T&gt;()</c> 注册注入，
    /// 或在引导程序中通过 <c>HubConfig</c> 配置。
    /// </summary>
    /// <value>实现了 <c>IDownloadSource</c> 的数据源实例。为 <c>null</c> 时默认使用 <c>HttpDownloadSource</c>。</value>
    /// <remarks>
    /// 在 <see cref="ExecuteStandardWorkflowAsync"/> 中，如果此属性为 <c>null</c>，
    /// 则会根据 <c>GlobalConfigInfo</c> 中的 <c>UpdateUrl</c>、版本号等信息自动创建 <c>HttpDownloadSource</c> 实例。
    /// </remarks>
    public Download.Abstractions.IDownloadSource? DownloadSource { get; set; }

    /// <summary>
    /// 初始化 <see cref="ClientUpdateStrategy"/> 的新实例。
    /// </summary>
    /// <remarks>
    /// 默认构造函数。所有属性使用默认值（无操作钩子、无操作报告器）。
    /// 下载编排器默认为 <c>null</c>，将在 <see cref="ExecuteStandardWorkflowAsync"/> 中创建默认的 <c>DefaultDownloadOrchestrator</c>。
    /// 策略实例需要通过 <see cref="Create"/> 方法传入 <see cref="GlobalConfigInfo"/> 完成初始化。
    /// </remarks>
    public ClientUpdateStrategy() { }

    /// <summary>
    /// 使用自定义下载编排器初始化 <see cref="ClientUpdateStrategy"/> 的新实例。
    /// </summary>
    /// <param name="orchestrator">自定义下载编排器实例，用于接管批量下载流程。如果为 <c>null</c>，则使用默认编排器。</param>
    /// <remarks>
    /// 通过此构造函数传入的编排器优先级高于通过 <see cref="SetOrchestrator"/> 方法设置的值。
    /// </remarks>
    public ClientUpdateStrategy(Download.Abstractions.IDownloadOrchestrator? orchestrator)
        => _orchestrator = orchestrator;

    /// <summary>
    /// 设置自定义 OS 级别策略。由引导程序通过 <c>.Strategy&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <param name="strategy">自定义 OS 策略实例。设置后将替换 <see cref="ResolveOsStrategy"/> 中的自动平台探测。</param>
    /// <remarks>
    /// 当设置此策略后，<see cref="ResolveOsStrategy"/> 将跳过 <c>RuntimeInformation.IsOSPlatform</c> 的检查，
    /// 直接使用此处注入的策略。适用于需要完全自定义平台行为的场景。
    /// </remarks>
    public void SetOsStrategy(IStrategy? strategy) => _customOsStrategy = strategy;

    /// <summary>
    /// 设置自定义下载编排器。由引导程序通过 <c>.DownloadOrchestrator&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <param name="orchestrator">自定义下载编排器实例。为 <c>null</c> 时使用默认的 <c>DefaultDownloadOrchestrator</c>。</param>
    /// <remarks>
    /// 设置后将在 <see cref="ExecuteStandardWorkflowAsync"/> 的下载阶段使用此编排器。
    /// 如果已通过构造函数注入编排器，则构造函数中的值优先。
    /// 自定义编排器完全接管下载流程，<see cref="SetDownloadPolicy"/>、<see cref="SetDownloadExecutor"/> 和
    /// <see cref="SetDownloadPipelineFactory"/> 的设置将被忽略。
    /// </remarks>
    public void SetOrchestrator(Download.Abstractions.IDownloadOrchestrator? orchestrator) => _orchestrator = orchestrator;

    /// <summary>
    /// 设置自定义下载重试策略。由引导程序通过 <c>.DownloadPolicy&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <param name="policy">自定义下载策略实例。为 <c>null</c> 时使用默认重试行为。</param>
    /// <remarks>
    /// 仅在使用默认下载编排器（<c>DefaultDownloadOrchestrator</c>）时生效。
    /// 如果同时设置了自定义编排器（<see cref="SetOrchestrator"/>），则此策略被忽略。
    /// 下载策略控制每次失败后的等待时间和最大重试次数。
    /// </remarks>
    public void SetDownloadPolicy(Download.Abstractions.IDownloadPolicy? policy) => _customDownloadPolicy = policy;

    /// <summary>
    /// 设置自定义单文件下载执行器。由引导程序通过 <c>.DownloadExecutor&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <param name="executor">自定义下载执行器实例。为 <c>null</c> 时使用默认的 HTTP 下载执行器。</param>
    /// <remarks>
    /// 仅在使用默认下载编排器（<c>DefaultDownloadOrchestrator</c>）时生效。
    /// 如果同时设置了自定义编排器（<see cref="SetOrchestrator"/>），则此执行器被忽略。
    /// 可用于实现 FTP、SFTP 或私有协议的文件下载。
    /// </remarks>
    public void SetDownloadExecutor(Download.Abstractions.IDownloadExecutor? executor) => _customDownloadExecutor = executor;

    /// <summary>
    /// 设置自定义下载后处理管道工厂。由引导程序通过 <c>.DownloadPipeline&lt;T&gt;()</c> 注册注入。
    /// </summary>
    /// <param name="factory">管道工厂委托，接收文件路径参数并返回 <c>IDownloadPipeline</c> 实例。为 <c>null</c> 时跳过管道处理。</param>
    /// <remarks>
    /// 仅在使用默认下载编排器（<c>DefaultDownloadOrchestrator</c>）时生效。
    /// 如果同时设置了自定义编排器（<see cref="SetOrchestrator"/>），则此工厂被忽略。
    /// 管道工厂在每个文件下载完成后被调用，可用于执行哈希校验、解密、病毒扫描等后处理操作。
    /// </remarks>
    public void SetDownloadPipelineFactory(Func<string?, Download.Abstractions.IDownloadPipeline>? factory) => _customDownloadPipelineFactory = factory;

    /// <summary>
    /// 使用全局配置信息初始化策略实例。解析 OS 特定策略并传递差异更新管道。
    /// </summary>
    /// <param name="parameter">全局配置信息，包含版本号、安装路径、更新 URL、应用密钥等。</param>
    /// <exception cref="ArgumentNullException"><paramref name="parameter"/> 为 <c>null</c> 时抛出。</exception>
    /// <remarks>
    /// 此方法在 <see cref="ExecuteAsync"/> 执行前调用，完成以下初始化：
    /// <para>- 保存配置信息到 <c>_configInfo</c> 字段；</para>
    /// <para>- 通过 <see cref="ResolveOsStrategy"/> 解析当前操作系统对应的平台策略；</para>
    /// <para>- 如果 OS 策略继承自 <c>AbstractStrategy</c>，传递待设置的差异更新管道（<c>DiffPipeline</c>）。</para>
    /// </remarks>
    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
        if (_osStrategy is AbstractStrategy abs)
        {
            if (_pendingDiffPipeline != null) abs.DiffPipeline = _pendingDiffPipeline;
        }
    }

    /// <summary>
    /// 执行客户端更新流程的入口方法。清理冲突进程、执行标准工作流，并在异常时触发错误事件和报告。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="InvalidOperationException">当策略尚未通过 <see cref="Create"/> 方法配置时抛出。</exception>
    /// <remarks>
    /// <para>执行流程：</para>
    /// <para>1. 调用 <see cref="CallSmallBowlHomeAsync"/> 关闭可能冲突的升级进程（Bowl）。</para>
    /// <para>2. 调用 <see cref="ExecuteWorkflowAsync"/> 执行核心更新工作流。</para>
    /// <para>3. 如果上述步骤抛出异常，依次安全调用：错误钩子 → 失败报告 → 日志记录 → 事件分发。</para>
    /// <para>所有异常都被捕获并通过 <see cref="EventManager"/> 分发为 <see cref="ExceptionEventArgs"/>，不会向上传播。</para>
    /// </remarks>
    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("ClientUpdateStrategy not configured.");

        try
        {
            GeneralTracer.Debug("ClientUpdateStrategy.ExecuteAsync start.");
            await CallSmallBowlHomeAsync(_configInfo.Bowl);
            await ExecuteWorkflowAsync();
        }
        catch (Exception ex)
        {
            var errCtx = BuildUpdateContext();
            await SafeOnUpdateErrorAsync(errCtx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(errCtx, ex).ConfigureAwait(false);
            GeneralTracer.Error("ClientUpdateStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    private DiffPipeline? _pendingDiffPipeline;

    /// <summary>
    /// 设置差异更新管道，用于在 OS 策略上并行应用增量补丁。
    /// </summary>
    /// <param name="diffPipeline">差异更新管道实例。如果为 <c>null</c>，则清除待设置管道。</param>
    /// <remarks>
    /// 如果 OS 策略（通过 <see cref="ResolveOsStrategy"/> 解析）尚未初始化，则将管道暂存到 <c>_pendingDiffPipeline</c> 字段，
    /// 待 <see cref="Create"/> 方法被调用时传递到 OS 策略的 <c>DiffPipeline</c> 属性。
    /// 差异管道支持并行应用增量补丁（differential patches），显著加快升级包应用速度。
    /// </remarks>
    public void SetDiffPipeline(DiffPipeline? diffPipeline)
    {
        if (_osStrategy is AbstractStrategy abs)
            abs.DiffPipeline = diffPipeline;
        else
            _pendingDiffPipeline = diffPipeline;
    }

    /// <summary>
    /// 启动已更新的应用程序。委托给已解析的 OS 特定策略执行。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// 此方法将启动调用委托给底层 OS 策略（<see cref="WindowsStrategy"/>、<see cref="LinuxStrategy"/> 或 <see cref="MacStrategy"/>）。
    /// 在 <see cref="LaunchUpgradeProcessAsync"/> 中被调用以启动升级端进程。
    /// 如果 <c>_osStrategy</c> 为 <c>null</c>（尚未调用 <see cref="Create"/>），调用将被安全忽略。
    /// </remarks>
    public async Task StartAppAsync()
    {
        if (_osStrategy != null)
            await _osStrategy.StartAppAsync();
    }

    /// <summary>
    /// 注册更新预检查回调。在版本校验完成后、实际下载和备份前调用，允许根据业务逻辑决定是否跳过本次更新。
    /// </summary>
    /// <param name="func">预检查回调函数，接收 <see cref="UpdateInfoEventArgs"/> 参数，返回 <c>true</c> 表示跳过更新，<c>false</c> 表示继续更新。</param>
    /// <returns>返回当前 <see cref="ClientUpdateStrategy"/> 实例，支持链式调用。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> 为 <c>null</c> 时抛出。</exception>
    /// <remarks>
    /// <para>预检查回调在 <see cref="CanSkip"/> 方法中被调用，仅在非强制更新时生效。</para>
    /// <para>回调接收完整的 <see cref="UpdateInfoEventArgs"/>，可根据版本号、发布日期、更新内容等业务逻辑决定是否跳过。</para>
    /// <para>例如：当更新版本仅包含非关键修复且当前版本低于某个阈值时跳过更新。</para>
    /// </remarks>
    public ClientUpdateStrategy UseUpdatePrecheck(Func<UpdateInfoEventArgs, bool> func)
    {
        _updatePrecheck = func ?? throw new ArgumentNullException(nameof(func));
        return this;
    }

    #region Workflow

    /// <summary>
    /// 执行核心更新工作流。根据运行时配置决定执行标准模式或静默模式。
    /// </summary>
    /// <remarks>
    /// 当前实现始终调用 <see cref="ExecuteStandardWorkflowAsync"/>。
    /// 运行时选项（<c>Encoding</c>、<c>Format</c>、<c>DownloadTimeOut</c> 等）
    /// 已由 <c>Bootstrap.ApplyRuntimeOptions()</c> 设置到 <c>_configInfo</c> 上。
    /// </remarks>
    private async Task ExecuteWorkflowAsync()
    {
        // Standard mode �?silent mode is handled by GeneralUpdateBootstrap.LaunchSilentAsync().
        // Runtime options (Encoding, Format, DownloadTimeOut, etc.) are already
        // populated on _configInfo by Bootstrap.ApplyRuntimeOptions().
        await ExecuteStandardWorkflowAsync();
    }

    /// <summary>
    /// 执行标准客户端更新工作流。包含版本校验、事件分发、跳过检查、钩子调用、备份、下载、场景分发和启动升级进程。
    /// </summary>
    /// <remarks>
    /// <para>完整执行流程如下：</para>
    /// <para>
    /// <b>步骤 1 — 版本校验</b>：使用 <c>DownloadSource</c> 或默认的 <c>HttpDownloadSource</c> 调用服务器验证版本。
    /// 返回结果包含更新资源清单以及主程序/升级程序的更新标志位。
    /// </para>
    /// <para>
    /// <b>步骤 2 — 场景判定</b>：根据服务器的 <c>HasMainUpdate</c> 和 <c>HasUpgradeUpdate</c> 标志确定更新场景：
    /// <c>None</c>（无需更新）、<c>UpgradeOnly</c>（仅升级程序）、<c>MainOnly</c>（仅主程序）、<c>Both</c>（两者都需要更新）。
    /// </para>
    /// <para>
    /// <b>步骤 3 — 事件分发</b>：构造 <see cref="UpdateInfoEventArgs"/> 并通过 <c>EventManager.Instance.Dispatch</c> 分发。
    /// </para>
    /// <para>
    /// <b>步骤 4 — 跳过检查</b>：若非强制更新且预检查回调返回 <c>true</c>，则跳过本次更新。
    /// </para>
    /// <para>
    /// <b>步骤 5 — 更新前钩子</b>：调用 <c>Hooks.OnBeforeUpdateAsync</c>，返回 <c>false</c> 时取消更新。
    /// </para>
    /// <para>
    /// <b>步骤 6 — 备份</b>：将当前安装目录备份到临时目录（可通过 <c>BackupEnabled</c> 禁用）。
    /// 同时初始化黑名单配置以排除不需要备份的文件。
    /// </para>
    /// <para>
    /// <b>步骤 7 — 失败版本检查</b>：通过环境变量 <c>UpgradeFail</c> 检查当前版本是否已知失败，避免重复失败的升级。
    /// </para>
    /// <para>
    /// <b>步骤 8 — 下载</b>：通过下载编排器下载所有更新包。
    /// 优先使用自定义编排器（<c>_orchestrator</c>），否则使用 <c>DefaultDownloadOrchestrator</c>，
    /// 后者支持自定义重试策略、下载执行器和后处理管道。
    /// </para>
    /// <para>
    /// <b>步骤 9 — 场景分发</b>：根据判定结果执行不同操作：
    /// <list type="bullet">
    /// <item><description><c>UpgradeOnly</c>：调用 <see cref="ApplyUpgradePackagesAsync"/> 原地应用升级更新包。</description></item>
    /// <item><description><c>MainOnly</c>：调用 <see cref="SendProcessIpc"/> 发送主程序更新信息，
    /// 然后调用 <see cref="LaunchUpgradeProcessAsync"/> 启动升级进程。</description></item>
    /// <item><description><c>Both</c>：先应用升级更新包，再发送主程序信息并启动升级进程。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private async Task ExecuteStandardWorkflowAsync()
    {
        GeneralTracer.Info(
            $"ClientUpdateStrategy: validating client={_configInfo!.ClientVersion}, upgrade={_configInfo.UpgradeClientVersion}");

        // Use injected DownloadSource (Hub/HTTP), or default to HttpDownloadSource
        var downloadSource = DownloadSource ?? new Download.Sources.HttpDownloadSource(
            _configInfo.UpdateUrl,
            _configInfo.ClientVersion,
            _configInfo.UpgradeClientVersion,
            _configInfo.AppSecretKey,
            GetPlatform(),
            _configInfo.ProductId,
            _configInfo.Scheme,
            _configInfo.Token);

        // Call server validation — returns assets plus per-side flags from the two Validate calls
        var sourceResult = await downloadSource.ListAsync().ConfigureAwait(false);
        var downloadPlan = Download.DownloadPlanBuilder.Build(sourceResult.Assets, _configInfo.ClientVersion);

        // Detect update status from SERVER validation results: IsMainUpdate is true only when
        // the server returned version info for the Client call, IsUpgradeUpdate only when
        // the server returned version info for the Upgrade call (requirement 1).
        _configInfo.IsMainUpdate = sourceResult.HasMainUpdate;
        _configInfo.IsUpgradeUpdate = sourceResult.HasUpgradeUpdate;
        _configInfo.LastVersion = downloadPlan.Assets.LastOrDefault()?.Version;

        var scenario = (_configInfo.IsMainUpdate, _configInfo.IsUpgradeUpdate) switch
        {
            (false, false) => UpdateScenario.None,
            (false, true) => UpdateScenario.UpgradeOnly,
            (true, false) => UpdateScenario.MainOnly,
            (true, true) => UpdateScenario.Both,
        };
        GeneralTracer.Info($"ClientUpdateStrategy: Scenario={scenario}, AssetCount={downloadPlan.Assets.Count}");

        // Dispatch update info event with populated version data (full GeneralSpacestation-compatible fields)
        var versionInfos = downloadPlan.Assets.Select(a => new VersionInfo
        {
            RecordId = a.RecordId,
            Name = a.Name,
            Url = a.Url,
            Size = a.Size,
            Hash = a.SHA256,
            Version = a.Version,
            IsForcibly = a.IsForcibly,
            IsFreeze = a.IsFreeze,
            AppType = a.AppType,
            UpgradeMode = a.UpgradeMode,
            IsCrossVersion = a.IsCrossVersion,
            FromVersion = a.FromVersion
        }).ToList();

        var versionResp = new VersionRespDTO
        {
            Code = versionInfos.Count > 0 ? 200 : 404,
            Body = versionInfos,
            Message = versionInfos.Count > 0 ? $"Found {versionInfos.Count} update(s)." : "No updates available."
        };

        var updateInfoArgs = new UpdateInfoEventArgs(versionResp);

        // Capture the first RecordId for status reporting to GeneralSpacestation
        _mainRecordId = downloadPlan.Assets.FirstOrDefault().RecordId;
        EventManager.Instance.Dispatch(this, updateInfoArgs);

        var isForcibly = downloadPlan.IsForcibly;
        if (CanSkip(isForcibly, updateInfoArgs))
        {
            GeneralTracer.Info("ClientUpdateStrategy: update skipped.");
            return;
        }

        // Scenario None: nothing to update — exit early
        if (scenario == UpdateScenario.None)
        {
            GeneralTracer.Info("ClientUpdateStrategy: no update available for client or upgrade.");
            return;
        }

        // Hooks: allow cancellation before download
        var hooksCtx = BuildUpdateContext();
        if (!await SafeOnBeforeUpdateAsync(hooksCtx).ConfigureAwait(false))
        {
            GeneralTracer.Info("ClientUpdateStrategy: update cancelled by OnBeforeUpdateAsync hook.");
            return;
        }

        // Report: update started
        await SafeReportUpdateStartedAsync(hooksCtx).ConfigureAwait(false);

        InitBlackList();
        _configInfo.TempPath = StorageManager.GetTempDirectory("main_temp");
        _configInfo.BackupDirectory = Path.Combine(_configInfo.InstallPath,
            $"{StorageManager.DirectoryName}{_configInfo.ClientVersion}");

        // Check failed version
        if (!string.IsNullOrEmpty(_configInfo.LastVersion) && CheckFail(_configInfo.LastVersion))
        {
            GeneralTracer.Warn(
                $"ClientUpdateStrategy: version {_configInfo.LastVersion} matches known-failed upgrade.");
            return;
        }

        // Backup — conditionally skipped when BackupEnabled is false
        if (_configInfo.BackupEnabled != false)
        {
            Backup();
        }
        else
        {
            GeneralTracer.Info("ClientUpdateStrategy: backup skipped (BackupEnabled=false).");
        }

        _osStrategy!.Create(_configInfo);

        // Download ALL packages via orchestrator (requirement 6: client downloads everything
        // regardless of whether client or upgrade needs updating)
        var orchOptions = Download.Models.DownloadOrchestratorOptions.From(_configInfo);
        GeneralTracer.Info($"ClientUpdateStrategy: downloading {downloadPlan.Assets.Count} asset(s).");
        if (_orchestrator != null)
        {
            await _orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
        }
        else
        {
            var httpClient = GeneralUpdate.Core.Network.HttpClientProvider.Shared;
            var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(
                httpClient, orchOptions, _customDownloadPolicy,
                _customDownloadExecutor, _customDownloadPipelineFactory);
            await orchestrator.ExecuteAsync(downloadPlan, _configInfo.TempPath).ConfigureAwait(false);
        }

        await SafeReportDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);
        await SafeOnDownloadCompletedAsync(hooksCtx).ConfigureAwait(false);

        // Build VersionInfo list with AppType preserved from server response.
        var downloadVersions = downloadPlan.Assets.Select(a => new VersionInfo
        {
            Name = a.Name,
            Hash = a.SHA256,
            Url = a.Url,
            Version = a.Version,
            Format = _configInfo.Format.ToExtension(),
            AppType = a.AppType ?? (int)AppType.Client
        }).ToList();

        var upgradeVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Upgrade).ToList();
        var clientVersions = downloadVersions.Where(v => v.AppType == (int)AppType.Client).ToList();
        GeneralTracer.Info(
            $"ClientUpdateStrategy: Upgrade packages={upgradeVersions.Count}, MainApp packages={clientVersions.Count}");

        // ── Dispatch by scenario — one switch, four states, zero nested if-else ──
        switch (scenario)
        {
            case UpdateScenario.UpgradeOnly:
                await ApplyUpgradePackagesAsync(upgradeVersions).ConfigureAwait(false);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx).ConfigureAwait(false);
                GeneralTracer.Info("ClientUpdateStrategy: Upgrade-only update applied, client continues running.");
                break;

            case UpdateScenario.MainOnly:
                SendProcessIpc(clientVersions);
                await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
                await LaunchUpgradeProcessAsync().ConfigureAwait(false);
                break;

            case UpdateScenario.Both:
                await ApplyUpgradePackagesAsync(upgradeVersions).ConfigureAwait(false);
                await SafeOnAfterUpdateAsync(hooksCtx).ConfigureAwait(false);
                await SafeReportUpdateAppliedAsync(hooksCtx).ConfigureAwait(false);
                SendProcessIpc(clientVersions);
                await SafeOnBeforeStartAppAsync(hooksCtx).ConfigureAwait(false);
                await LaunchUpgradeProcessAsync().ConfigureAwait(false);
                break;
        }
    }

    #endregion

    #region Scenario actions

    /// <summary>
    /// 原地应用升级程序（Upgrade）的更新包。委托 OS 策略执行实际的补丁应用操作。
    /// </summary>
    /// <param name="upgradeVersions">升级程序的版本信息列表。</param>
    /// <remarks>
    /// 此方法仅处理 <c>AppType.Upgrade</c> 类型的更新包。
    /// 将更新包路径设置到 <c>_configInfo.UpdateVersions</c>，然后委托 OS 策略（如 <see cref="WindowsStrategy"/>）
    /// 通过其管道（增量/全量）逐个应用补丁。
    /// </remarks>
    private async Task ApplyUpgradePackagesAsync(List<VersionInfo> upgradeVersions)
    {
        if (upgradeVersions.Count == 0) return;
        GeneralTracer.Info("ClientUpdateStrategy: applying Upgrade packages in place.");
        _configInfo!.UpdateVersions = upgradeVersions;
        _osStrategy!.Create(_configInfo);
        await _osStrategy.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 将主程序（Client）的更新信息序列化为 <c>ProcessInfo</c> 并通过加密 IPC 发送给升级端进程。
    /// </summary>
    /// <param name="clientVersions">主程序的版本信息列表。</param>
    /// <remarks>
    /// <para>此方法完成以下操作：</para>
    /// <para>1. 使用 <c>ConfigurationMapper.MapToProcessInfo</c> 将配置信息和版本列表映射为 <c>ProcessInfo</c> 对象；</para>
    /// <para>2. 将 <c>ProcessInfo</c> 序列化为 JSON 字符串，存储在 <c>_configInfo.ProcessInfo</c> 中；</para>
    /// <para>3. 通过 <c>EncryptedFileProcessInfoProvider</c> 将加密后的进程信息发送给升级端。</para>
    /// <para>升级端（Bowl）收到此信息后，将根据 <c>ProcessInfo</c> 执行实际的安装替换操作。</para>
    /// </remarks>
    private void SendProcessIpc(List<VersionInfo> clientVersions)
    {
        var processInfo = ConfigurationMapper.MapToProcessInfo(
            _configInfo!, clientVersions,
            _configInfo!.BlackFormats ?? BlackListDefaults.DefaultBlackFormats,
            _configInfo.BlackFiles ?? BlackListDefaults.DefaultBlackFiles,
            _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);

        _configInfo.ProcessInfo = JsonSerializer.Serialize(processInfo,
            ProcessInfoJsonContext.Default.ProcessInfo);
        new EncryptedFileProcessInfoProvider().Send(processInfo);
        GeneralTracer.Info("ClientUpdateStrategy: ProcessInfo sent with MainApp versions only.");
    }

    /// <summary>
    /// 启动升级进程（Bowl/Upgrade App），委托 OS 策略执行平台特定的进程启动操作。
    /// </summary>
    /// <remarks>
    /// <para>启动前配置 OS 策略的启动参数：</para>
    /// <para>- <c>LaunchAppName</c>：设置为 <c>_configInfo.UpdateAppName</c>，指定要启动的升级程序文件名；</para>
    /// <para>- <c>LaunchBowl</c>：设置为 <c>false</c>，避免递归启动 Bowl 进程；</para>
    /// <para>- <c>UseUpdatePath</c>：根据 <c>_configInfo.UpdatePath</c> 是否为空字符串决定。</para>
    /// <para>调用 <see cref="StartAppAsync"/> 后，升级进程将接管后续的安装替换操作。</para>
    /// </remarks>
    private async Task LaunchUpgradeProcessAsync()
    {
        if (_osStrategy is AbstractStrategy abs)
        {
            abs.LaunchAppName = _configInfo!.UpdateAppName;
            abs.LaunchBowl = false;
            abs.UseUpdatePath = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath);
        }

        GeneralTracer.Info(
            $"ClientUpdateStrategy: launching upgrade process {_configInfo!.UpdateAppName} via OS strategy.");
        await _osStrategy!.StartAppAsync();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 解析当前操作系统对应的更新策略。优先使用自定义策略，否则根据操作系统自动选择。
    /// </summary>
    /// <returns>OS 特定策略实例（<see cref="WindowsStrategy"/>、<see cref="LinuxStrategy"/> 或 <see cref="MacStrategy"/>）。</returns>
    /// <exception cref="PlatformNotSupportedException">当前操作系统不受支持（非 Windows、Linux 或 macOS）时抛出。</exception>
    /// <remarks>
    /// <para>解析优先级：</para>
    /// <para>1. 通过 <see cref="SetOsStrategy"/> 设置的自定义策略（由引导程序通过 <c>.Strategy&lt;T&gt;()</c> 注入）；</para>
    /// <para>2. 根据 <c>RuntimeInformation.IsOSPlatform</c> 自动探测当前操作系统并实例化对应策略。</para>
    /// <para>如果以上均不匹配，则抛出 <see cref="PlatformNotSupportedException"/>。</para>
    /// </remarks>
    private IStrategy ResolveOsStrategy()
    {
        if (_customOsStrategy != null)
            return _customOsStrategy;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacStrategy();
        throw new PlatformNotSupportedException("The current operating system is not supported!");
    }

    /// <summary>
    /// 初始化文件黑名单配置。用于备份和文件操作时排除不需要处理的文件、格式和目录。
    /// </summary>
    /// <remarks>
    /// 优先使用 <c>_configInfo</c> 中配置的黑名单；如果未配置则使用 <see cref="BlackListDefaults"/> 提供的默认值。
    /// 黑名单包括：排除的文件名列表（如配置文件）、排除的文件扩展名列表（如 .log）、
    /// 跳过的目录列表（如临时目录）。
    /// </remarks>
    private void InitBlackList()
    {
        var effectiveConfig = new BlackListConfig(
            _configInfo!.BlackFiles?.Count > 0 ? _configInfo.BlackFiles : BlackListDefaults.DefaultBlackFiles,
            _configInfo.BlackFormats?.Count > 0 ? _configInfo.BlackFormats : BlackListDefaults.DefaultBlackFormats,
            _configInfo.SkipDirectorys?.Count > 0
                ? _configInfo.SkipDirectorys
                : BlackListDefaults.DefaultSkipDirectories
        );
        StorageManager.BlackListMatcher = new DefaultBlackListMatcher(effectiveConfig);
    }

    /// <summary>
    /// 备份当前安装目录到指定的备份目录，用于更新失败时回滚。
    /// </summary>
    /// <remarks>
    /// 备份操作通过 <c>StorageManager.Backup</c> 执行，排除黑名单中配置的目录。
    /// 备份目录的路径格式为：{InstallPath}/backup_{ClientVersion}。
    /// 此步骤可通过 <c>GlobalConfigInfo.BackupEnabled</c> 设置为 <c>false</c> 跳过。
    /// </remarks>
    private void Backup()
    {
        GeneralTracer.Info(
            $"ClientUpdateStrategy: backing up {_configInfo!.InstallPath} -> {_configInfo.BackupDirectory}");
        StorageManager.Backup(_configInfo.InstallPath, _configInfo.BackupDirectory,
            _configInfo.SkipDirectorys ?? BlackListDefaults.DefaultSkipDirectories);
    }

    /// <summary>
    /// 判断本次更新是否可以被跳过。
    /// </summary>
    /// <param name="isForcibly">是否强制更新。强制更新时不可跳过。</param>
    /// <param name="updateInfo">更新信息事件参数，包含版本列表和响应状态。</param>
    /// <returns>如果可以跳过则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// <para>跳过条件：</para>
    /// <para>1. <paramref name="isForcibly"/> 为 <c>false</c>（非强制更新）；</para>
    /// <para>2. 通过 <see cref="UseUpdatePrecheck"/> 注册的预检查回调返回 <c>true</c>。</para>
    /// <para>如果任意条件不满足（强制更新或无预检查回调），则不可跳过。</para>
    /// <para>此方法在 <see cref="ExecuteStandardWorkflowAsync"/> 的事件分发之后、备份之前被调用。</para>
    /// </remarks>
    private bool CanSkip(bool isForcibly, UpdateInfoEventArgs updateInfo)
    {
        if (isForcibly) return false;
        return _updatePrecheck?.Invoke(updateInfo) == true;
    }

    /// <summary>
    /// 检查指定版本是否已被记录为失败的升级版本。
    /// </summary>
    /// <param name="version">要检查的版本号字符串。</param>
    /// <returns>如果该版本曾被标记为失败升级且当前环境变量中的失败版本大于或等于指定版本，则返回 <c>true</c>。</returns>
    /// <remarks>
    /// 通过读取环境变量 <c>UpgradeFail</c> 获取已知失败的版本号。
    /// 如果 <c>UpgradeFail</c> 环境变量为空或 <paramref name="version"/> 为空，则返回 <c>false</c>。
    /// 版本比较使用 <see cref="Version"/> 类的语义化版本比较。
    /// 此机制用于避免反复尝试已知失败的升级。
    /// </remarks>
    private bool CheckFail(string version)
    {
        var fail = Environments.GetEnvironmentVariable("UpgradeFail");
        if (string.IsNullOrEmpty(fail) || string.IsNullOrEmpty(version))
            return false;
        return new Version(fail) >= new Version(version);
    }

    /// <summary>
    /// 获取当前运行平台的枚举值。
    /// </summary>
    /// <returns>当前平台类型（<see cref="PlatformType.Windows"/>、<see cref="PlatformType.Linux"/>、
    /// <see cref="PlatformType.MacOS"/> 或 <see cref="PlatformType.Unknown"/>）。</returns>
    /// <remarks>
    /// 使用 <c>RuntimeInformation.IsOSPlatform</c> 进行运行时检测。
    /// 此返回值用于构造 <c>HttpDownloadSource</c> 时向服务器告知客户端平台。
    /// </remarks>
    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    /// <summary>
    /// 关闭指定名称的冲突进程（Bowl 升级进程），释放文件锁定。
    /// </summary>
    /// <param name="processName">要关闭的进程名称（不含扩展名）。为空或空白时跳过。</param>
    /// <remarks>
    /// 此方法在更新流程的入口处被调用，用于确保升级进程（Bowl）不在运行状态，
    /// 避免文件锁定导致后续备份或替换操作失败。
    /// 关闭操作通过 <c>GracefulExit.ShutdownAsync</c> 实现优雅退出。
    /// 如果指定进程不存在或关闭过程中发生异常，此方法会记录警告但不阻断流程。
    /// </remarks>
    private async Task CallSmallBowlHomeAsync(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return;
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
    // Hooks & Reporter safe wrappers
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建更新上下文对象，用于传递给钩子和报告器方法。
    /// </summary>
    /// <returns>包含当前更新信息的 <see cref="Hooks.UpdateContext"/> 实例。</returns>
    private Hooks.UpdateContext BuildUpdateContext()
    {
        return new Hooks.UpdateContext(
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? AppDomain.CurrentDomain.BaseDirectory,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.Client
        );
    }

    /// <summary>
    /// 安全调用更新前钩子。如果钩子抛出异常，记录警告并返回 <c>true</c>（允许继续更新）。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    /// <returns>钩子返回的值；如果钩子抛出异常则返回 <c>true</c>。</returns>
    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// 安全调用启动应用前钩子。如果钩子抛出异常，记录警告并继续流程。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeOnBeforeStartAppAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全调用更新错误钩子。如果钩子抛出异常，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    /// <param name="error">更新过程中发生的异常。</param>
    private async Task SafeOnUpdateErrorAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全调用更新完成后钩子（升级包应用后）。如果钩子抛出异常，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeOnAfterUpdateAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全调用下载完成钩子。如果钩子抛出异常，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeOnDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            var downloadCtx = new Hooks.DownloadContext(
                _configInfo?.MainAppName ?? _configInfo?.UpdateAppName ?? "unknown",
                _configInfo?.LastVersion ?? "",
                0, TimeSpan.Zero, _configInfo?.TempPath, true);
            await Hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"OnDownloadCompletedAsync hook failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全上报更新已开始状态。如果上报失败，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeReportUpdateStartedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Updating, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全上报下载已完成状态。如果上报失败，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeReportDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Updating, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report DownloadCompleted failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全上报更新失败状态。如果上报失败，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    /// <param name="error">导致更新失败的异常。</param>
    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Failure, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 安全上报更新应用成功状态。如果上报失败，记录警告。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter
                .ReportAsync(new Download.Reporting.UpdateReport(_mainRecordId,
                    (int)Download.Reporting.UpdateStatus.Success, 1)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}");
        }
    }

    #endregion
}