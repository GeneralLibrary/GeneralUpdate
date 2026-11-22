using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.Download.Reporting;
using Xunit.Abstractions;

namespace CoreTest.Compress;

/// <summary>Isolated algorithm interoperability, not production authorization or crash recovery.</summary>
public sealed class ControlledCrossProcessTests
{
    private readonly ITestOutputHelper _output;

    public ControlledCrossProcessTests(ITestOutputHelper output) => _output = output;

    [BowlRecoveryTheory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    public async Task KilledWriter_RealBowlTestPeerRestoresOrPreservesConflicts(int stopAfter, bool conflict)
        => await RunScenario(stopAfter, conflict, false);

    [BowlRecoveryAndGeneralSpaceFact]
    public async Task KilledWriter_RecoveryFollowedByRealGeneralSpaceReporting()
        => await RunScenario(1, false, true);

    [BowlSupervisorTheory]
    [InlineData("success")]
    [InlineData("fail-after-first")]
    [InlineData("fail-after-second")]
    public async Task SupervisorStartsActualWorkerAndRecoversFailedApply(string mode)
        => await RunSupervisorScenario(mode, false);

    [BowlSupervisorAndGeneralSpaceFact]
    public async Task SupervisorRecoversBeforeRealGeneralSpaceReporting()
        => await RunSupervisorScenario("fail-after-first", true, true);

    [BowlSupervisorFact]
    public async Task SupervisorRetriesDurableReportAfterTemporaryFailure()
        => await RunSupervisorScenario("fail-after-first", false, false, true);

    private async Task RunSupervisorScenario(string mode, bool reportToRealServer, bool supervisorReports = false,
        bool retryReport = false)
    {
        var id = Guid.NewGuid().ToString("D");
        var root = Path.Combine(Path.GetTempPath(), "BowlControlledIntegration", id);
        var install = Path.Combine(root, "install");
        var state = Path.Combine(root, "state");
        foreach (var dir in new[] { install, state, Path.Combine(root, "backup"), Path.Combine(root, "stage") })
            Directory.CreateDirectory(dir);
        Process? supervisor = null;
        try
        {
            File.WriteAllText(Path.Combine(install, "old.txt"), "old");
            File.WriteAllText(Path.Combine(install, "unknown.txt"), "user data");
            byte[] package = CreatePackage();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var serving = ServePackage(listener, package);
            File.WriteAllText(Path.Combine(root, "test-package.json"), JsonSerializer.Serialize(new
            {
                url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/package.zip",
                sha256 = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant()
            }));
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_SUPERVISOR")!)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var reportListener = new TcpListener(IPAddress.Loopback, 0);
            Task<(string First, string Second)>? reportRequests = null;
            const string testKey = "isolated-test-supervisor-credential";
            if (retryReport)
            {
                reportListener.Start();
                reportRequests = ServeReports(reportListener);
                File.WriteAllText(Path.Combine(state, "test-report.json"), JsonSerializer.Serialize(new
                {
                    url = $"http://127.0.0.1:{((IPEndPoint)reportListener.LocalEndpoint).Port}/Upgrade/Report",
                    recordId = 42, type = 1, appKeyEnvironmentVariable = "GU_TEST_SUPERVISOR_APP_KEY"
                }));
                start.Environment["GU_TEST_SUPERVISOR_APP_KEY"] = testKey;
            }
            if (supervisorReports)
            {
                using var config = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")!));
                var server = config.RootElement;
                var url = new Uri(new Uri(server.GetProperty("apiBaseUrl").GetString()!),
                    server.GetProperty("reportEndpoint").GetString()!).ToString();
                File.WriteAllText(Path.Combine(state, "test-report.json"), JsonSerializer.Serialize(new
                {
                    url,
                    recordId = server.GetProperty("recordId").GetInt32(),
                    type = 1,
                    appKeyEnvironmentVariable = "GU_TEST_SUPERVISOR_APP_KEY"
                }));
                start.Environment["GU_TEST_SUPERVISOR_APP_KEY"] = server.GetProperty("appKey").GetString()!;
            }
            foreach (string arg in new[] { "test-supervise", root, mode,
                         Path.Combine(AppContext.BaseDirectory, "MonitorFixture", "MonitoringTestHost.exe") })
                start.ArgumentList.Add(arg);
            supervisor = Process.Start(start) ?? throw new InvalidOperationException("Supervisor did not start.");
            var stdout = supervisor.StandardOutput.ReadToEndAsync();
            var stderr = supervisor.StandardError.ReadToEndAsync();
            await supervisor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await serving.WaitAsync(TimeSpan.FromSeconds(10));
            var outputText = await stdout + await stderr;
            _output.WriteLine(outputText);
            Assert.True(supervisor.ExitCode == 0, outputText);
            Assert.True(File.Exists(Path.Combine(state, "test-supervisor-result.json")), outputText);
            using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(state, "test-supervisor-result.json")));
            Assert.Equal(mode == "success" ? "success" : "workerFailed", result.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(mode == "success" ? "notRequested" : "succeeded", result.RootElement.GetProperty("rollback").GetString());
            Assert.Single(Directory.GetFiles(state, "apply-*.json"));
            Assert.Equal(mode == "success" ? "updated" : "old", File.ReadAllText(Path.Combine(install, "old.txt")));
            Assert.Equal(mode == "success", File.Exists(Path.Combine(install, "new.txt")));
            Assert.Equal("user data", File.ReadAllText(Path.Combine(install, "unknown.txt")));
            Assert.Equal("old", File.ReadAllText(Path.Combine(root, "backup", "old.txt")));
            if (retryReport)
            {
                var outboxPath = Path.Combine(state, "reporting", "outbox.json");
                using (var pending = JsonDocument.Parse(File.ReadAllText(outboxPath)))
                {
                    Assert.Equal("pending", pending.RootElement.GetProperty("state").GetString());
                    Assert.Equal(1, pending.RootElement.GetProperty("attempts").GetInt32());
                    Assert.Equal("HTTP 503", pending.RootElement.GetProperty("lastError").GetString());
                    var due = pending.RootElement.GetProperty("nextAttemptUtc").GetDateTimeOffset();
                    var delay = due - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(100);
                    if (delay > TimeSpan.Zero) await Task.Delay(delay);
                }
                var retry = new ProcessStartInfo(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_SUPERVISOR")!)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                };
                retry.ArgumentList.Add("test-report-retry");
                retry.ArgumentList.Add(root);
                retry.Environment["GU_TEST_SUPERVISOR_APP_KEY"] = testKey;
                using var process = Process.Start(retry) ?? throw new InvalidOperationException("Report retry did not start.");
                var errors = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                Assert.True(process.ExitCode == 0, await errors);
                var requests = await reportRequests!.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(requests.First, requests.Second);
                using var acknowledged = JsonDocument.Parse(File.ReadAllText(outboxPath));
                Assert.Equal("acknowledged", acknowledged.RootElement.GetProperty("state").GetString());
                Assert.Equal(2, acknowledged.RootElement.GetProperty("attempts").GetInt32());
                Assert.False(File.ReadAllText(outboxPath).Contains(testKey, StringComparison.Ordinal));
            }
            if (supervisorReports)
            {
                var reporting = Path.Combine(state, "reporting");
                Assert.True(File.Exists(Path.Combine(reporting, "result.json")));
                using var outbox = JsonDocument.Parse(File.ReadAllText(Path.Combine(reporting, "outbox.json")));
                Assert.Equal("acknowledged", outbox.RootElement.GetProperty("state").GetString());
                Assert.Equal(1, outbox.RootElement.GetProperty("attempts").GetInt32());
                using var config = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")!));
                var appKey = config.RootElement.GetProperty("appKey").GetString()!;
                foreach (string file in Directory.GetFiles(reporting, "*.json", SearchOption.AllDirectories))
                {
                    Assert.False(File.ReadAllText(file).Contains(appKey, StringComparison.Ordinal),
                        "A reporting file contains the credential.");
                }
            }
            if (reportToRealServer) await ReportSyntheticFailure(root, install, !supervisorReports);
        }
        finally
        {
            if (supervisor != null)
            {
                if (!supervisor.HasExited) { supervisor.Kill(); await supervisor.WaitForExitAsync(); }
                supervisor.Dispose();
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [BowlSupervisorFact]
    public async Task KilledSupervisorTerminatesWorkerAndRejectsAttemptReuse()
    {
        var id = Guid.NewGuid().ToString("D");
        var root = Path.Combine(Path.GetTempPath(), "BowlControlledIntegration", id);
        foreach (var dir in new[] { "install", "backup", "stage", "state" })
            Directory.CreateDirectory(Path.Combine(root, dir));
        Process? supervisor = null;
        try
        {
            File.WriteAllText(Path.Combine(root, "install", "old.txt"), "old");
            File.WriteAllText(Path.Combine(root, "test-pause-after-first"), "pause");
            var package = CreatePackage();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var serving = ServePackage(listener, package);
            File.WriteAllText(Path.Combine(root, "test-package.json"), JsonSerializer.Serialize(new
            {
                url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/package.zip",
                sha256 = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant()
            }));
            ProcessStartInfo StartInfo()
            {
                var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_SUPERVISOR")!)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (string arg in new[] { "test-supervise", root, "fail-after-first",
                             Path.Combine(AppContext.BaseDirectory, "MonitorFixture", "MonitoringTestHost.exe") })
                    start.ArgumentList.Add(arg);
                return start;
            }
            supervisor = Process.Start(StartInfo()) ?? throw new InvalidOperationException("Supervisor did not start.");
            var line = await supervisor.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.StartsWith("TEST_WORKER ", line);
            using var worker = Process.GetProcessById(int.Parse(line!["TEST_WORKER ".Length..]));
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!File.Exists(Path.Combine(root, "test-first-write-complete")) &&
                   !supervisor.HasExited && DateTime.UtcNow < deadline)
                await Task.Delay(20);
            Assert.True(File.Exists(Path.Combine(root, "test-first-write-complete")), "Worker did not reach the write.");
            await serving.WaitAsync(TimeSpan.FromSeconds(10));
            supervisor.Kill();
            await supervisor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("updated", File.ReadAllText(Path.Combine(root, "install", "old.txt")));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "state"), "apply-*.json"));
            using var retry = Process.Start(StartInfo()) ?? throw new InvalidOperationException("Retry did not start.");
            var stderr = retry.StandardError.ReadToEndAsync();
            await retry.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(retry.ExitCode != 0, await stderr);
            Assert.Equal("updated", File.ReadAllText(Path.Combine(root, "install", "old.txt")));
        }
        finally
        {
            if (supervisor != null)
            {
                if (!supervisor.HasExited) { supervisor.Kill(); await supervisor.WaitForExitAsync(); }
                supervisor.Dispose();
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [WindowsControlledTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkerDoesNotPrepareBeforeValidHostAndGo(bool wrongHost)
    {
        var id = Guid.NewGuid().ToString("D");
        var root = Path.Combine(Path.GetTempPath(), "BowlControlledIntegration", id);
        foreach (var dir in new[] { "install", "backup", "stage", "state" })
            Directory.CreateDirectory(Path.Combine(root, dir));
        Process? worker = null;
        try
        {
            File.WriteAllText(Path.Combine(root, "install", "old.txt"), "old");
            string pipeName = "gu-controlled-test-" + Guid.NewGuid().ToString("N");
            using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var self = Process.GetCurrentProcess();
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "MonitorFixture", "MonitoringTestHost.exe"))
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string arg in new[] { "test-controlled-worker", pipeName,
                         (wrongHost ? self.Id + 1 : self.Id).ToString(),
                         new DateTimeOffset(self.StartTime.ToUniversalTime()).ToString("O"), root, id, "success" })
                start.ArgumentList.Add(arg);
            worker = Process.Start(start) ?? throw new InvalidOperationException("Worker did not start.");
            var stderr = worker.StandardError.ReadToEndAsync();
            await pipe.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (!wrongHost)
            {
                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
                Assert.Equal("READY", await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
                await writer.WriteLineAsync("DENY");
            }
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(worker.ExitCode != 0, await stderr);
            Assert.Equal("old", File.ReadAllText(Path.Combine(root, "install", "old.txt")));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, "backup")));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, "stage")));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, "state")));
            Assert.False(File.Exists(Path.Combine(root, "download.zip")));
        }
        finally
        {
            if (worker != null)
            {
                if (!worker.HasExited) { worker.Kill(); await worker.WaitForExitAsync(); }
                worker.Dispose();
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private async Task RunScenario(int stopAfter, bool conflict, bool reportToRealServer)
    {
        var id = Guid.NewGuid().ToString("D");
        var root = Path.Combine(Path.GetTempPath(), "BowlControlledIntegration", id);
        var install = Path.Combine(root, "install");
        var backup = Path.Combine(root, "backup");
        var stage = Path.Combine(root, "stage");
        var state = Path.Combine(root, "state");
        foreach (var dir in new[] { install, backup, stage, state }) Directory.CreateDirectory(dir);
        Process? writer = null;
        try
        {
            File.WriteAllText(Path.Combine(install, "old.txt"), "old");
            File.WriteAllText(Path.Combine(install, "unknown.txt"), "user data");
            byte[] package;
            using (var buffer = new MemoryStream())
            {
                using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                {
                    foreach (var item in new[] { (Name: "old.txt", Content: "updated"), (Name: "new.txt", Content: "new") })
                    {
                        using var output = new StreamWriter(zip.CreateEntry(item.Name).Open());
                        output.Write(item.Content);
                    }
                }
                package = buffer.ToArray();
            }
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var serving = ServePackage(listener, package);

            var fixture = Path.Combine(AppContext.BaseDirectory, "MonitorFixture", "MonitoringTestHost.exe");
            var start = new ProcessStartInfo(fixture) { UseShellExecute = false };
            start.ArgumentList.Add("--controlled-apply-fixture");
            start.ArgumentList.Add(id);
            start.ArgumentList.Add(stopAfter.ToString());
            start.ArgumentList.Add($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/package.zip");
            start.ArgumentList.Add(Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant());
            writer = Process.Start(start) ?? throw new InvalidOperationException("Writer did not start.");
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(Path.Combine(root, "first-write-complete")) && !writer.HasExited && DateTime.UtcNow < deadline)
                await Task.Delay(20);
            Assert.True(File.Exists(Path.Combine(root, "first-write-complete")), "Writer failed before the injected fault.");
            await serving.WaitAsync(TimeSpan.FromSeconds(10));
            writer.Kill();
            await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("updated", File.ReadAllText(Path.Combine(install, "old.txt")));
            Assert.Equal(stopAfter == 1, File.Exists(Path.Combine(install, "new.txt")));
            var intent = Assert.Single(Directory.GetFiles(state, "apply-*.json"));
            if (conflict) File.WriteAllText(Path.Combine(install, "old.txt"), "changed by user");

            var first = await Recover(root, id, intent);
            Assert.Equal(conflict ? 2 : 0, first.ExitCode);
            Assert.Equal("user data", File.ReadAllText(Path.Combine(install, "unknown.txt")));
            if (conflict)
            {
                Assert.Equal("changed by user", File.ReadAllText(Path.Combine(install, "old.txt")));
                Assert.Equal("new", File.ReadAllText(Path.Combine(install, "new.txt")));
                return;
            }

            Assert.Contains("RECOVERY_COMPLETE", first.Output);
            Assert.Equal("old", File.ReadAllText(Path.Combine(install, "old.txt")));
            Assert.False(File.Exists(Path.Combine(install, "new.txt")));
            var retry = await Recover(root, id, intent);
            Assert.Equal(0, retry.ExitCode);
            Assert.Contains("RECOVERY_COMPLETE", retry.Output);
            Assert.Equal("old", File.ReadAllText(Path.Combine(install, "old.txt")));
            Assert.Equal("user data", File.ReadAllText(Path.Combine(install, "unknown.txt")));
            if (reportToRealServer) await ReportSyntheticFailure(root, install);
        }
        finally
        {
            if (writer != null)
            {
                if (!writer.HasExited) { writer.Kill(); await writer.WaitForExitAsync(); }
                writer.Dispose();
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private async Task ReportSyntheticFailure(string root, string install, bool sendReport = true)
    {
        using var e2e = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")!));
        var baseUri = new Uri(e2e.RootElement.GetProperty("apiBaseUrl").GetString()!);
        var appKey = e2e.RootElement.GetProperty("appKey").GetString()!;
        var recordId = e2e.RootElement.GetProperty("recordId").GetInt32();
        if (sendReport)
        {
            using var http = new HttpClient();
            var reporter = new HttpUpdateReporter(http, new Uri(baseUri, e2e.RootElement.GetProperty("reportEndpoint").GetString()!).ToString())
            {
                AppKey = appKey
            };
            await reporter.ReportAsync(new UpdateReport(recordId, 3, 1) { Phase = "Apply", RequiresAppKey = true });
        }
        var config = new UpdateContext
        {
            InstallPath = install, DiagnosticsDirectory = Path.Combine(root, "diagnostics"), AppSecretKey = appKey,
            UpdateVersions = [new VersionEntry { RecordId = recordId }]
        };
        using var attempt = UpdateAttempt.Begin(config, "update");
        attempt.SetReportIdentity(recordId, 1);
        attempt.Record("filesApplying");
        attempt.RecordFailure(new InvalidOperationException("Synthetic isolated recovery test failure"));
        var installationId = InstallationIdentity.GetOrCreate(config);
        var outbox = Path.Combine(config.DiagnosticsDirectory, "update-outbox", installationId);
        var caseDir = Assert.Single(Directory.GetDirectories(outbox));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json")));
        var artifactId = manifest.RootElement.GetProperty("guid").GetString();
        Assert.True(Guid.TryParse(artifactId, out _));
        Assert.Equal(recordId, manifest.RootElement.GetProperty("recordId").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("recordType").GetInt32());
        _output.WriteLine($"Synthetic failure recordId={recordId}; artifactId={artifactId}");
        var options = new DiagnosticsOptions
        {
            Enabled = true, Url = new Uri(baseUri, e2e.RootElement.GetProperty("artifactEndpoint").GetString()!).ToString(),
            AppKey = appKey, MaxRetry = 0
        };
        await new PendingDiagnosticsScanner(install, options, new DiagnosticsUploader(options),
            stateRoot: config.DiagnosticsDirectory, installationId: installationId).ScanAsync(CancellationToken.None);
        Assert.False(Directory.Exists(caseDir), "GeneralSpace did not acknowledge the synthetic failure diagnostic.");
    }

    private static async Task ServePackage(TcpListener listener, byte[] package)
    {
        using var connection = await listener.AcceptTcpClientAsync();
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        while (await reader.ReadLineAsync() is { Length: > 0 }) { }
        var headers = System.Text.Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Length: {package.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers);
        await stream.WriteAsync(package);
        await stream.FlushAsync();
    }

    private static byte[] CreatePackage()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var item in new[] { (Name: "old.txt", Content: "updated"), (Name: "new.txt", Content: "new") })
            {
                using var output = new StreamWriter(zip.CreateEntry(item.Name).Open());
                output.Write(item.Content);
            }
        return buffer.ToArray();
    }

    private static async Task<(string First, string Second)> ServeReports(TcpListener listener)
    {
        async Task<string> Serve(int status, string body)
        {
            using var connection = await listener.AcceptTcpClientAsync();
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, false, 1024, true);
            var request = new System.Text.StringBuilder();
            int length = 0;
            string? line;
            while ((line = await reader.ReadLineAsync()) is { Length: > 0 })
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    length = int.Parse(line.Substring("Content-Length:".Length).Trim());
                if (line.StartsWith("Idempotency-Key:", StringComparison.OrdinalIgnoreCase))
                    request.Append(line.Trim()).Append('\n');
            }
            var content = new char[length];
            await reader.ReadBlockAsync(content, 0, length);
            request.Append(content);
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            var headers = System.Text.Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Service Unavailable")}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers);
            await stream.WriteAsync(bytes);
            return request.ToString();
        }
        return (await Serve(503, "{}"), await Serve(200, "{\"code\":200,\"body\":true,\"isSuccess\":true}"));
    }

    private static async Task<(int ExitCode, string Output)> Recover(string root, string id, string intent)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_RECOVERY_PEER")!);
        start.ArgumentList.Add("test-recover");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add(id);
        start.ArgumentList.Add(intent);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Recovery peer did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return (process.ExitCode, await stdout + await stderr);
    }

    private sealed class BowlRecoveryTheoryAttribute : TheoryAttribute
    {
        public BowlRecoveryTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows() ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_RECOVERY_PEER")))
                Skip = "Set GENERALUPDATE_BOWL_RECOVERY_PEER to the Bowl test peer DLL on Windows.";
        }
    }

    private sealed class BowlRecoveryAndGeneralSpaceFactAttribute : FactAttribute
    {
        public BowlRecoveryAndGeneralSpaceFactAttribute()
        {
            if (!OperatingSystem.IsWindows() ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_RECOVERY_PEER")) ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")))
                Skip = "Set the Bowl recovery peer and protected GeneralSpace test configuration paths.";
        }
    }

    private sealed class BowlSupervisorTheoryAttribute : TheoryAttribute
    {
        public BowlSupervisorTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows() ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_SUPERVISOR")))
                Skip = "Set GENERALUPDATE_BOWL_SUPERVISOR to the Bowl test peer executable on Windows.";
        }
    }

    private sealed class BowlSupervisorAndGeneralSpaceFactAttribute : FactAttribute
    {
        public BowlSupervisorAndGeneralSpaceFactAttribute()
        {
            if (!OperatingSystem.IsWindows() ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_SUPERVISOR")) ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")))
                Skip = "Set the Bowl supervisor and protected GeneralSpace test configuration paths.";
        }
    }

    private sealed class BowlSupervisorFactAttribute : FactAttribute
    {
        public BowlSupervisorFactAttribute()
        {
            if (!OperatingSystem.IsWindows() ||
                !File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_BOWL_SUPERVISOR")))
                Skip = "Set GENERALUPDATE_BOWL_SUPERVISOR to the Bowl test peer executable on Windows.";
        }
    }

    private sealed class WindowsControlledTheoryAttribute : TheoryAttribute
    {
        public WindowsControlledTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "The controlled fixture requires Windows.";
        }
    }
}
