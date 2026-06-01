using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Hooks;

try
{
    await RunOssUpgradeAsync();
    /*var isOssMode = args.Length > 0 && args[0] == "--oss";

    if (isOssMode)
    {
        await RunOssUpgradeAsync();
    }
    else
    {
        await RunStandardUpgradeAsync();
    }*/
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: {ex}");
    Environment.Exit(1);
}

// ═══════════════════════════════════════════════════════════════════
// OSS Upgrade mode — read versions.json → download packages → decompress → launch main app
// ═══════════════════════════════════════════════════════════════════
static async Task RunOssUpgradeAsync()
{
    Console.WriteLine("=== GeneralUpdate OSS Upgrade Test ===");
    Console.WriteLine($"Started at {DateTime.Now}");
    Console.WriteLine($"Running from: {AppDomain.CurrentDomain.BaseDirectory}");

    // OssUpgrade flow:
    // 1. Read {MainAppName}_versions.json from InstallPath (downloaded by OssClient)
    // 2. Filter versions > ClientVersion, sorted by PubTime asc
    // 3. Download each asset's ZIP from the URL in the version record
    // 4. Decompress all ZIPs to InstallPath, delete archives
    // 5. Launch MainAppName, then exit
    //
    // NOTE: Unlike the standard Upgrade path (which reads config from IPC),
    // OssUpgrade requires explicit SetConfig() so it knows where to find
    // the version JSON and which version to compare against.
    //
    // InstallPath must point to the SAME directory as the OssClient's InstallPath,
    // because the versions.json was downloaded there. When the upgrade runs from
    // a subdirectory (e.g. update/), we resolve up to the parent.

    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    // baseDir ends with '\' (e.g. "...\update\"), so Path.Combine with ".." goes up one level.
    var installPath = Path.GetFullPath(Path.Combine(baseDir, ".."));

    Console.WriteLine($"[OssUpgrade] BaseDir={baseDir}");
    Console.WriteLine($"[OssUpgrade] InstallPath={installPath}");
    GeneralUpdate.Core.GeneralTracer.Info($"[OssUpgrade] BaseDir={baseDir}");
    GeneralUpdate.Core.GeneralTracer.Info($"[OssUpgrade] InstallPath={installPath}");

    await new GeneralUpdateBootstrap()
        .SetConfig(new UpdateRequest
        {
            UpdateUrl = "http://localhost:5000/packages/versions.json",
            InstallPath = installPath,
            ClientVersion = "1.0.0",
            MainAppName = "ClientTest.exe",
            UpdateAppName = "UpgradeTest.exe",
            AppSecretKey = "dfeb5833-975e-4afb-88f1-6278ee9aeff6"
        })
        .SetOption(Option.AppType, AppType.OssUpgrade)
        .Hooks<UpgradeTestHooks>()
        .AddListenerMultiDownloadStatistics(OnDownloadStatistics)
        .AddListenerMultiDownloadCompleted(OnDownloadCompleted)
        .AddListenerMultiAllDownloadCompleted(OnAllDownloadCompleted)
        .AddListenerMultiDownloadError(OnDownloadError)
        .AddListenerException(OnException)
        .LaunchAsync();

    Console.WriteLine("OSS Upgrade test completed.");
}

// ═══════════════════════════════════════════════════════════════════
// Standard Upgrade mode — read IPC config → apply patches → launch main app
// ═══════════════════════════════════════════════════════════════════
static async Task RunStandardUpgradeAsync()
{
    Console.WriteLine("=== GeneralUpdate Upgrade Test ===");
    Console.WriteLine($"Started at {DateTime.Now}");
    Console.WriteLine($"Running from: {AppDomain.CurrentDomain.BaseDirectory}");

    // Config comes from the encrypted IPC file written by the Client process.
    // The Client's generalupdate.manifest.json + SetSource flows through IPC,
    // so the Upgrade never needs to load a manifest directly.

    await new GeneralUpdateBootstrap()
        .SetOption(Option.AppType, AppType.Upgrade)
        .Hooks<UpgradeTestHooks>()
        .AddListenerMultiDownloadStatistics(OnDownloadStatistics)
        .AddListenerMultiDownloadCompleted(OnDownloadCompleted)
        .AddListenerMultiAllDownloadCompleted(OnAllDownloadCompleted)
        .AddListenerMultiDownloadError(OnDownloadError)
        .AddListenerException(OnException)
        .LaunchAsync();

    Console.WriteLine("Upgrade test completed.");
}

// ═══════════════════════════════════════════════════════════════════
// Event handlers (shared across both modes)
// ═══════════════════════════════════════════════════════════════════

static void OnDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
{
    var v = e.Version as VersionEntry;
    Console.WriteLine($"[Apply] {v?.Version}: {e.ProgressPercentage}%");
}

static void OnDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
{
    var v = e.Version as VersionEntry;
    Console.WriteLine($"[Apply] {v?.Version}: {(e.IsCompleted ? "SUCCESS" : "FAILED")}");
}

static void OnAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
{
    Console.WriteLine(e.IsAllDownloadCompleted
        ? "[Apply] All patches applied."
        : $"[Apply] Patches finished with {e.FailedVersions.Count} failure(s).");
}

static void OnDownloadError(object sender, MultiDownloadErrorEventArgs e)
{
    var v = e.Version as VersionEntry;
    Console.WriteLine($"[Apply] Error @ {v?.Version}: {e.Exception.Message}");
}

static void OnException(object sender, ExceptionEventArgs e)
{
    Console.WriteLine($"[Error] {e.Exception}");
}

// ═══════════════════════════════════════════════════════════════════
// Hooks (shared across both modes)
// ═══════════════════════════════════════════════════════════════════

sealed class UpgradeTestHooks : IUpdateHooks
{
    public async Task<bool> OnBeforeUpdateAsync(HookContext ctx)
    {
        Console.WriteLine($"[Hook] OnBeforeUpdate: {ctx.CurrentVersion} -> {ctx.TargetVersion}");
        return await Task.FromResult(true);
    }

    public async Task OnDownloadCompletedAsync(DownloadContext ctx)
    {
        Console.WriteLine($"[Hook] OnDownloadCompleted: {ctx.AssetName} v{ctx.Version} ({ctx.TotalBytes} bytes, {ctx.Duration}) {(ctx.Success ? "OK" : "FAIL")}");
        await Task.CompletedTask;
    }

    public async Task OnAfterUpdateAsync(HookContext ctx)
    {
        Console.WriteLine($"[Hook] OnAfterUpdate: {ctx.CurrentVersion} -> {ctx.TargetVersion}");

        // Write version marker to prevent infinite update loops.
        // The OssClient reads this file on startup to know its current version.
        if (!string.IsNullOrWhiteSpace(ctx.TargetVersion) && !string.IsNullOrWhiteSpace(ctx.InstallPath))
        {
            var markerPath = Path.Combine(ctx.InstallPath, ".current_version");
            File.WriteAllText(markerPath, ctx.TargetVersion);
            Console.WriteLine($"[Hook] Version marker written: {markerPath} = {ctx.TargetVersion}");
        }

        await Task.CompletedTask;
    }

    public async Task OnUpdateErrorAsync(HookContext ctx, Exception ex)
    {
        Console.WriteLine($"[Hook] OnUpdateError: {ctx.CurrentVersion} -> {ctx.TargetVersion} | {ex.Message}");
        await Task.CompletedTask;
    }

    public async Task OnBeforeStartAppAsync(HookContext ctx)
    {
        Console.WriteLine($"[Hook] OnBeforeStartApp: {ctx.UpdateAppName} @ {ctx.InstallPath}");
        await Task.CompletedTask;
    }
}
