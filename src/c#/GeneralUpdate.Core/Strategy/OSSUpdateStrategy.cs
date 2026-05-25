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
/// OSS (Object Storage Service) update strategy — single-process, no separate upgrade.
/// Downloads version configuration, fetches update packages from OSS,
/// decompresses them, starts the main application, and exits.
/// </summary>
public class OSSUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
    private const int DefaultTimeOut = 60;

    /// <summary>Lifecycle hooks injected by the bootstrap.</summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();
    /// <summary>Update status reporter injected by the bootstrap.</summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();
    /// <summary>Download source for OSS version listing. Override via <c>.DownloadSource&lt;OssDownloadSource&gt;()</c>.</summary>
    public IDownloadSource? DownloadSource { get; set; }
    /// <summary>Download orchestrator. Override via <c>.DownloadOrchestrator&lt;T&gt;()</c>.</summary>
    public IDownloadOrchestrator? DownloadOrchestrator { get; set; }

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    public async Task ExecuteAsync()
    {
        if (_configInfo == null)
            throw new InvalidOperationException("OSSUpdateStrategy not configured. Call Create() first.");

        var ctx = BuildUpdateContext();
        try
        {
            // 1. Download version configuration from server
            GeneralTracer.Debug("OSSUpdateStrategy: 1. Downloading version configuration.");
            var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.AppName}_versions.json";
            var versionsFilePath = Path.Combine(_appPath, versionFileName);

            DownloadVersionConfig(_configInfo.UpdateUrl, versionsFilePath);
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

            // 2. Check if upgrade is needed
            versions = versions.OrderByDescending(x => x.PubTime).ToList();
            var latest = versions.First();
            if (!IsOssUpgrade(_configInfo.ClientVersion, latest.Version))
            {
                GeneralTracer.Info("OSSUpdateStrategy: no upgrade needed.");
                return;
            }

            // Hooks: allow cancellation before download
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("OSSUpdateStrategy: update cancelled by hook.");
                return;
            }

            // Report: update started
            await SafeReportUpdateStartedAsync(ctx).ConfigureAwait(false);

            // 3. Build download assets
            List<DownloadAsset> assets;
            if (DownloadSource != null)
            {
                GeneralTracer.Debug("OSSUpdateStrategy: 2. Using injected IDownloadSource.");
                assets = (await DownloadSource.ListAsync().ConfigureAwait(false)).ToList();
            }
            else
            {
                GeneralTracer.Debug("OSSUpdateStrategy: 2. Building assets from version config.");
                assets = versions.OrderBy(v => v.PubTime).Select(v =>
                {
                    if (string.IsNullOrWhiteSpace(v.Url))
                        throw new InvalidOperationException(
                            $"OSS version '{v.PacketName ?? v.Version}' has no download URL.");
                    var zipName = $"{v.PacketName ?? v.Version}zip";
                    if (!zipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        zipName += ".zip";
                    return new DownloadAsset(
                        Name: zipName, Url: v.Url, Size: 0,
                        SHA256: v.Hash, Version: v.Version ?? "0.0.0");
                }).ToList();
            }

            if (assets.Count == 0)
                throw new InvalidOperationException("No assets to download.");

            // 4. Download packages
            GeneralTracer.Debug($"OSSUpdateStrategy: 3. Downloading {assets.Count} asset(s).");
            await DownloadAssetsAsync(assets).ConfigureAwait(false);

            // 5. Decompress
            GeneralTracer.Debug("OSSUpdateStrategy: 4. Decompressing packages.");
            DecompressAssets(assets);

            await SafeOnDownloadCompletedAsync(ctx).ConfigureAwait(false);
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);
            await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            // 6. Start main app and exit
            GeneralTracer.Debug("OSSUpdateStrategy: 5. Launching main application.");
            StartApp();
            await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SafeOnUpdateErrorAsync(ctx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(ctx, ex).ConfigureAwait(false);
            GeneralTracer.Error("OSSUpdateStrategy.ExecuteAsync failed.", ex);
            throw;
        }
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
    }

    public void StartApp()
    {
        var appName = _configInfo?.MainAppName ?? _configInfo?.AppName;
        if (string.IsNullOrEmpty(appName)) return;

        var appPath = Path.Combine(_appPath, appName);
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"Application not found: {appPath}");

        Process.Start(appPath);
        GeneralTracer.Debug("OSSUpdateStrategy: application started.");
    }

    #region Helpers

    private static void DownloadVersionConfig(string url, string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        using var httpClient = new HttpClient();
        var bytes = httpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
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

    private async Task DownloadAssetsAsync(List<DownloadAsset> assets)
    {
        var plan = new DownloadPlan(assets, false);

        if (DownloadOrchestrator != null)
        {
            await DownloadOrchestrator.ExecuteAsync(plan, _appPath).ConfigureAwait(false);
        }
        else
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_configInfo?.DownloadTimeOut > 0 ? _configInfo!.DownloadTimeOut : DefaultTimeOut) };
            var orchestrator = new DefaultDownloadOrchestrator(httpClient);
            await orchestrator.ExecuteAsync(plan, _appPath).ConfigureAwait(false);
        }
    }

    private void DecompressAssets(List<DownloadAsset> assets)
    {
        var encoding = Encoding.GetEncoding(_configInfo?.Encoding?.CodePage ?? Encoding.UTF8.CodePage);
        foreach (var asset in assets)
        {
            var zipFilePath = Path.Combine(_appPath, $"{asset.Name}{Format.ZIP}");
            CompressProvider.Decompress(Format.ZIP, zipFilePath, _appPath, encoding);

            if (!File.Exists(zipFilePath)) continue;
            File.SetAttributes(zipFilePath, FileAttributes.Normal);
            File.Delete(zipFilePath);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Hooks & Reporter safe wrappers
    // ════════════════════════════════════════════════════════════════

    private Hooks.UpdateContext BuildUpdateContext()
    {
        return new Hooks.UpdateContext(
            _configInfo?.AppName ?? "unknown",
            _configInfo?.InstallPath ?? _appPath,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.OSS
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
                _configInfo?.MainAppName ?? _configInfo?.AppName ?? "unknown",
                _configInfo?.LastVersion ?? "",
                0, TimeSpan.Zero, _appPath, true);
            await Hooks.OnDownloadCompletedAsync(downloadCtx).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"OnDownloadCompletedAsync hook failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateStartedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.UpdateStarted, ctx.AppType, DateTimeOffset.UtcNow
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateAppliedAsync(Hooks.UpdateContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.UpdateApplied, ctx.AppType, DateTimeOffset.UtcNow
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}"); }
    }

    private async Task SafeReportUpdateFailedAsync(Hooks.UpdateContext ctx, Exception error)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(
                ctx.AppName, ctx.CurrentVersion, ctx.TargetVersion,
                Download.Reporting.UpdateEvent.UpdateFailed, ctx.AppType, DateTimeOffset.UtcNow,
                ErrorMessage: error.Message
            )).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}"); }
    }

    #endregion
}
