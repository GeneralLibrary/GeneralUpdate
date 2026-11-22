using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeneralUpdate.Core.Compress;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;

if (args.Length > 0 && args[0] == "test-controlled-worker")
{
    try { await ControlledTestWorker.RunAsync(args); }
    catch (Exception error)
    {
        Console.Error.WriteLine("CONTROLLED_TEST_WORKER_FAILED: " + error);
        Environment.ExitCode = 2;
    }
    return;
}

// A protocol test peer, not a Bowl implementation: no repair, result.json or network delivery.
if (args.Length == 5 && args[0] == "--controlled-apply-fixture")
{
    if (!Guid.TryParseExact(args[1], "D", out var fixtureId) || args[1] != fixtureId.ToString("D"))
        throw new ArgumentException("Expected a canonical fixture GUID.");
    if (!int.TryParse(args[2], out var stopAfter) || stopAfter is < 0 or > 1)
        throw new ArgumentException("Expected a fixture write index of 0 or 1.");
    var root = Path.Combine(Path.GetTempPath(), "BowlControlledIntegration", args[1]);
    var install = Path.Combine(root, "install");
    var state = Path.Combine(root, "state");
    var backup = Path.Combine(root, "backup");
    var stage = Path.Combine(root, "stage");
    var packageUri = new Uri(args[3], UriKind.Absolute);
    if (packageUri.Scheme != "http" || packageUri.Host != "127.0.0.1")
        throw new ArgumentException("The fixture only downloads from IPv4 loopback.");
    using var client = new HttpClient();
    var bytes = await client.GetByteArrayAsync(packageUri);
    var archivePath = Path.Combine(root, "download.zip");
    await File.WriteAllBytesAsync(archivePath, bytes);
    using var stagedZip = FullZipStager.Stage(archivePath, stage, args[4], 10, 1024 * 1024, 2 * 1024 * 1024);
    var plan = ControlledFullZipPrepare.Prepare(stagedZip, install, state, backup, ["data"]);
    ControlledFullZipApply.Apply(plan, afterIntent: stagedZip.RetainForRecovery, afterWrite: i =>
    {
        if (i != stopAfter) return;
        File.WriteAllText(Path.Combine(root, "first-write-complete"), "ready");
        Thread.Sleep(Timeout.Infinite);
    });
    throw new InvalidOperationException("The fault-injection process unexpectedly completed.");
}

if (args[0] == "--application")
{
    // The test terminates this exact process during the observation window; no GUI or application installation is needed.
    await Task.Delay(TimeSpan.FromSeconds(30));
    return;
}

if (args[0] == "--real-producer")
{
    // Opt-in cross-repository integration: exercise the real Core producer against a separately built Bowl executable.
    // Only the isolated install/state trees below are written; this fixture never implements Bowl result/outbox behavior.
    var root = args[1];
    var scenario = args[3];
    var config = new UpdateContext
    {
        InstallPath = Path.Combine(root, "install"),
        DiagnosticsDirectory = Path.Combine(root, "state"),
        ClientVersion = "1.0.0", LastVersion = "2.0.0",
        LaunchClientAfterUpdate = scenario is "processAlive" or "launchFailure" or "healthCheckFailure" or "crash",
        ReportUrl = args.Length > 4 ? args[4] : string.Empty,
        Monitoring = new BowlOptions
        {
            Enabled = true, ExecutablePath = args[2], ReadyTimeoutSeconds = 10,
            HealthTimeoutSeconds = scenario == "crash" ? 20 : 2, UpdateTimeoutSeconds = 30,
            CrashCollectionEnabled = scenario == "crash"
        },
        UpdateVersions = scenario == "updaterTerminated"
            ? [new VersionEntry { RecordId = 42, Version = "2.0.0", Name = "sample" }]
            : []
    };
    Directory.CreateDirectory(config.InstallPath);
    var strategy = new UpdateStrategy();
    strategy.SetOsStrategy(scenario == "updaterTerminated" ? new WaitingStrategy(root) : new RealLaunchStrategy(scenario));
    strategy.Create(config);
    await strategy.ExecuteAsync();
    return;
}

if (args[0] == "--producer")
{
    // Run the real updater and block inside apply, allowing the test to verify evidence survives abrupt process death.
    var root = args[1];
    var config = new UpdateContext
    {
        InstallPath = Path.Combine(root, "install"),
        DiagnosticsDirectory = Path.Combine(root, "state"),
        ClientVersion = "1.0.0", LastVersion = "2.0.0", LaunchClientAfterUpdate = false,
        Monitoring = new BowlOptions { Enabled = true, ExecutablePath = Environment.ProcessPath! },
        UpdateVersions = [new VersionEntry { RecordId = 42, Version = "2.0.0", Name = "sample" }]
    };
    Directory.CreateDirectory(config.InstallPath);
    var strategy = new UpdateStrategy();
    strategy.SetOsStrategy(new WaitingStrategy(root));
    strategy.Create(config);
    await strategy.ExecuteAsync();
    return;
}

var id = args[Array.IndexOf(args, "--attempt") + 1];
var stateRoot = args[Array.IndexOf(args, "--state-root") + 1];
var directory = Path.Combine(stateRoot, "attempts", id);
var request = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "request.json")))!;
if (request["protocolVersion"]!.GetValue<int>() != 1 || request["attemptId"]!.GetValue<string>() != id)
    throw new InvalidDataException("Invalid request.");
var modePath = Path.Combine(stateRoot, "fixture-mode.txt");
// Per-test files select fault injection without changing the production command-line contract or global environment.
var mode = File.Exists(modePath) ? File.ReadAllText(modePath) : "normal";
if (mode == "exit-before") return;
if (mode == "timeout") { await Task.Delay(TimeSpan.FromSeconds(30)); return; }
using var self = Process.GetCurrentProcess();
// Each malformed mode breaks exactly one readiness invariant; the rest of the peer record remains valid.
var ready = new
{
    protocolVersion = mode == "wrong-version" ? 99 : 1,
    attemptId = mode == "wrong-id" ? Guid.NewGuid().ToString("D") : id,
    host = new
    {
        pid = self.Id,
        startTimeUtc = mode == "wrong-start" ? DateTimeOffset.UtcNow.AddDays(-1) :
            new DateTimeOffset(self.StartTime.ToUniversalTime())
    }
};
File.WriteAllText(Path.Combine(directory, "ready.tmp"), JsonSerializer.Serialize(ready));
// Publish only complete JSON, matching the protocol's immutable readiness acknowledgment.
File.Move(Path.Combine(directory, "ready.tmp"), Path.Combine(directory, "ready.json"));
var deadline = DateTime.UtcNow.AddSeconds(30);
while (DateTime.UtcNow < deadline)
{
    if (File.Exists(Path.Combine(stateRoot, "exit-host"))) return;
    // The producer uses atomic replacement; denying delete-sharing here would create an artificial Windows file-lock failure.
    JsonNode state;
    try
    {
        using var stateStream = new FileStream(Path.Combine(directory, "producer.json"), FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        state = JsonNode.Parse(stateStream)!;
    }
    catch (IOException)
    {
        await Task.Delay(20);
        continue;
    }
    if (state["protocolVersion"]!.GetValue<int>() != 1 || state["attemptId"]!.GetValue<string>() != id)
        throw new InvalidDataException("Invalid producer.");
    var stage = state["stage"]!.GetValue<string>();
    if (stage == "completed" || stage == "failed")
    {
        // Fixture receipts are deliberately not result.json: these tests do not certify production Bowl terminal delivery.
        File.WriteAllText(Path.Combine(directory, "fixture-outcome.txt"), stage);
        return;
    }
    if (stage == "awaitingHealth")
    {
        // Observe only the declared PID/start-time pair for the complete window; absence of a dump is irrelevant.
        var identity = state["application"]!;
        var watch = Stopwatch.StartNew();
        var healthy = true;
        while (watch.Elapsed < TimeSpan.FromSeconds(request["healthTimeoutSeconds"]!.GetValue<int>()))
        {
            if (!IsAlive(identity)) { healthy = false; break; }
            await Task.Delay(20);
        }
        File.WriteAllText(Path.Combine(directory, "fixture-outcome.txt"), healthy ? "processAlive" : "healthFailed");
        return;
    }
    if (!IsAlive(request["updater"]!))
    {
        // Before handoff to application observation, producer death leaves the last durable stage as recovery evidence.
        File.WriteAllText(Path.Combine(directory, "fixture-outcome.txt"), "updaterExited");
        return;
    }
    await Task.Delay(20);
}
throw new TimeoutException("Fixture timed out.");

// Process lookup after exit is an expected negative observation, not a fixture infrastructure failure.
static bool IsAlive(JsonNode identity)
{
    try
    {
        using var process = Process.GetProcessById(identity["pid"]!.GetValue<int>());
        return !process.HasExited && process.StartTime.ToUniversalTime() ==
            identity["startTimeUtc"]!.GetValue<DateTimeOffset>().UtcDateTime;
    }
    catch (ArgumentException) { return false; }
}

/// <summary>Blocks after the real updater persisted filesApplying, without performing destructive application writes.</summary>
sealed class WaitingStrategy(string root) : AbstractStrategy
{
    protected override PipelineBuilder BuildPipeline(PipelineContext context) => new(context);
    public override async Task ExecuteAsync()
    {
        File.WriteAllText(Path.Combine(root, "applying.txt"), "filesApplying");
        await Task.Delay(Timeout.Infinite);
    }

}

/// <summary>Launch an isolated test application while preserving Core's real identity-recording callback.</summary>
sealed class RealLaunchStrategy(string mode) : AbstractStrategy
{
    protected override PipelineBuilder BuildPipeline(PipelineContext context) => new(context);

    public override async Task StartAppAsync()
    {
        if (mode == "launchFailure")
            throw new FileNotFoundException("Deliberately missing integration-test application.", "missing-test-app");
        var info = mode == "crash"
            ? new ProcessStartInfo("dotnet") { UseShellExecute = false }
            : new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (mode == "crash")
        {
            info.ArgumentList.Add(Environment.GetEnvironmentVariable("GENERALUPDATE_CRASH_TEST_PEER")!);
            info.ArgumentList.Add("8000");
            info.ArgumentList.Add("--crash");
        }
        else info.ArgumentList.Add("--application");
        using var application = Spawn(info) ?? throw new InvalidOperationException("Test application did not start.");
        if (OnAppStarted != null) await OnAppStarted(application);
        if (mode == "healthCheckFailure")
        {
            // The process exists at handoff but fails inside the survival window, distinct from failing to start it.
            application.Kill();
            await application.WaitForExitAsync();
        }
    }

    /// <summary>Spawns via the Process instance Start() member (static launcher form is avoided project-wide).</summary>
    private static Process? Spawn(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        return process.Start() ? process : null;
    }
}
