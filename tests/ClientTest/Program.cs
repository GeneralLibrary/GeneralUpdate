using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Hooks;

try
{
    await RunUpdateTestAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: {ex}");
    Console.WriteLine("Press Enter to exit...");
    Console.ReadLine();
    Environment.Exit(1);
}

// NOTE: In the success path where a MainApp update is applied and the Upgrade
// process is launched, the Client process exits inside the bootstrap —
// the OS strategy's StartAppAsync() calls GracefulExit.CurrentProcessAsync().
// The code below is only reached when no update is needed, or when only
// Upgrade packages were applied (no MainApp IPC/launch).
Console.WriteLine("Press Enter to exit...");
Console.ReadLine();

// ═══════════════════════════════════════════════════════════════════
// Core update test — runs the full non-silent immediate update flow.
//
// The update mode (chain vs. cross-version) is determined entirely by
// GeneralUpdate.Core's DownloadPlanBuilder, which inspects the server
// response and prefers CVP when its FromVersion matches the local
// client version. The test itself is mode-agnostic — configure the
// GeneralSpacestation server's data to exercise either path.
// ═══════════════════════════════════════════════════════════════════
static async Task RunUpdateTestAsync()
{
    Console.WriteLine("=== GeneralUpdate Client Test ===");
    Console.WriteLine($"Started at {DateTime.Now}");
    Console.WriteLine($"Running from: {AppDomain.CurrentDomain.BaseDirectory}");

    var updateUrl = "http://localhost:7391/Upgrade/Verification";
    var reportUrl = "http://localhost:7391/Upgrade/Report";
    var appSecretKey =
        Environment.GetEnvironmentVariable("APP_SECRET_KEY")
        ?? "dfeb5833-975e-4afb-88f1-6278ee9aeff6";

    Console.WriteLine($"UpdateUrl: {updateUrl}");
    Console.WriteLine();
    Console.WriteLine("NOTE: Configure the GeneralSpacestation server with");
    Console.WriteLine("      the desired test data before running.");
    Console.WriteLine("      - Chain data:      TbPackets (IsCrossVersion=false)");
    Console.WriteLine("      - Cross-version:   additionally TbVersionArchives +");
    Console.WriteLine("                         TbPacket (IsCrossVersion=true,");
    Console.WriteLine("                         FromVersion=currentVersion)");
    Console.WriteLine();

    // Non-silent immediate update flow:
    // 1. Version validation against server (HttpDownloadSource.ListAsync)
    // 2. Event dispatch (UpdateInfoEventArgs — shows available versions)
    // 3. Pre-check, hooks, backup
    // 4. Download all packages via DefaultDownloadOrchestrator
    // 5. Scenario dispatch:
    //    - UpgradeOnly: apply upgrade packages in-place, client continues
    //    - MainOnly:   send MainApp versions via IPC → launch Upgrade process → exit
    //    - Both:       apply upgrade packages → IPC → launch Upgrade process → exit
    //    - None:       no-op
    await new GeneralUpdateBootstrap()
        .SetSource(updateUrl, appSecretKey, reportUrl)
        .SetOption(Option.AppType, AppType.Client)
        .Hooks<ClientTestHooks>()
        .AddListenerMultiDownloadStatistics(OnDownloadStatistics)
        .AddListenerMultiDownloadCompleted(OnDownloadCompleted)
        .AddListenerMultiAllDownloadCompleted(OnAllDownloadCompleted)
        .AddListenerMultiDownloadError(OnDownloadError)
        .AddListenerException(OnException)
        .AddListenerUpdateInfo(OnUpdateInfo)
        .LaunchAsync();

    Console.WriteLine("Update test completed.");
}

// ═══════════════════════════════════════════════════════════════════
// Event handlers
// ═══════════════════════════════════════════════════════════════════

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
        {
            var mode = vi.IsCrossVersion == true ? "CVP" : "Chain";
            var appType = vi.AppType switch
            {
                1 => "Client",
                2 => "Upgrade",
                _ => $"Unknown({vi.AppType})"
            };
            Console.WriteLine($"  - [{mode}] {vi.Version} ({vi.Name}) [{vi.Size} bytes] " +
                              $"AppType={appType} " +
                              $"{(vi.IsForcibly == true ? "(forced)" : "")}" +
                              $"{(!string.IsNullOrEmpty(vi.FromVersion) ? $" from={vi.FromVersion}" : "")}");
        }
    }
    else
    {
        Console.WriteLine("  No updates available.");
    }
}

// ═══════════════════════════════════════════════════════════════════
// Hooks
// ═══════════════════════════════════════════════════════════════════

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
