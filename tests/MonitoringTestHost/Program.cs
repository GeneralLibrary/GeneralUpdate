using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;

// A protocol test peer, not a Bowl implementation: no repair, result.json or network delivery.
if (args[0] == "--application")
{
    // The test terminates this exact process during the observation window; no GUI or application installation is needed.
    await Task.Delay(TimeSpan.FromSeconds(30));
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
    using var stateStream = new FileStream(Path.Combine(directory, "producer.json"), FileMode.Open,
        FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    var state = JsonNode.Parse(stateStream)!;
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
