using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Hooks;

try
{
    Console.WriteLine("=== GeneralUpdate Client Test (Silent Mode) ===");
    Console.WriteLine($"Started at {DateTime.Now}");
    Console.WriteLine($"Running from: {AppDomain.CurrentDomain.BaseDirectory}");

    // Secrets come from code — never from files.
    var updateUrl = "http://localhost:5000/Upgrade/Verification";
    var reportUrl = "http://localhost:5000/Upgrade/Report";
    var appSecretKey = Environment.GetEnvironmentVariable("APP_SECRET_KEY") ?? "dfeb5833-975e-4afb-88f1-6278ee9aeff6";

    Console.WriteLine($"UpdateUrl: {updateUrl}");
    Console.WriteLine($"Silent mode: ENABLED (poll every 1 minute)");
    Console.WriteLine();

    // Silent mode: polls server in background, prepares update, launches Upgrade on exit.
    var bootstrap = await new GeneralUpdateBootstrap()
        .SetSource(updateUrl, appSecretKey, reportUrl)
        .SetOption(Option.AppType, AppType.Client)
        .SetOption(Option.Silent, true)
        .SetOption(Option.SilentPollIntervalMinutes, 1)
        .Hooks<ClientTestHooks>()
        .AddListenerMultiDownloadStatistics(OnDownloadStatistics)
        .AddListenerMultiDownloadCompleted(OnDownloadCompleted)
        .AddListenerMultiAllDownloadCompleted(OnAllDownloadCompleted)
        .AddListenerMultiDownloadError(OnDownloadError)
        .AddListenerException(OnException)
        .AddListenerUpdateInfo(OnUpdateInfo)
        .LaunchAsync();

    var orchestrator = bootstrap.SilentOrchestrator;

    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════╗");
    Console.WriteLine("║  Silent poll running in background.        ║");
    Console.WriteLine("║  Press Ctrl+C or Enter to exit.            ║");
    Console.WriteLine("║  On exit, Upgrade process will be launched ║");
    Console.WriteLine("║  if an update has been prepared.           ║");
    Console.WriteLine("╚════════════════════════════════════════════╝");
    Console.WriteLine();

    // Keep the process alive so the background poll loop can work.
    // When the user presses Ctrl+C or Enter, the process exits and
    // ProcessExit fires, which triggers the upgrade launch.
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        Console.WriteLine();
        Console.WriteLine("[Shutdown] Ctrl+C pressed. Exiting...");
        e.Cancel = true; // Prevent immediate kill — let ProcessExit fire
        cts.Cancel();
    };

    try
    {
        await Task.Delay(Timeout.Infinite, cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Expected on Ctrl+C — graceful shutdown
    }

    // Explicitly launch the upgrade process before exiting.
    // ProcessExit may not fire reliably in all scenarios (e.g. console Ctrl+C),
    // so we call TryLaunchUpgrade() directly as the primary launch path.
    // If ProcessExit also fires later, the _updaterStarted guard prevents a double-launch.
    Console.WriteLine("[Shutdown] Launching upgrade process...");
    if (orchestrator != null && orchestrator.HasPreparedUpdate)
    {
        var launched = orchestrator.TryLaunchUpgrade();
        Console.WriteLine(launched
            ? "[Shutdown] Upgrade process launched successfully."
            : "[Shutdown] No update prepared or upgrade already launched.");
    }
    else
    {
        Console.WriteLine("[Shutdown] No orchestrator or no update prepared.");
    }

    Console.WriteLine("[Shutdown] Client test exiting gracefully.");
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: {ex}");
    Environment.Exit(1);
}

static void OnDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
{
    var v = e.Version as VersionEntry;
    Console.WriteLine($"[Download] {v?.Version}: {e.ProgressPercentage}% | {e.Speed} | ETA: {e.Remaining}");
}

static void OnDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
{
    var v = e.Version as VersionEntry;
    Console.WriteLine($"[Download] {v?.Version}: {(e.IsCompleted ? "SUCCESS" : "FAILED")}");
}

static void OnAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
{
    Console.WriteLine(e.IsAllDownloadCompleted
        ? "[Download] All downloads completed."
        : $"[Download] Downloads finished with {e.FailedVersions.Count} failure(s).");
}

static void OnDownloadError(object sender, MultiDownloadErrorEventArgs e)
{
    var v = e.Version as VersionEntry;
    Console.WriteLine($"[Download] Error @ {v?.Version}: {e.Exception.Message}");
}

static void OnException(object sender, ExceptionEventArgs e)
{
    Console.WriteLine($"[Error] {e.Exception}");
}

static void OnUpdateInfo(object sender, UpdateInfoEventArgs e)
{
    Console.WriteLine($"[UpdateInfo] Code={e.Info?.Code}, Message={e.Info?.Message}");
    if (e.Info?.Body is { Count: > 0 })
    {
        foreach (var vi in e.Info.Body)
            Console.WriteLine($"  - {vi.Version} ({vi.Name}) [{vi.Size} bytes] {(vi.IsForcibly == true ? "(forced)" : "")}");
    }
    else
    {
        Console.WriteLine("  No updates available.");
    }
}

sealed class ClientTestHooks : IUpdateHooks
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
