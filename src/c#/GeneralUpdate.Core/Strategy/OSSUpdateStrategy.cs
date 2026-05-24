using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Compress;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// OSS (Object Storage Service) update strategy.
/// Downloads version configuration, fetches update packages from OSS,
/// decompresses them, and launches the main application.
/// </summary>
/// <remarks>
/// This replaces the legacy <c>OSSStrategy</c> and <c>GeneralUpdateOSS</c> classes.
/// The OSS workflow is OS-agnostic — no platform-specific pipeline is required.
/// </remarks>
public class OSSUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
    private const int TimeOut = 60;

    /// <summary>Lifecycle hooks injected by the bootstrap.</summary>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();
    /// <summary>Update status reporter injected by the bootstrap.</summary>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

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
            var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.AppName}_versions.json";

            GeneralTracer.Debug("OSSUpdateStrategy: 1. Reading version configuration file.");
            var jsonPath = Path.Combine(_appPath, versionFileName);
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException(jsonPath);

            GeneralTracer.Debug("OSSUpdateStrategy: 2. Parsing version configuration.");
            var versions = StorageManager.GetJson<List<VersionOSS>>(jsonPath,
                VersionOSSJsonContext.Default.ListVersionOSS);
            if (versions == null || versions.Count == 0)
                throw new InvalidOperationException("No versions found in OSS configuration.");

            versions = versions.OrderBy(v => v.PubTime).ToList();

            // Hooks: allow cancellation before download
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("OSSUpdateStrategy: update cancelled by OnBeforeUpdateAsync hook.");
                return;
            }

            // Report: update started
            await SafeReportUpdateStartedAsync(ctx).ConfigureAwait(false);

            GeneralTracer.Debug($"OSSUpdateStrategy: 3. Downloading {versions.Count} version(s).");
            await DownloadVersionsAsync(versions);

            GeneralTracer.Debug("OSSUpdateStrategy: 4. Decompressing packages.");
            Decompress(versions);

            // Report: update applied
            await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);

            // Hooks: before starting main app
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            GeneralTracer.Debug("OSSUpdateStrategy: 5. Launching main application.");
            StartApp();
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

    private async Task DownloadVersionsAsync(List<VersionOSS> versions)
    {
        var assets = versions.Select(v => new Download.Models.DownloadAsset(
            Name: v.PacketName ?? v.Version ?? "unknown",
            Url: v.Url ?? string.Empty,
            Size: 0,
            SHA256: v.Hash,
            Version: v.Version ?? "0.0.0"
        )).ToList();

        var plan = new Download.Models.DownloadPlan(assets, false);

        var httpClient = new System.Net.Http.HttpClient();
        try
        {
            var orchestrator = new Download.Orchestrators.DefaultDownloadOrchestrator(httpClient);
            await orchestrator.ExecuteAsync(plan, _appPath).ConfigureAwait(false);
        }
        finally { httpClient.Dispose(); }
    }

    private void Decompress(List<VersionOSS> versions)
    {
        var encoding = Encoding.GetEncoding(_configInfo?.Encoding?.CodePage ?? Encoding.UTF8.CodePage);
        foreach (var version in versions)
        {
            var zipFilePath = Path.Combine(_appPath, $"{version.PacketName}{Format.ZIP}");
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
            AppType.OSSApp
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
