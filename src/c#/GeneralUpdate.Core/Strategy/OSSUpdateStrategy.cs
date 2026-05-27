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
/// OSS (Object Storage Service) update strategy �?client/upgrade split via AppType.
/// <list type="bullet">
///   <item><see cref="AppType.OSSClient"/> �?downloads version config, checks for updates,
///        starts the upgrade process, and exits.</item>
///   <item><see cref="AppType.OSSUpgrade"/> �?reads version config, downloads packages from OSS,
///        decompresses them, starts the main app, and exits.</item>
/// </list>
/// </summary>
public class OSSUpdateStrategy : IStrategy
{
    private readonly AppType _role;
    private GlobalConfigInfo? _configInfo;
    private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
    private const int DefaultTimeOut = 60;

    public OSSUpdateStrategy(AppType role = AppType.OSSClient)
    {
        _role = role;
    }

    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();
    public IDownloadSource? DownloadSource { get; set; }
    public IDownloadOrchestrator? DownloadOrchestrator { get; set; }

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

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
            DecompressAssets(assets, installPath);

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

    private static bool IsOssUpgrade(string clientVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion))
            return false;
        return Version.TryParse(clientVersion, out var cv)
            && Version.TryParse(serverVersion, out var sv)
            && cv < sv;
    }

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

    private static void DecompressAssets(List<DownloadAsset> assets, string targetPath)
    {
        var encoding = Encoding.UTF8;
        foreach (var asset in assets)
        {
            var zipFilePath = Path.Combine(targetPath, asset.Name);
            CompressProvider.Decompress(Format.Zip, zipFilePath, targetPath, encoding);

            if (!File.Exists(zipFilePath)) continue;
            File.SetAttributes(zipFilePath, FileAttributes.Normal);
            File.Delete(zipFilePath);
        }
    }

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

    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.UpdateContext ctx)
    {
        try { return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}"); return true; }
    }
    private async Task SafeOnBeforeStartAppAsync(Hooks.UpdateContext ctx)
    {
        try { await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}"); }
    }
    private async Task SafeOnUpdateErrorAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try { await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}"); }
    }
    private async Task SafeOnAfterUpdateAsync(Hooks.UpdateContext ctx)
    {
        try { await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}"); }
    }
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
    private async Task SafeReportUpdateStartedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateEvent.UpdateStarted, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}"); }
    }
    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateEvent.UpdateApplied, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}"); }
    }
    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateEvent.UpdateFailed, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}"); }
    }

    #endregion
}
