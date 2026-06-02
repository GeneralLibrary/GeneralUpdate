using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
/// Oss (Object Storage Service) update strategy.
/// Uses <c>AppType</c> to distinguish between client (OssClient) and upgrade (OssUpgrade) roles,
/// performing version checking, downloading, decompression, and application launch accordingly.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the <see cref="IStrategy"/> interface and provides complete Oss update lifecycle management.
/// Based on the <c>AppType</c>, different workflows are executed:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><c>AppType.OssClient</c> (Client)</term>
///     <description>
///       Downloads the version configuration file and compares it with the current version.
///       If a new version is available, launches the upgrade process (<c>GeneralUpdate.Upgrade.exe</c>),
///       then exits itself. The upgrade process is responsible for actual download and installation.
///     </description>
///   </item>
///   <item>
///     <term><c>AppType.OssUpgrade</c> (Upgrade)</term>
///     <description>
///       Reads the version configuration file, downloads update packages from Oss, decompresses files,
///       launches the main application, and then exits itself. This is the process that actually performs
///       the update operations.
///     </description>
///   </item>
/// </list>
/// <para>
/// This strategy also provides complete lifecycle callbacks and status reporting via <c>IUpdateHooks</c>
/// and <c>IUpdateReporter</c>, supporting custom logic at various update stages and reporting update status
/// to the server.
/// </para>
/// </remarks>
public class OssStrategy : IStrategy
{
    private readonly AppType _role;
    private UpdateContext? _configInfo;
    private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
    private const int DefaultTimeOut = 60;

    /// <summary>
    /// Initializes the Oss update strategy with the specified role.
    /// </summary>
    /// <param name="role">Specifies the role of the current instance. <c>AppType.OssClient</c> indicates the client process,
    /// <c>AppType.OssUpgrade</c> indicates the upgrade process. Defaults to <c>AppType.OssClient</c>.</param>
    /// <remarks>
    /// The client role is only responsible for checking the version and launching the upgrade process;
    /// the upgrade role handles the actual downloading, decompression, and installation operations.
    /// </remarks>
    public OssStrategy(AppType role)
    {
        _role = role;
    }

    /// <summary>
    /// Gets or sets the update lifecycle hooks for executing custom callbacks before and after updates.
    /// </summary>
    /// <remarks>
    /// The default implementation is <c>NoOpUpdateHooks</c> (no operation). Set this property to inject custom hook implementations
    /// for executing custom logic at stages such as update start, download completion, update completion,
    /// pre-application launch, and error handling.
    /// </remarks>
    public Hooks.IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

    /// <summary>
    /// Gets or sets the update status reporter for reporting update progress and results to the server or event system.
    /// </summary>
    /// <remarks>
    /// The default implementation is <c>HttpUpdateReporter</c>. Set this property to inject custom reporter implementations
    /// to report update status (updating, success, failure) to a remote service (such as GeneralSpacestation).
    /// </remarks>
    public Download.Reporting.IUpdateReporter Reporter { get; set; } = new Download.Reporting.HttpUpdateReporter();

    /// <summary>
    /// Gets or sets the download source for retrieving the download asset list from remote storage.
    /// </summary>
    /// <remarks>
    /// If this property is set, <c>IDownloadSource.ListAsync</c> is used to obtain the download asset list,
    /// instead of reading from the local version configuration JSON file.
    /// </remarks>
    public IDownloadSource? DownloadSource { get; set; }

    /// <summary>
    /// Gets or sets the download orchestrator for managing the orderly download of multiple assets.
    /// </summary>
    /// <remarks>
    /// If this property is set, <c>IDownloadOrchestrator.ExecuteAsync</c> is used to perform the download;
    /// otherwise, a default <c>DefaultDownloadOrchestrator</c> instance is created for downloading.
    /// The download orchestrator supports progress reporting, concurrency control, and error handling.
    /// </remarks>
    public IDownloadOrchestrator? DownloadOrchestrator { get; set; }

    /// <summary>
    /// Initializes the Oss update strategy instance with global configuration information.
    /// </summary>
    /// <param name="parameter">Global configuration information containing settings such as install path, application name, and version number.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is null.</exception>
    /// <remarks>
    /// This method must be called before <see cref="ExecuteAsync"/>.
    /// The configuration information is stored in internal fields for subsequent use, including install path,
    /// version number, and download timeout settings.
    /// </remarks>
    public void Create(UpdateContext parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    /// <summary>
    /// Asynchronously executes the main Oss update strategy flow based on the role (AppType).
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the strategy has not been initialized via <see cref="Create"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method dispatches the execution flow based on the <c>_role</c> specified at construction:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term><c>AppType.OssUpgrade</c></term>
    ///     <description>Calls <c>ExecuteUpgradeAsync</c> to execute the full update flow:
    ///       read version config, download update packages, decompress, and launch the main application.</description>
    ///   </item>
    ///   <item>
    ///     <term><c>AppType.OssClient</c></term>
    ///     <description>Calls <c>ExecuteClientAsync</c> to execute the client check flow:
    ///       download version config, check for updates, and (if available) launch the upgrade process.</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public async Task ExecuteAsync()
    {
        if (_configInfo == null)
            throw new InvalidOperationException("OssStrategy not configured. Call Create() first.");

        // Fill missing identity fields from generalupdate.manifest.json,
        // making the manifest the single source of configuration across
        // all update flows (standard ClientStrategy and OssStrategy).
        Configuration.AppMetadataDiscoverer.Discover(_configInfo);

        // Dispatch by role — no env-var detection needed.
        if (_role == AppType.OssUpgrade)
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
    /// Client-side update check flow. Downloads the version configuration file, checks for a new version,
    /// and if an update is available, launches the upgrade process and exits the current process.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// The execution steps are as follows:
    /// </para>
    /// <list type="number">
    ///   <item>Downloads the version configuration file from <c>_configInfo.UpdateUrl</c> to the install directory.</item>
    ///   <item>Deserializes the JSON file, obtains the version list, and sorts it in descending order by publish time.</item>
    ///   <item>Compares the latest server version with the current client version.</item>
    ///   <item>If a new version is available, resolves the upgrade process path and launches the upgrade program.</item>
    ///   <item>Calls <c>GracefulExit.CurrentProcessAsync</c> to exit the current process.</item>
    /// </list>
    /// </remarks>
    private async Task ExecuteClientAsync()
    {
        GeneralTracer.Debug("OssStrategy (client): checking for updates.");

        var installPath = _configInfo!.InstallPath;
        var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.UpdateAppName}_versions.json";
        var versionsFilePath = Path.Combine(installPath, versionFileName);

        GeneralTracer.Info($"[OssClient] InstallPath={installPath}");
        GeneralTracer.Info($"[OssClient] VersionFileName={versionFileName}");
        GeneralTracer.Info($"[OssClient] ClientVersion={_configInfo.ClientVersion}");
        GeneralTracer.Info($"[OssClient] MainAppName={_configInfo.MainAppName}");
        GeneralTracer.Info($"[OssClient] UpdateAppName={_configInfo.UpdateAppName}");

        if (!string.IsNullOrEmpty(_configInfo.UpdateUrl))
        {
            GeneralTracer.Info($"[OssClient] Downloading from {_configInfo.UpdateUrl} ...");
            await DownloadVersionConfig(_configInfo.UpdateUrl, versionsFilePath).ConfigureAwait(false);
            GeneralTracer.Info($"[OssClient] Downloaded -> {versionsFilePath}");
        }

        if (!File.Exists(versionsFilePath))
        {
            GeneralTracer.Info("[OssClient] FAIL: version config file not found after download!");
            return;
        }

        var jsonText = File.ReadAllText(versionsFilePath);
        GeneralTracer.Info($"[OssClient] JSON downloaded ({jsonText.Length} chars)");

        List<OssVersionRecord> versions;
        try
        {
            versions = JsonSerializer.Deserialize(
                jsonText,
                JsonContext.OssVersionRecordJsonContext.Default.ListOssVersionRecord);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"[OssClient] JSON deserialize error: {ex.GetType().Name}: {ex.Message}", ex);
            throw;
        }

        if (versions == null || versions.Count == 0)
        {
            GeneralTracer.Info($"[OssClient] FAIL: no versions found (count={versions?.Count.ToString() ?? "null"})");
            return;
        }

        GeneralTracer.Info($"[OssClient] Deserialized {versions.Count} version(s)");
        foreach (var v in versions)
            GeneralTracer.Info($"  - {v.PacketName} v{v.Version} PubTime={v.PubTime:O}");

        versions = versions.OrderByDescending(x => x.PubTime).ToList();
        var latest = versions.First();
        GeneralTracer.Info($"[OssClient] Latest: {latest.Version} (PubTime={latest.PubTime:O})");

        if (!IsOssUpgrade(_configInfo.ClientVersion, latest.Version))
        {
            GeneralTracer.Info($"[OssClient] No upgrade needed: {_configInfo.ClientVersion} >= {latest.Version}");
            return;
        }

        GeneralTracer.Info($"[OssClient] Upgrade needed: {_configInfo.ClientVersion} -> {latest.Version}");

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
        GeneralTracer.Info($"[OssClient] Resolved upgrade path: {appPath}");

        // List exe files in the directory to help diagnose missing file issues
        try
        {
            var dirFiles = Directory.GetFiles(upgradeDir, "*.exe").Select(f => Path.GetFileName(f));
            GeneralTracer.Info($"[OssClient] *.exe files in {upgradeDir}: [{string.Join(", ", dirFiles)}]");
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"[OssClient] Could not list directory {upgradeDir}: {ex.Message}");
        }

        if (!File.Exists(appPath))
        {
            GeneralTracer.Error($"[OssClient] FAIL: Upgrade app NOT FOUND at {appPath}");
            throw new FileNotFoundException($"Upgrade application not found: {appPath}");
        }

        GeneralTracer.Info($"[OssClient] Launching upgrade: {appPath}");
        Process.Start(appPath);
        GeneralTracer.Info("[OssClient] Upgrade launched, exiting.");
        await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
    }

    // ════════════════════════════════════════════════════════════════
    // Upgrade side: download packages, decompress, start main app
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Upgrade-side update flow. Downloads update packages from Oss, decompresses them, and launches the main application.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This is the core method that actually performs the update operations. The execution steps are as follows:
    /// </para>
    /// <list type="number">
    ///   <item>Reads the version configuration file or retrieves the asset list via <c>DownloadSource</c>.</item>
    ///   <item>Triggers the <c>OnBeforeUpdateAsync</c> hook, allowing the update to be cancelled.</item>
    ///   <item>Downloads all update assets to the install directory.</item>
    ///   <item>Decompresses all downloaded ZIP files and deletes the original archives.</item>
    ///   <item>Sequentially triggers lifecycle hooks for download completion, update completion, and update applied.</item>
    ///   <item>Launches the main application.</item>
    /// </list>
    /// <para>
    /// Any exception is caught, triggering the <c>OnUpdateErrorAsync</c> hook and reporting the failure status,
    /// then exiting the current process.
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
                    JsonContext.OssVersionRecordJsonContext.Default.ListOssVersionRecord);
                if (versions == null || versions.Count == 0)
                    throw new InvalidOperationException("No versions found in Oss configuration.");

                assets = versions.OrderBy(v => v.PubTime)
                    .Where(v => new Version(v.Version ?? "0.0.0") > new Version(_configInfo.ClientVersion))
                    .Select(v =>
                    {
                        if (string.IsNullOrWhiteSpace(v.Url))
                            throw new InvalidOperationException(
                                $"Oss version '{v.PacketName ?? v.Version}' has no download URL.");
                        var zipName = $"{v.PacketName ?? v.Version}{Format.Zip.ToExtension()}";
                        return new DownloadAsset(
                            Name: zipName, Url: v.Url, Size: 0,
                            SHA256: v.Hash, Version: v.Version ?? "0.0.0");
                    }).ToList();
            }

            if (assets.Count == 0)
                throw new InvalidOperationException("No assets to download.");

            // Compute LastVersion deterministically via Version comparison
            // so hooks see the correct TargetVersion regardless of source ordering.
            _configInfo.LastVersion = assets
                .Select(a => new Version(a.Version))
                .Max()!
                .ToString();
            ctx = BuildUpdateContext();

            // Hooks: allow cancellation before download. Called after assets are
            // built and LastVersion is set so OnBeforeUpdateAsync sees the real
            // TargetVersion for decision-making.
            if (!await SafeOnBeforeUpdateAsync(ctx).ConfigureAwait(false))
            {
                GeneralTracer.Info("OssStrategy (upgrade): cancelled by hook.");
                return;
            }

            await SafeReportUpdateStartedAsync(ctx).ConfigureAwait(false);

            GeneralTracer.Debug($"OssStrategy (upgrade): downloading {assets.Count} asset(s).");
            await DownloadAssetsAsync(assets, installPath).ConfigureAwait(false);

            GeneralTracer.Debug("OssStrategy (upgrade): decompressing.");
            var encoding = Encoding.GetEncoding(_configInfo?.Encoding?.CodePage ?? Encoding.UTF8.CodePage);
            DecompressAssets(assets, installPath, encoding);

            // Update generalupdate.manifest.json ClientVersion so the client
            // reads the correct version on next startup, preventing infinite loops.
            Configuration.ManifestInfo.TryUpdateVersion(
                installPath,
                clientVersion: _configInfo.LastVersion);

            await SafeOnDownloadCompletedAsync(ctx).ConfigureAwait(false);
            await SafeOnAfterUpdateAsync(ctx).ConfigureAwait(false);
            await SafeReportUpdateAppliedAsync(ctx).ConfigureAwait(false);
            await SafeOnBeforeStartAppAsync(ctx).ConfigureAwait(false);

            GeneralTracer.Debug("OssStrategy (upgrade): launching main app.");
            await StartAppAsync();
        }
        catch (Exception ex)
        {
            await SafeOnUpdateErrorAsync(ctx, ex).ConfigureAwait(false);
            await SafeReportUpdateFailedAsync(ctx, ex).ConfigureAwait(false);
            GeneralTracer.Error("OssStrategy.ExecuteUpgradeAsync failed.", ex);
            GeneralUpdate.Core.Event.EventManager.Instance.Dispatch(this, new GeneralUpdate.Core.Event.ExceptionEventArgs(ex, ex.Message));
        }
        finally
        {
            await GracefulExit.CurrentProcessAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously starts the updated main application.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. Returns a completed task if the application name is not configured.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file is not found at the main application path.</exception>
    /// <remarks>
    /// <para>
    /// This method is called after the upgrade-side update flow completes. It will:
    /// </para>
    /// <list type="number">
    ///   <item>Retrieve the main application name (prefers <c>MainAppName</c>, falls back to <c>UpdateAppName</c>).</item>
    ///   <item>Locate the main application executable in the install directory.</item>
    ///   <item>Start the main application using <c>Process.Start</c>.</item>
    /// </list>
    /// <para>
    /// Unlike the Windows/Linux/Mac strategies, this method does not call <c>GracefulExit.CurrentProcessAsync</c>;
    /// the exit operation is handled by the caller <c>ExecuteUpgradeAsync</c> in its finally block.
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
        GeneralTracer.Debug("OssStrategy: main application started.");
        return Task.CompletedTask;
    }

    #region Helpers

    /// <summary>
    /// Downloads the version configuration file from the specified URL and saves it to the local path.
    /// </summary>
    /// <param name="url">The remote URL of the version configuration file.</param>
    /// <param name="path">The local save path.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// If the local file already exists, it is deleted before downloading the new file.
    /// Uses the shared <c>HttpClientProvider</c> instance to send HTTP requests.
    /// </remarks>
    private static async Task DownloadVersionConfig(string url, string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        var bytes = await Network.HttpClientProvider.Shared.GetByteArrayAsync(url).ConfigureAwait(false);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Determines whether the client needs an Oss upgrade.
    /// </summary>
    /// <param name="clientVersion">The current client version string.</param>
    /// <param name="serverVersion">The latest server version string.</param>
    /// <returns>Returns true if the server version is higher than the client version; otherwise false.</returns>
    /// <remarks>
    /// This method attempts to parse both version strings as <c>Version</c> types for comparison.
    /// If either version string is null, empty, or cannot be parsed, returns false indicating no upgrade is needed.
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
    /// Downloads all update assets to the specified target path.
    /// </summary>
    /// <param name="assets">The list of assets to download.</param>
    /// <param name="targetPath">The target installation path.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// If <c>DownloadOrchestrator</c> is set, uses that orchestrator to perform the download;
    /// otherwise creates a default <c>DefaultDownloadOrchestrator</c> instance for downloading.
    /// The default orchestrator's timeout is read from the configuration; if not configured, 60 seconds is used.
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
            var options = new DownloadOrchestratorOptions
            {
                DownloadTimeout = TimeSpan.FromSeconds(
                    _configInfo?.DownloadTimeOut > 0 ? _configInfo!.DownloadTimeOut : DefaultTimeOut)
            };
            var orchestrator = new DefaultDownloadOrchestrator(
                Network.HttpClientProvider.Shared, options);
            await orchestrator.ExecuteAsync(plan, targetPath).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Decompresses all downloaded asset files (ZIP format).
    /// </summary>
    /// <param name="assets">The list of downloaded assets.</param>
    /// <param name="targetPath">The target path for decompression.</param>
    /// <param name="encoding">The character encoding to use during decompression.</param>
    /// <remarks>
    /// Iterates through the asset list and performs ZIP decompression for each asset.
    /// Deletes the original ZIP files after decompression completes.
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
    /// Builds the update context for passing update-related information to lifecycle hooks.
    /// </summary>
    /// <returns>An <c>UpdateContext</c> instance containing application name, install path, version number, and other information.</returns>
    private Hooks.HookContext BuildUpdateContext()
    {
        return new Hooks.HookContext(
            _configInfo?.UpdateAppName ?? "unknown",
            _configInfo?.InstallPath ?? _appPath,
            _configInfo?.ClientVersion ?? "0.0.0",
            _configInfo?.LastVersion,
            AppType.OssUpgrade
        );
    }

    /// <summary>
    /// Safely invokes the pre-update hook, catching and logging exceptions to prevent blocking the update flow.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    /// <returns>The result of the hook invocation; defaults to true (continue updating) if the hook throws an exception.</returns>
    private async Task<bool> SafeOnBeforeUpdateAsync(Hooks.HookContext ctx)
    {
        try { return await Hooks.OnBeforeUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeUpdateAsync hook failed: {ex.Message}"); return true; }
    }
    /// <summary>
    /// Safely invokes the pre-start-app hook, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeOnBeforeStartAppAsync(Hooks.HookContext ctx)
    {
        try { await Hooks.OnBeforeStartAppAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnBeforeStartAppAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// Safely invokes the update error hook, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    /// <param name="error">The exception that occurred during the update.</param>
    private async Task SafeOnUpdateErrorAsync(Hooks.HookContext ctx, Exception error)
    {
        try { await Hooks.OnUpdateErrorAsync(ctx, error).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnUpdateErrorAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// Safely invokes the post-update hook, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeOnAfterUpdateAsync(Hooks.HookContext ctx)
    {
        try { await Hooks.OnAfterUpdateAsync(ctx).ConfigureAwait(false); }
        catch (Exception ex) { GeneralTracer.Warn($"OnAfterUpdateAsync hook failed: {ex.Message}"); }
    }
    /// <summary>
    /// Safely invokes the download completed hook, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeOnDownloadCompletedAsync(Hooks.HookContext ctx)
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
    /// Safely reports the update started status, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeReportUpdateStartedAsync(Hooks.HookContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Updating, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateStarted failed: {ex.Message}"); }
    }
    /// <summary>
    /// Safely reports the update applied status, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    private async Task SafeReportUpdateAppliedAsync(Hooks.HookContext ctx)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Success, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateApplied failed: {ex.Message}"); }
    }
    /// <summary>
    /// Safely reports the update failed status, catching and logging exceptions.
    /// </summary>
    /// <param name="ctx">The update context.</param>
    /// <param name="error">The exception that occurred during the update.</param>
    private async Task SafeReportUpdateFailedAsync(Hooks.HookContext ctx, Exception error)
    {
        try
        {
            await Reporter.ReportAsync(new Download.Reporting.UpdateReport(0, (int)Download.Reporting.UpdateStatus.Failure, 1)).ConfigureAwait(false);
        }
        catch (Exception ex) { GeneralTracer.Warn($"Report UpdateFailed failed: {ex.Message}"); }
    }

    #endregion
}
