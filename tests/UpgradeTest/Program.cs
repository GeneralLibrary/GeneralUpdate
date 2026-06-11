using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Hooks;

try
{
    await RunStandardUpgradeAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: {ex}");
    Environment.Exit(1);
}

Console.WriteLine("Press Enter to exit...");
Console.ReadLine();

// ═══════════════════════════════════════════════════════════════════
// Standard Upgrade mode — read IPC config → apply patches → launch main app
// ═══════════════════════════════════════════════════════════════════
static async Task RunStandardUpgradeAsync()
{
    Console.WriteLine("=== GeneralUpdate Upgrade Test ===");
    Console.WriteLine($"Started at {DateTime.Now}");
    Console.WriteLine($"Running from: {AppDomain.CurrentDomain.BaseDirectory}");

    // Config comes from the encrypted IPC file written by the Client process.
    // The generalupdate.manifest.json lives in the Client's directory; the Upgrade
    // process receives all necessary identity fields and update versions through
    // the ProcessContract via EncryptedFileProcessContractProvider.Receive().
    //
    // When no IPC file exists (first run, no update pending), the bootstrap
    // treats it as a no-op and returns gracefully — no error, no crash.
    Console.WriteLine("Reading IPC process contract (written by Client process)...");
    Console.WriteLine("IPC file: %TEMP%/GeneralUpdate/ipc/process_info.enc");
    Console.WriteLine();

    await new GeneralUpdateBootstrap()
        .SetOption(Option.AppType, AppType.Upgrade)
        .Hooks<UpgradeTestHooks>()
        .AddListenerMultiDownloadStatistics(OnDownloadStatistics)
        .AddListenerMultiDownloadCompleted(OnDownloadCompleted)
        .AddListenerMultiAllDownloadCompleted(OnAllDownloadCompleted)
        .AddListenerMultiDownloadError(OnDownloadError)
        .AddListenerException(OnException)
        .LaunchAsync();

    // NOTE: After the update pipeline completes and the main app is launched,
    // WindowsStrategy.StartAppAsync() calls GracefulExit.CurrentProcessAsync(),
    // which terminates the Upgrade process. This line is only reached when no
    // updates were found (no IPC data) or when update application fails.
    Console.WriteLine("Upgrade test completed.");
}

// ═══════════════════════════════════════════════════════════════════
// Event handlers
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
// Hooks
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
        // Manifest ClientVersion is updated by UpdateStrategy itself via
        // ManifestInfo.TryUpdateVersion() after successful apply.
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
