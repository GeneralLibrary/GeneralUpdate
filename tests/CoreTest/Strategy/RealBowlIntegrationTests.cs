using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace CoreTest.Strategy;

/// <summary>
/// Explicitly enabled production-host integration. Set GENERALUPDATE_REAL_BOWL_HOST to a built Bowl.Host executable.
/// No cross-repository ProjectReference is used, and normal Core builds do not require a Bowl checkout.
/// </summary>
public sealed class RealBowlIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RealBowlIntegration-" + Guid.NewGuid().ToString("N"));
    private readonly List<Process> _processes = [];
    private static string HostPath => Environment.GetEnvironmentVariable("GENERALUPDATE_REAL_BOWL_HOST")!;
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "MonitorFixture",
        OperatingSystem.IsWindows() ? "MonitoringTestHost.exe" : "MonitoringTestHost");

    /// <summary>Validate actual ready/result/outbox output; receipts from the test peer are not accepted as evidence.</summary>
    [RealBowlTheory]
    [InlineData("filesOnly", "success", "filesApplied")]
    [InlineData("processAlive", "success", "processAlive")]
    [InlineData("launchFailure", "launchFailure", "none")]
    [InlineData("healthCheckFailure", "healthCheckFailure", "none")]
    [InlineData("updaterTerminated", "updaterTerminated", "none")]
    public async Task RealProducerAndHost_AgreeOnLifecycle(string mode, string outcome, string verification)
    {
        var producer = StartProducer(mode);
        if (mode == "updaterTerminated")
        {
            await WaitUntil(() => File.Exists(Path.Combine(_root, "applying.txt")));
            producer.Kill();
        }
        await producer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        var attempt = await FindAttempt();
        TrackIdentity(Read(attempt, "ready.json")["host"]);
        await WaitUntil(() => File.Exists(Path.Combine(attempt, "outbox.json")));
        var result = Read(attempt, "result.json");
        Assert.Equal(1, result["protocolVersion"]!.GetValue<int>());
        Assert.Equal(Path.GetFileName(attempt), result["attemptId"]!.GetValue<string>());
        Assert.Equal(producer.Id, result["updater"]!["pid"]!.GetValue<int>());
        Assert.Equal(outcome, result["outcome"]!.GetValue<string>());
        Assert.Equal(verification, result["verification"]!.GetValue<string>());
        Assert.Equal("localOnly", Read(attempt, "outbox.json")["state"]!.GetValue<string>());
        var state = Read(attempt, "producer.json");
        TrackIdentity(state["application"]);
        if (mode == "processAlive")
        {
            Assert.Equal("awaitingHealth", state["stage"]!.GetValue<string>());
            Assert.Equal(state["application"]!.ToJsonString(), result["application"]!.ToJsonString());
            var observed = result["observedAtUtc"]!.GetValue<DateTimeOffset>();
            var published = state["updatedAtUtc"]!.GetValue<DateTimeOffset>();
            Assert.True(observed - published >= TimeSpan.FromSeconds(2), "Host must observe the complete survival window.");
        }
        Assert.False(File.Exists(Path.Combine(attempt, "fixture-outcome.txt")));
    }

    /// <summary>Real host delivery retains the same event through a 503 and acknowledges only a later 2xx retry.</summary>
    [RealBowlFact]
    public async Task RealHost_RetainsNon2xxAndRetriesLegacyPayload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var firstRequest = Respond(listener, 503);
        var producer = StartProducer("filesOnly", $"http://127.0.0.1:{port}/report");
        await producer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        var payload = JsonNode.Parse(await firstRequest.WaitAsync(TimeSpan.FromSeconds(20)))!.AsObject();
        Assert.Equal(new[] { "recordId", "status", "type" }, payload.Select(pair => pair.Key));
        Assert.Equal(2, payload["status"]!.GetValue<int>());
        var attempt = await FindAttempt();
        TrackIdentity(Read(attempt, "ready.json")["host"]);
        await WaitUntil(() => File.Exists(Path.Combine(attempt, "outbox.json")) &&
                              Read(attempt, "outbox.json")["attempts"]!.GetValue<int>() >= 1);
        var pending = Read(attempt, "outbox.json");
        Assert.Equal("pending", pending["state"]!.GetValue<string>());
        var eventId = pending["eventId"]!.GetValue<string>();
        var next = pending["nextAttemptUtc"]!.GetValue<DateTimeOffset>();
        var delay = next - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(200);
        if (delay > TimeSpan.Zero) await Task.Delay(delay);
        var retryRequest = Respond(listener, 200);
        var retry = Start(HostPath, "--retry", "1", "--state-root", Path.Combine(_root, "state"));
        await retry.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        var retried = JsonNode.Parse(await retryRequest.WaitAsync(TimeSpan.FromSeconds(20)))!;
        Assert.Equal(payload.ToJsonString(), retried.ToJsonString());
        await WaitUntil(() => Read(attempt, "outbox.json")["state"]!.GetValue<string>() == "acknowledged");
        Assert.Equal(eventId, Read(attempt, "outbox.json")["eventId"]!.GetValue<string>());
    }

    private Process StartProducer(string mode, string endpoint = "")
    {
        Directory.CreateDirectory(_root);
        return Start(FixturePath, "--real-producer", _root, HostPath, mode, endpoint);
    }

    private Process Start(string path, params string[] args)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        var process = Process.Start(start) ?? throw new InvalidOperationException("Integration process failed to start.");
        _processes.Add(process);
        return process;
    }

    private async Task<string> FindAttempt()
    {
        var attempts = Path.Combine(_root, "state", "attempts");
        await WaitUntil(() => Directory.Exists(attempts) && Directory.GetDirectories(attempts).Length == 1);
        return Assert.Single(Directory.GetDirectories(attempts));
    }

    private static JsonNode Read(string directory, string name)
    {
        // Both peers publish via atomic replacement; permit rename/deletion while this snapshot is open.
        using var stream = new FileStream(Path.Combine(directory, name), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return JsonNode.Parse(stream)!;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);
        Assert.True(condition(), "Timed out waiting for real Bowl protocol output.");
    }

    private void TrackIdentity(JsonNode? identity)
    {
        if (identity == null) return;
        try
        {
            var process = Process.GetProcessById(identity["pid"]!.GetValue<int>());
            if (process.StartTime.ToUniversalTime() == identity["startTimeUtc"]!.GetValue<DateTimeOffset>().UtcDateTime)
                _processes.Add(process);
            else process.Dispose();
        }
        catch (ArgumentException) { }
    }

    private static async Task<string> Respond(TcpListener listener, int status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var length = 0;
        while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 } header)
            if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                length = int.Parse(header["Content-Length:".Length..].Trim());
        var buffer = new char[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(offset), timeout.Token);
            if (read == 0) throw new EndOfStreamException("Incomplete integration HTTP request.");
            offset += read;
        }
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, timeout.Token);
        return new string(buffer);
    }

    public void Dispose()
    {
        // Collect application/host identities even when an assertion interrupted the test before normal tracking.
        var attempts = Path.Combine(_root, "state", "attempts");
        if (Directory.Exists(attempts))
            foreach (var attempt in Directory.GetDirectories(attempts))
            {
                if (File.Exists(Path.Combine(attempt, "ready.json"))) TrackIdentity(Read(attempt, "ready.json")["host"]);
                if (File.Exists(Path.Combine(attempt, "producer.json"))) TrackIdentity(Read(attempt, "producer.json")["application"]);
            }
        foreach (var process in _processes)
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
            process.Dispose();
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

/// <summary>Skip real-host tests unless the caller explicitly supplies an existing external executable.</summary>
public sealed class RealBowlFactAttribute : FactAttribute
{
    public RealBowlFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_REAL_BOWL_HOST")))
            Skip = "Set GENERALUPDATE_REAL_BOWL_HOST to run external production-host integration.";
    }
}

/// <summary>Theory equivalent of the explicit real-host deployment gate.</summary>
public sealed class RealBowlTheoryAttribute : TheoryAttribute
{
    public RealBowlTheoryAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_REAL_BOWL_HOST")))
            Skip = "Set GENERALUPDATE_REAL_BOWL_HOST to run external production-host integration.";
    }
}
