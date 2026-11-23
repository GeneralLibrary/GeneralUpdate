using System.Text.Json;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.Ipc;

if (args.Length > 0 && args[0] == "client" ||
    Path.GetFileNameWithoutExtension(Environment.ProcessPath)?.Equals("PushUpdater", StringComparison.OrdinalIgnoreCase) == true)
{
    await RunHostAsync();
    return;
}

const string packet = """
    {"Name":"pkg.zip","Url":"https://example.test/a","Version":"2.0.0",
     "AppType":2,"RecordId":123,"PackageType":2,"Size":42}
    """;
var source = new PushDownloadSource(packet);
_ = new GeneralUpdateBootstrap().DownloadSource(source);
var result = await source.ListAsync();
var asset = result.Assets.Single();
if (asset.Name != "pkg" || asset.RecordId != 123 || asset.AppType != 2 || asset.Size != 42 ||
    DownloadPlanBuilder.Build(result.Assets, "1.0.0", "1.0.0").Assets.Count != 1)
    throw new InvalidOperationException("Push packet mapping or plan construction failed.");

var legacy = await new PushDownloadSource(
    """{"Url":"https://example.test/a","Version":"2.0.0","AppType":1}""").ListAsync();
if (legacy.Assets.Single().RecordId != 0)
    throw new InvalidOperationException("Legacy packet record ID was not absent.");

var contract = new ProcessContract
{
    ReportType = 2,
    UpdateVersions = new List<VersionEntry> { new() { AppType = 2, RecordId = 123 } }
};
var json = JsonSerializer.Serialize(contract, ProcessContractJsonContext.Default.ProcessContract);
var roundTrip = JsonSerializer.Deserialize(json, ProcessContractJsonContext.Default.ProcessContract);
if (roundTrip?.ReportType != 2 || roundTrip.UpdateVersions?.Single().RecordId != 123)
    throw new InvalidOperationException("Push IPC serialization failed.");

Console.WriteLine("Push Native AOT smoke passed.");

static async Task RunHostAsync()
{
    var root = Environment.GetEnvironmentVariable("GU_PUSH_ROOT")
        ?? throw new InvalidOperationException("GU_PUSH_ROOT is required.");
    var updater = Path.GetFileNameWithoutExtension(Environment.ProcessPath)?
        .Equals("PushUpdater", StringComparison.OrdinalIgnoreCase) == true;
    var bootstrap = new GeneralUpdateBootstrap(
        new EncryptedFileProcessContractProvider(Path.Combine(root, "ipc")));
    GeneralUpdate.Core.Network.HttpClientProvider.ExtraHeaders["X-Poison"] = "other-tenant";
    bootstrap.AddListenerException((_, e) => Console.Error.WriteLine(e.Exception));

    if (updater)
    {
        File.WriteAllText(Path.Combine(root, "updater-started"), Environment.ProcessId.ToString());
        await bootstrap.SetOption(Option.AppType, AppType.Upgrade).Hooks<ApplyGateHooks>().LaunchAsync();
        File.WriteAllText(Path.Combine(root, "updater-finished"), Environment.ProcessId.ToString());
        return;
    }

    var serverUrl = Environment.GetEnvironmentVariable("GU_PUSH_SERVER")
        ?? throw new InvalidOperationException("GU_PUSH_SERVER is required.");
    var appKey = Environment.GetEnvironmentVariable("GU_PUSH_APPKEY")
        ?? throw new InvalidOperationException("GU_PUSH_APPKEY is required.");
    var tenant = Environment.GetEnvironmentVariable("GU_PUSH_TENANT");
    await bootstrap.SetConfig(new UpdateRequest
        {
            UpdateUrl = serverUrl + "/Upgrade/Verification",
            ReportUrl = serverUrl + "/Upgrade/Report",
            AppSecretKey = Environment.GetEnvironmentVariable("GU_PUSH_SECRET")
                ?? "process-fixture-api-secret",
            Token = Environment.GetEnvironmentVariable("GU_PUSH_TOKEN"),
            BasicUsername = Environment.GetEnvironmentVariable("GU_PUSH_BASIC_USERNAME"),
            BasicPassword = Environment.GetEnvironmentVariable("GU_PUSH_BASIC_PASSWORD"),
            ProductId = Environment.GetEnvironmentVariable("GU_PUSH_PRODUCT") ?? "push-product",
            ClientVersion = Environment.GetEnvironmentVariable("GU_PUSH_CLIENT_VERSION") ?? "1.0.0",
            UpgradeClientVersion = Environment.GetEnvironmentVariable("GU_PUSH_UPGRADE_VERSION") ?? "1.0.0",
            InstallPath = root,
            MainAppName = "Client",
            UpdateAppName = OperatingSystem.IsWindows() ? "PushUpdater.exe" : "PushUpdater",
            UpdatePath = "updater",
            CustomHeaders = string.IsNullOrEmpty(tenant) ? null :
                new Dictionary<string, string> { ["X-Tenant-Id"] = tenant }
        })
        .EnablePushUpdates(new HubConfig
        {
            Url = serverUrl + "/UpgradeHub",
            ValidationUrl = serverUrl + "/Upgrade/ValidatePush",
            AppKey = appKey
        })
        .SetOption(Option.AppType, AppType.Client)
        .SetOption(Option.LaunchClientAfterUpdate, false)
        .SetOption(Option.RetryCount, 0)
        .SetOption(Option.VerifyChecksum, false) // validated push must still verify its hash
        .LaunchAsync();
    File.WriteAllText(Path.Combine(root, "client-ready"), Environment.ProcessId.ToString());
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var stop = WaitForFileAsync(Path.Combine(root, "client-stop"), timeout.Token);
    await Task.WhenAny(bootstrap.PushCompletion, stop);
    await bootstrap.StopPushUpdatesAsync();
    File.WriteAllText(Path.Combine(root, "client-finished"), Environment.ProcessId.ToString());
}

static async Task WaitForFileAsync(string path, CancellationToken token)
{
    while (!File.Exists(path))
        await Task.Delay(50, token);
}

public sealed class ApplyGateHooks : IUpdateHooks
{
    public async Task<bool> OnBeforeUpdateAsync(HookContext context)
    {
        var root = Environment.GetEnvironmentVariable("GU_PUSH_ROOT")
            ?? throw new InvalidOperationException("GU_PUSH_ROOT is required.");
        if (Environment.GetEnvironmentVariable("GU_PUSH_PAUSE_APPLY") == "1")
        {
            File.WriteAllText(Path.Combine(root, "before-apply"), Environment.ProcessId.ToString());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (!File.Exists(Path.Combine(root, "resume-apply")))
                await Task.Delay(50, timeout.Token);
        }
        return true;
    }

    public Task OnDownloadCompletedAsync(DownloadContext ctx) => Task.CompletedTask;
    public Task OnAfterUpdateAsync(HookContext ctx) => Task.CompletedTask;
    public Task OnUpdateErrorAsync(HookContext ctx, Exception ex) => Task.CompletedTask;
    public Task OnBeforeStartAppAsync(HookContext ctx) => Task.CompletedTask;
}
