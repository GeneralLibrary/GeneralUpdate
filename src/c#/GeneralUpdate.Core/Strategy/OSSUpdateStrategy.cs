using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.Compress;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Orchestrators;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// OSS（对象存储服务，Object Storage Service）更新策略。
/// 通过 AppType 区分客户端（OSSClient）和升级端（OSSUpgrade）两种角色，
/// 分别执行版本检查、下载、解压缩和应用程序启动等操作。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IStrategy"/> 接口，提供完整的 OSS 更新生命周期管理。
/// 根据 <c>AppType</c> 的不同，执行不同的流程：
/// </para>
/// <list type="bullet">
///   <item>
///     <term><c>AppType.OSSClient</c>（客户端）</term>
///     <description>
///       下载版本配置文件，与当前版本比较。如果有新版本，则启动升级进程（<c>GeneralUpdate.Upgrade.exe</c>），
///       然后退出自身。升级进程负责执行实际的下载和安装操作。
///     </description>
///   </item>
///   <item>
///     <term><c>AppType.OSSUpgrade</c>（升级端）</term>
///     <description>
///       读取版本配置文件，从 OSS 下载更新包，解压缩文件，启动主应用程序，
///       然后退出自身。这是实际执行更新操作的进程。
///     </description>
///   </item>
/// </list>
/// <para>
/// 此策略还通过 <c>IUpdateHooks</c> 和 <c>IUpdateReporter</c> 提供完整的生命周期回调
/// 和状态上报功能，支持在更新各阶段执行自定义逻辑以及向服务器报告更新状态。
/// </para>
/// </remarks>
public class OSSUpdateStrategy : IStrategy
{
    private readonly AppType _role;
    private GlobalConfigInfo? _configInfo;
    private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
    private const int DefaultTimeOut = 60;

    /// <summary>
    /// 使用指定的角色初始化 OSS 更新策略。
    /// </summary>
    /// <param name="role">指定当前实例的角色。<c>AppType.OSSClient</c> 表示客户端进程，
    /// <c>AppType.OSSUpgrade</c> 表示升级进程。默认为 <c>AppType.OSSClient</c>。</param>
    /// <remarks>
    /// 客户端角色仅负责检查版本并启动升级进程；升级角色负责实际的下载、解压缩和安装操作。
    /// </remarks>
    public OSSUpdateStrategy(AppType role = AppType.OSSClient)
    {
        _role = role;
    }

    /// <summary>
    /// 获取或设置更新生命周期钩子，用于在更新前后执行自定义回调。
    /// </summary>
    /// <remarks>
    /// 默认实现为 <c>NoOpUpdateHooks</c>（空操作）。可通过设置此属性注入自定义钩子实现，
    /// 以在更新开始、下载完成、更新完成、应用程序启动前以及错误处理等阶段执行自定义逻辑。
    /// </remarks>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// 获取或设置更新状态报告器，用于向服务器或事件系统报告更新进度和结果。
    /// </summary>
    /// <remarks>
    /// 默认实现为 <c>NoOpUpdateReporter</c>（空操作）。可通过设置此属性注入自定义报告器实现，
    /// 以将更新状态（正在更新、成功、失败）上报给远程服务（如 GeneralSpacestation）。
    /// </remarks>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

    /// <summary>
    /// 获取或设置下载源，用于从远程存储获取下载资产列表。
    /// </summary>
    /// <remarks>
    /// 如果设置了此属性，将使用 <c>IDownloadSource.ListAsync</c> 获取下载资产列表，
    /// 而不是从本地版本配置 JSON 文件中读取。
    /// </remarks>
    public IDownloadSource? DownloadSource { get; set; }

    /// <summary>
    /// 获取或设置下载编排器，用于管理多个下载资产的有序下载。
    /// </summary>
    /// <remarks>
    /// 如果设置了此属性，将使用 <c>IDownloadOrchestrator.ExecuteAsync</c> 执行下载操作；
    /// 否则将创建默认的 <c>DefaultDownloadOrchestrator</c> 实例进行下载。
    /// 下载编排器支持进度报告、并发控制和错误处理。
    /// </remarks>
    public IDownloadOrchestrator? DownloadOrchestrator { get; set; }

    /// <summary>
    /// 使用全局配置信息初始化 OSS 更新策略实例。
    /// </summary>
    /// <param name="parameter">全局配置信息，包含安装路径、应用名称、版本号等设置。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="parameter"/> 为 null 时抛出。</exception>
    /// <remarks>
    /// 此方法必须在调用 <see cref="ExecuteAsync"/> 之前调用。
    /// 配置信息会被保存到内部字段以供后续使用，包括安装路径、版本号和下载超时等设置。
    /// </remarks>
    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    /// <summary>
    /// 根据角色（AppType）异步执行 OSS 更新策略的主要流程。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="InvalidOperationException">策略未通过 <see cref="Create"/> 初始化时抛出。</exception>
    /// <remarks>
    /// <para>
    /// 此方法根据构造时指定的 <c>_role</c> 分发执行流程：
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term><c>AppType.OSSUpgrade</c></term>
    ///     <description>调用 <c>ExecuteUpgradeAsync</c> 执行完整的更新流程：
    ///       读取版本配置→下载更新包→解压缩→启动主应用。</description>
    ///   </item>
    ///   <item>
    ///     <term><c>AppType.OSSClient</c></term>
    ///     <description>调用 <c>ExecuteClientAsync</c> 执行客户端检查流程：
    ///       下载版本配置→检查更新→（如有更新）启动升级进程。</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public async Task ExecuteAsync()
    {
        if (_configInfo == null)
            throw new InvalidOperationException("OSSUpdateStrategy not configured. Call Create() first.");

        // Dispatch by role �?no env-var detection needed.
        if (_role == AppType.OSSUpgrade)
        {
            await ExecuteUpgradeAsync();
            return;
        }

        await ExecuteClientAsync();
    }

    // ════════════════════════════════════════════════════════════════
    // Client side: check version, start upgrade process
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 客户端更新检查流程。下载版本配置文件，检查是否有新版本，
    /// 如有更新则启动升级进程并退出当前进程。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// <para>
    /// 执行步骤如下：
    /// </para>
    /// <list type="number">
    ///   <item>从 <c>_configInfo.UpdateUrl</c> 下载版本配置文件到安装目录。</item>
    ///   <item>反序列化 JSON 文件，获取版本列表并按发布时间降序排序。</item>
    ///   <item>比较服务器最新版本与当前客户端版本。</item>
    ///   <item>如果有新版本，解析升级进程路径并启动升级程序。</item>
    ///   <item>调用 <c>GracefulExit.CurrentProcessAsync</c> 退出当前进程。</item>
    /// </list>
    /// </remarks>
    private async Task ExecuteClientAsync()
    {
        GeneralTracer.Debug("OSSUpdateStrategy (client): checking for updates.");

        var installPath = _configInfo!.InstallPath;
        var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.UpdateAppName}_versions.json";
        var versionsFilePath = Path.Combine(installPath, versionFileName);

        if (!string.IsNullOrEmpty(_configInfo.UpdateUrl))
        {
            await DownloadVersionConfig(_configInfo.UpdateUrl, versionsFilePath).ConfigureAwait(false);
        }

        if (!File.Exists(versionsFilePath))
        {
            GeneralTracer.Info("OSSUpdateStrategy: version config download failed, aborting.");
            return;
        }

        var versions = JsonSerializer.Deserialize(
            File.ReadAllText(versionsFilePath),
            JsonContext.VersionOSSJsonContext.Default.ListVersionOSS);
        if (versions == null || versions.Count == 0)
        {
            GeneralTracer.Info("OSSUpdateStrategy: no versions found, aborting.");
            return;
        }

        versions = versions.OrderByDescending(x => x.PubTime).ToList();
        var latest = versions.First();

        if (!IsOssUpgrade(_configInfo.ClientVersion, latest.Version))
        {
            GeneralTracer.Info("OSSUpdateStrategy: no upgrade needed.");
            return;
        }

        // Resolve upgrade exe: prefer UpdatePath, fall back to InstallPath
        var upgradeDir = !string.IsNullOrWhiteSpace(_configInfo.UpdatePath)
            ? (Path.IsPathRooted(_configInfo.UpdatePath)
                ? _configInfo.UpdatePath
                : Path.Combine(installPath, _configInfo.UpdatePath))
            : installPath;
        var upgradeAppName = !string.IsNullOrWhiteSpace(_configInfo.UpdateAppName)
            ? _configInfo.UpdateAppName
            : "GeneralUpdate.Upgrade.exe";
        var appPath = Path.Combine(upgradeDir, upgradeAppName);
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"Upgrade application not found: {appPath}");

        Process.Start(appPath);
        await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
    }

    // ════════════════════════════════════════════════════════════════
    // Upgrade side: download packages, decompress, start main app
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 升级端更新流程。从 OSS 下载更新包、解压缩、启动主应用程序。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// <para>
    /// 这是实际执行更新操作的核心方法。执行步骤如下：
    /// </para>
    /// <list type="number">
    ///   <item>读取版本配置文件或通过 <c>DownloadSource</c> 获取资产列表。</item>
    ///   <item>触发 <c>OnBeforeUpdateAsync</c> 钩子，允许取消更新。</item>
    ///   <item>下载所有更新资产到安装目录。</item>
    ///   <item>解压缩所有下载的 ZIP 文件并删除原始压缩包。</item>
    ///   <item>依次触发下载完成、更新完成、更新应用等生命周期钩子。</item>
    ///   <item>启动主应用程序。</item>
    /// </list>
    /// <para>
    /// 任何异常都会被捕获，触发 <c>OnUpdateErrorAsync</c> 钩子并报告失败状态，
    /// 然后退出当前进程。
    /// </para>
    /// </remarks>
    private async Task ExecuteUpgradeAsync()
    {
        var ctx = BuildUpdateContext();
        try
        {
            // Client downloaded the version JSON to InstallPath; Upgrade reads it from there
            var installPath = _configInfo!.InstallPath;
            var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.UpdateAppName}_versions.json";
            var jsonPath = Path.Combine(installPath, versionFileName);

            if (!File.Exists(jsonPath) && DownloadSource == null)
                throw new FileNotFoundException($"Version config not found: {jsonPath}");

            // Hooks: allow cancellation before download
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("OSSUpdateStrategy (upgrade): cancelled by hook.");
                return;
            }

            await SafeReportUpdateStartedAsync(ctx).ConfigureAwait(false);

            // Build download assets from version config or injected source
            List<DownloadAsset> assets;
            if (DownloadSource != null)
            {
                var sourceResult = await DownloadSource.ListAsync().ConfigureAwait(false);
                assets = sourceResult.Assets.ToList();
            }
            else
            {
                var versions = JsonSerializer.Deserialize(
                    File.ReadAllText(jsonPath),
                    JsonContext.VersionOSSJsonContext.Default.ListVersionOSS);
                if (versions == null || versions.Count == 0)
                    throw new InvalidOperationException("No versions found in OSS configuration.");

                assets = versions.OrderBy(v => v.PubTime)
                    .Where(v => new Version(v.Version ?? "0.0.0") > new Version(_configInfo.ClientVersion))
                    .Select(v =>
                    {
                        if (string.IsNullOrWhiteSpace(v.Url))
                            throw new InvalidOperationException(
                                $"OSS version '{v.PacketName ?? v.Version}' has no download URL.");
                        var zipName = $"{v.PacketName ?? v.Version}{Format.Zip.ToExtension()}";
                        return new DownloadAsset(
                            Name: zipName, Url: v.Url, Size: 0,
                            SHA256: v.Hash, Version: v.Version ?? "0.0.0");
                    }).ToList();
            }

            if (assets.Count == 0)
                throw new InvalidOperationException("No assets to download.");

            GeneralTracer.Debug($"OSSUpdateStrategy (upgrade): downloading {assets.Count} asset(s).");
            await DownloadAssetsAsync(assets, installPath).ConfigureAwait(false);

            GeneralTracer.Debug("OSSUpdateStrategy (upgrade): decompressing.");
            var encoding = Encoding.GetEncoding(_configInfo?.Encoding?.CodePage ?? Encoding.UTF8.CodePage);
            DecompressAssets(assets, installPath, encoding);

            await SafeOnDownloadCompletedAsync(ctx).ConfigureAwait(false);
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);
            await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            GeneralTracer.Debug("OSSUpdateStrategy (upgrade): launching main app.");
            await StartAppAsync();
        }
        catch (Exception ex)
        {
            await SafeOnUpdateErrorAsync(ctx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(ctx, ex).ConfigureAwait(false);
            GeneralTracer.Error("OSSUpdateStrategy.ExecuteUpgradeAsync failed.", ex);
            GeneralUpdate.Core.Event.EventManager.Instance.Dispatch(this, new GeneralUpdate.Core.Event.ExceptionEventArgs(ex, ex.Message));
        }
        finally
        {
            await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 异步启动已更新的主应用程序。
    /// </summary>
    /// <returns>表示异步操作的任务。如果应用程序名称未配置，则返回已完成的任务。</returns>
    /// <exception cref="FileNotFoundException">在主应用程序路径上找不到文件时抛出。</exception>
    /// <remarks>
    /// <para>
    /// 此方法在升级端更新流程完成后调用。它会：
    /// </para>
    /// <list type="number">
    ///   <item>获取主应用程序名称（优先使用 <c>MainAppName</c>，其次使用 <c>UpdateAppName</c>）。</item>
    ///   <item>在安装目录中定位主应用程序的可执行文件。</item>
    ///   <item>使用 <c>Process.Start</c> 启动主应用程序。</item>
    /// </list>
    /// <para>
    /// 与 Windows/Linux/Mac 策略不同，此方法不执行 <c>GracefulExit.CurrentProcessAsync</c>，
    /// 退出操作由调用者 <c>ExecuteUpgradeAsync</c> 在 finally 块中处理。
    /// </para>
    /// </remarks>
    public Task StartAppAsync()
    {
        var appName = _configInfo?.MainAppName ?? _configInfo?.UpdateAppName;
        if (string.IsNullOrEmpty(appName)) return Task.CompletedTask;

        var targetDir = _configInfo?.InstallPath ?? _appPath;
        var appPath = Path.Combine(targetDir, appName);
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"Application not found: {appPath}");

        Process.Start(appPath);
        GeneralTracer.Debug("OSSUpdateStrategy: main application started.");
        return Task.CompletedTask;
    }

    #region Helpers

    /// <summary>
    /// 从指定 URL 下载版本配置文件并保存到本地路径。
    /// </summary>
    /// <param name="url">版本配置文件的远程 URL。</param>
    /// <param name="path">本地保存路径。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// 如果本地已存在同名文件，会先将其删除再下载新文件。
    /// 使用共享的 <c>HttpClientProvider</c> 实例发送 HTTP 请求。
    /// </remarks>
    private static async Task DownloadVersionConfig(string url, string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        using var httpClient = GeneralUpdate.Core.Network.HttpClientProvider.Shared;
        var bytes = await httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// 判断客户端是否需要进行 OSS 升级。
    /// </summary>
    /// <param name="clientVersion">当前客户端版本字符串。</param>
    /// <param name="serverVersion">服务器最新版本字符串。</param>
    /// <returns>如果服务器版本高于客户端版本则返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 此方法会尝试将两个版本字符串解析为 <c>Version</c> 类型进行比较。
    /// 如果任一版本字符串为 null、空字符串或无法解析，则返回 false 表示不需要升级。
    /// </remarks>
    private static bool IsOssUpgrade(string clientVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion))
            return false;
        return Version.TryParse(clientVersion, out var cv)
            && Version.TryParse(serverVersion, out var sv)
            && cv < sv;
    }

    /// <summary>
    /// 下载所有更新资产到指定的目标路径。
    /// </summary>
    /// <param name="assets">要下载的资产列表。</param>
    /// <param name="targetPath">目标安装路径。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// 如果设置了 <c>DownloadOrchestrator</c>，则使用该编排器执行下载；
    /// 否则创建默认的 <c>DefaultDownloadOrchestrator</c> 实例进行下载。
    /// 默认编排器的超时时间从配置中读取，如果未配置则使用 60 秒。
    /// </remarks>
    private async Task DownloadAssetsAsync(List<DownloadAsset> assets, string targetPath)
    {
        var plan = new DownloadPlan(assets, false);
        if (DownloadOrchestrator != null)
        {
            await DownloadOrchestrator.ExecuteAsync(plan, targetPath).ConfigureAwait(false);
        }
        else
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_configInfo?.DownloadTimeOut > 0 ? _configInfo!.DownloadTimeOut : DefaultTimeOut)
            };
            var orchestrator = new DefaultDownloadOrchestrator(httpClient);
            await orchestrator.ExecuteAsync(plan, targetPath).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 解压缩所有下载的资产文件（ZIP 格式）。
    /// </summary>
    /// <param name="assets">已下载的资产列表。</param>
    /// <param name="targetPath">解压缩目标路径。</param>
    /// <param name="encoding">解压缩时使用的字符编码。</param>
    /// <remarks>
    /// 遍历资产列表，对每个资产执行 ZIP 解压缩操作。
    /// 解压缩完成后删除原始的 ZIP 文件。
    /// </remarks>
    private static void DecompressAssets(List<DownloadAsset> assets, string targetPath, Encoding encoding)
    {
        foreach (var asset in assets)
        {
            var zipFilePath = Path.Combine(targetPath, asset.Name);
            CompressProvider.Decompress(Format.Zip, zipFilePath, targetPath, encoding);

            if (!File.Exists(zipFilePath)) continue;
            File.SetAttributes(zipFilePath, FileAttributes.Normal);
            File.Delete(zipFilePath);
        }
    }

    /// <summary>
    /// 构建更新上下文，用于传递更新相关信息给生命周期钩子。
    /// </summary>
    /// <returns>包含应用名称、安装路径、版本号等信息的 <c>UpdateContext</c> 实例。</returns>
    private Hooks.UpdateContext BuildUpdateContext()
    {
        return new Hooks.UpdateContext(
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? _appPath,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.OSSUpgrade
        );
    }

    /// <summary>
    /// 安全地调用更新前的钩子，捕获并记录异常以防止阻止更新流程。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    /// <returns>钩子调用的结果；如果钩子抛出异常则默认为 true（继续更新）。</returns>
    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.UpdateContext ctx)
    {
        try { return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}"); return true; }
    }
    /// <summary>
    /// 安全地调用应用启动前的钩子，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeOnBeforeStartAppAsync(Hooks.UpdateContext ctx)
    {
        try { await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// 安全地调用更新错误钩子，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    /// <param name="error">更新过程中发生的异常。</param>
    private async Task SafeOnUpdateErrorAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try { await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// 安全地调用更新完成后的钩子，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeOnAfterUpdateAsync(Hooks.UpdateContext ctx)
    {
        try { await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// 安全地调用下载完成钩子，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeOnDownloadCompletedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            var downloadCtx = new Hooks.DownloadContext(
                _configInfo?.MainAppName ?? _configInfo?.UpdateAppName ?? "unknown",
                _configInfo?.LastVersion ?? "", 0, TimeSpan.Zero, _appPath, true);
            await Hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"OnDownloadCompletedAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// 安全地报告更新已开始的状态，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeReportUpdateStartedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Updating, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}"); }
    }
    /// <summary>
    /// 安全地报告更新已应用的状态，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Success, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}"); }
    }
    /// <summary>
    /// 安全地报告更新失败的状态，捕获并记录异常。
    /// </summary>
    /// <param name="ctx">更新上下文。</param>
    /// <param name="error">更新过程中发生的异常。</param>
    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Failure, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}"); }
    }

    #endregion
}
