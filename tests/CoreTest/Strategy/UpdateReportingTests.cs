using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;

namespace CoreTest.Strategy;

/// <summary>
/// Exercises real disk records and separate peer processes. The peer is not a production Bowl host:
/// these tests cover GeneralUpdate's producer contract, not Bowl rollback, outbox delivery or business health.
/// </summary>
public sealed class UpdateReportingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UpdateReporting-" + Guid.NewGuid().ToString("N"));
    private readonly List<Process> _processes = [];
    private static string HostPath => Path.Combine(AppContext.BaseDirectory, "MonitorFixture",
        OperatingSystem.IsWindows() ? "MonitoringTestHost.exe" : "MonitoringTestHost");

    /// <summary>Keep install, staging and evidence paths isolated; monitor deployment stays outside all of them.</summary>
    private UpdateContext Config(bool monitoring = false, bool launch = false)
    {
        Directory.CreateDirectory(Path.Combine(_root, "install"));
        Directory.CreateDirectory(Path.Combine(_root, "state"));
        return new UpdateContext
        {
            InstallPath = Path.Combine(_root, "install"),
            DiagnosticsDirectory = Path.Combine(_root, "state"),
            ClientVersion = "1.0.0", LastVersion = "2.0.0",
            LaunchClientAfterUpdate = launch, MainAppName = "missing-app",
            BackupEnabled = false, Format = Format.Zip,
            TempPath = Path.Combine(_root, "staging"),
            UpdateVersions = [],
            Monitoring = new BowlOptions
            {
                Enabled = monitoring, ExecutablePath = HostPath, ReadyTimeoutSeconds = 3,
                HealthTimeoutSeconds = 1
            }
        };
    }

    private static string AttemptPath(UpdateContext config) =>
        Path.Combine(config.DiagnosticsDirectory!, "attempts", config.UpdateAttemptId!);

    private static JsonNode Read(UpdateContext config, string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AttemptPath(config), file)))!;

    private static async Task Run(UpdateContext config, TestOs os, IUpdateReporter reporter)
    {
        var strategy = new UpdateStrategy { Reporter = reporter };
        strategy.SetOsStrategy(os);
        strategy.Create(config);
        await strategy.ExecuteAsync();
    }

    /// <summary>No monitor deployment is required for file-only completion, and success explicitly means filesApplied.</summary>
    [Fact]
    public async Task FilesOnly_WithoutMonitor_ReportsOneFilesAppliedSuccess()
    {
        var config = Config();
        var reporter = new CaptureReporter();
        await Run(config, new TestOs(), reporter);
        var report = Assert.Single(reporter.Reports);
        Assert.Equal(2, report.Status);
        Assert.Equal("filesApplied", report.Outcome);
        Assert.Equal("completed", report.Stage);
        Assert.Equal(config.UpdateAttemptId, report.UpdateAttemptId);
        Assert.False(File.Exists(Path.Combine(AttemptPath(config), "request.json")));
    }

    /// <summary>A failure after file application must not be preceded by the old premature Success report.</summary>
    [Fact]
    public async Task LaunchFailure_NeverReportsPrematureSuccess()
    {
        var config = Config(launch: true);
        var reporter = new CaptureReporter();
        await Run(config, new TestOs { LaunchError = new FileNotFoundException("missing executable", "missing.exe") }, reporter);
        var failure = Assert.Single(reporter.Reports);
        Assert.Equal(3, failure.Status);
        Assert.Equal("launchFailure", failure.Error!.Category);
        Assert.Equal("missing.exe", failure.Error.FailedPath);
        Assert.Contains("FileNotFoundException", failure.Error.StackTrace);
    }

    /// <summary>Timeout, exit and malformed identity/version must all fail closed before any application-file work.</summary>
    [Theory]
    [InlineData("timeout")]
    [InlineData("exit-before")]
    [InlineData("wrong-id")]
    [InlineData("wrong-version")]
    [InlineData("wrong-start")]
    public async Task MissingOrInvalidReadiness_StopsBeforeFileChanges(string mode)
    {
        var config = Config(monitoring: true);
        config.Monitoring!.ReadyTimeoutSeconds = 1;
        config.UpdateVersions = [new VersionEntry { Name = "sample", Version = "2.0.0", RecordId = 42 }];
        File.WriteAllText(Path.Combine(config.DiagnosticsDirectory!, "fixture-mode.txt"), mode);
        var os = new TestOs();
        var reporter = new CaptureReporter();
        await Run(config, os, reporter);
        Assert.False(os.Applied);
        Assert.Empty(reporter.Reports);
        var failure = Read(config, "producer.json");
        Assert.Equal("failed", failure["stage"]!.GetValue<string>());
        Assert.Equal("monitorUnavailable", failure["error"]!["category"]!.GetValue<string>());
        TrackHost(config);
    }

    /// <summary>A missing optional deployment is a monitor outage, not a legacy server update Failure.</summary>
    [Fact]
    public async Task MissingExecutable_IsMonitoringUnavailableNotUpdateFailure()
    {
        var config = Config(monitoring: true);
        config.Monitoring!.ExecutablePath = Path.Combine(_root, "not-present");
        var reporter = new CaptureReporter();
        var os = new TestOs();
        await Run(config, os, reporter);
        Assert.False(os.Applied);
        Assert.Empty(reporter.Reports);
        var events = Directory.GetFiles(Path.Combine(AttemptPath(config), "events"), "*.json");
        Assert.Contains(events, file => File.ReadAllText(file).Contains("monitorUnavailable"));
    }

    /// <summary>The real producer writes its handoff; only the test peer observes the terminal window, with no updater HTTP success.</summary>
    [Theory]
    [InlineData(false, "filesOnly", "completed")]
    [InlineData(true, "processAlive", "processAlive")]
    public async Task MonitorOwnsTerminalDelivery(bool launch, string mode, string outcome)
    {
        var config = Config(monitoring: true, launch: launch);
        var reporter = new CaptureReporter();
        var os = new TestOs();
        await Run(config, os, reporter);
        TrackHost(config);
        Assert.Empty(reporter.Reports);
        Assert.Equal(mode, Read(config, "request.json")["launchMode"]!.GetValue<string>());
        await WaitForFile(Path.Combine(AttemptPath(config), "fixture-outcome.txt"));
        Assert.Equal(outcome, File.ReadAllText(Path.Combine(AttemptPath(config), "fixture-outcome.txt")));
        if (launch)
        {
            var producer = Read(config, "producer.json");
            Assert.Equal("awaitingHealth", producer["stage"]!.GetValue<string>());
            Assert.Equal(Environment.ProcessId, producer["application"]!["pid"]!.GetValue<int>());
        }
        Assert.False(File.Exists(Path.Combine(AttemptPath(config), "result.json")));
    }

    /// <summary>Ready is not a permanent health guarantee: loss of the peer must block subsequent apply/launch work.</summary>
    [Fact]
    public async Task MonitorExitsDuringApply_PreventsLaunch()
    {
        var config = Config(monitoring: true, launch: true);
        config.UpdateVersions = [new VersionEntry { Name = "sample", Version = "2.0.0", RecordId = 42 }];
        var os = new TestOs
        {
            Apply = async () =>
            {
                var ready = Read(config, "ready.json");
                using var host = Process.GetProcessById(ready["host"]!["pid"]!.GetValue<int>());
                File.WriteAllText(Path.Combine(config.DiagnosticsDirectory!, "exit-host"), "");
                await host.WaitForExitAsync();
            }
        };
        var reporter = new CaptureReporter();
        await Run(config, os, reporter);
        Assert.False(os.Launched);
        Assert.Empty(reporter.Reports);
        Assert.Equal("monitorUnavailable", Read(config, "producer.json")["error"]!["category"]!.GetValue<string>());
    }

    /// <summary>Starting a process is insufficient for processAlive when that exact process dies during observation.</summary>
    [Fact]
    public async Task ApplicationDiesInObservationWindow_ProducerNeverClaimsSuccess()
    {
        var config = Config(monitoring: true, launch: true);
        var start = new ProcessStartInfo(HostPath) { UseShellExecute = false };
        start.ArgumentList.Add("--application");
        var application = Process.Start(start)!;
        _processes.Add(application);
        var reporter = new CaptureReporter();
        await Run(config, new TestOs { Application = application }, reporter);
        TrackHost(config);
        application.Kill();
        await application.WaitForExitAsync();
        await WaitForFile(Path.Combine(AttemptPath(config), "fixture-outcome.txt"));
        Assert.Equal("healthFailed", File.ReadAllText(Path.Combine(AttemptPath(config), "fixture-outcome.txt")));
        Assert.Empty(reporter.Reports);
        Assert.Equal("awaitingHealth", Read(config, "producer.json")["stage"]!.GetValue<string>());
    }

    /// <summary>Kill a real updater process, then recover identity and its last stage exclusively from disk.</summary>
    [Fact]
    public async Task UpdaterKilled_PreservesDiskEvidenceAndExactIdentity()
    {
        var config = Config();
        var start = new ProcessStartInfo(HostPath) { UseShellExecute = false };
        start.ArgumentList.Add("--producer");
        start.ArgumentList.Add(_root);
        var updater = Process.Start(start)!;
        _processes.Add(updater);
        await WaitForFile(Path.Combine(_root, "applying.txt"));
        var attempt = Assert.Single(Directory.GetDirectories(Path.Combine(config.DiagnosticsDirectory!, "attempts")));
        config.UpdateAttemptId = Path.GetFileName(attempt);
        TrackHost(config);
        Assert.Equal(updater.Id, Read(config, "request.json")["updater"]!["pid"]!.GetValue<int>());
        updater.Kill();
        await updater.WaitForExitAsync();
        await WaitForFile(Path.Combine(attempt, "fixture-outcome.txt"));
        Assert.Equal("updaterExited", File.ReadAllText(Path.Combine(attempt, "fixture-outcome.txt")));
        Assert.Equal("filesApplying", Read(config, "producer.json")["stage"]!.GetValue<string>());
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(attempt, "events")));
    }

    /// <summary>A non-2xx leaves redacted evidence pending; retry sends only the legacy allowlisted HTTP fields.</summary>
    [Fact]
    public async Task FailedHttpDelivery_RemainsPendingAndRetriesLegacyPayloadOnly()
    {
        var config = Config(launch: true);
        config.Token = "private-token";
        var handler = new CaptureHandler(HttpStatusCode.ServiceUnavailable);
        var reporter = new HttpUpdateReporter(new HttpClient(handler), "https://reports.example.test/status");
        await Run(config, new TestOs { LaunchError = new InvalidOperationException("private-token must stay local") }, reporter);
        var directory = AttemptPath(config);
        var pending = Assert.Single(Directory.GetFiles(Path.Combine(directory, "pending")));
        Assert.Contains("[REDACTED]", File.ReadAllText(pending));
        Assert.DoesNotContain("private-token", File.ReadAllText(pending));
        var payload = JsonNode.Parse(handler.Body!)!.AsObject();
        Assert.Equal(new[] { "recordId", "status", "type" }, payload.Select(p => p.Key));
        handler.Status = HttpStatusCode.OK;
        await UpdateAttempt.RetryPendingReportsAsync(directory, reporter);
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "pending")));
    }

    /// <summary>Direct callers also need an error signal, otherwise a durable queue would falsely delete unsent records.</summary>
    [Fact]
    public async Task HttpNonSuccess_ThrowsRatherThanAcknowledgingDelivery()
    {
        var reporter = new HttpUpdateReporter(new HttpClient(new CaptureHandler(HttpStatusCode.BadRequest)),
            "https://reports.example.test/status");
        await Assert.ThrowsAsync<HttpRequestException>(() => reporter.ReportAsync(new UpdateReport(42)));
    }

    /// <summary>Reject evidence paths that an application update or rollback could overwrite.</summary>
    [Fact]
    public async Task StateDirectoryInsideInstall_IsRejectedBeforeApply()
    {
        var config = Config();
        config.DiagnosticsDirectory = Path.Combine(config.InstallPath, "logs");
        var os = new TestOs();
        await Run(config, os, new CaptureReporter());
        Assert.False(os.Applied);
        Assert.False(Directory.Exists(config.DiagnosticsDirectory));
    }

    /// <summary>Both delta and full fallback fail: preserve the concrete exception and never apply a dependent later package.</summary>
    [Fact]
    public async Task PipelineAndFallbackFailure_PreserveOriginalErrorWithoutLaterSuccess()
    {
        var config = Config(launch: true);
        config.UpdateVersions =
        [
            new VersionEntry
            {
                RecordId = 42, Name = "broken", Version = "2.0.0",
                PackageType = 1, FallbackFullName = "fallback"
            },
            new VersionEntry { RecordId = 43, Name = "must-not-run", Version = "3.0.0" }
        ];
        var reporter = new CaptureReporter();
        var os = new TestOs { FailPipeline = true };
        await Run(config, os, reporter);
        Assert.False(os.AllPackagesSucceeded);
        Assert.False(os.Launched);
        Assert.IsType<InvalidDataException>(os.LastError);
        Assert.Equal(2, os.PipelinesBuilt);
        var failure = Assert.Single(reporter.Reports);
        Assert.Equal(3, failure.Status);
        Assert.Equal("broken", failure.PackageName);
        Assert.Equal("2.0.0", failure.PackageVersion);
        Assert.Equal(nameof(FailingMiddleware), failure.Error!.Stage);
        Assert.Contains("original pipeline failure", failure.Error.StackTrace);
    }

    /// <summary>Preparation failures retain the known download path and push trigger rather than substituting package AppType.</summary>
    [Fact]
    public async Task ClientDownloadFailure_PersistsKnownPathAndPushType()
    {
        var config = Config();
        config.Encoding = System.Text.Encoding.UTF8;
        config.AppSecretKey = "key";
        var reporter = new CaptureReporter();
        var strategy = new ClientStrategy(new FailedDownload()) { Reporter = reporter, DownloadSource = new AssetSource() };
        strategy.SetReportType(2);
        strategy.Create(config);
        await strategy.ExecuteAsync();
        Assert.DoesNotContain(reporter.Reports, report => report.Status == 2);
        var failure = Assert.Single(reporter.Reports.Where(report => report.Status == 3));
        Assert.Equal(2, failure.Type);
        Assert.Equal(42, failure.RecordId);
        Assert.Equal("downloading", failure.Error!.Stage);
        Assert.EndsWith("sample.zip", failure.Error.FailedPath);
        Assert.Contains("simulated download error", failure.Error.StackTrace);
    }

    /// <summary>OSS must inspect the orchestrator result instead of treating completion of its Task as download success.</summary>
    [Fact]
    public async Task OssDownloadFailure_DoesNotApplyOrReportSuccess()
    {
        var config = Config();
        var reporter = new CaptureReporter();
        var strategy = new OssStrategy(AppType.OssUpgrade)
        {
            Reporter = reporter, DownloadSource = new AssetSource(), DownloadOrchestrator = new FailedDownload()
        };
        strategy.Create(config);
        await strategy.ExecuteAsync();
        Assert.DoesNotContain(reporter.Reports, report => report.Status == 2);
        Assert.Contains(reporter.Reports, report => report.Status == 3 && report.Error!.Message!.Contains("simulated download error"));
    }

    /// <summary>Use production source-generated JSON to guard Client-to-Update correlation and option compatibility.</summary>
    [Fact]
    public void ProcessContract_PreservesAttemptAndMonitorConfiguration()
    {
        var config = Config(monitoring: true);
        config.UpdateAttemptId = Guid.NewGuid().ToString("D");
        config.Encoding = System.Text.Encoding.UTF8;
        config.AppSecretKey = "key";
        config.BackupDirectory = Path.Combine(_root, "backup");
        var contract = ConfigurationMapper.MapToProcessContract(config,
            [new VersionEntry { Name = "sample", Version = "2.0.0" }], [], [], [], 2);
        var json = JsonSerializer.Serialize(contract, GeneralUpdate.Core.JsonContext.ProcessContractJsonContext.Default.ProcessContract);
        var roundtrip = JsonSerializer.Deserialize(json, GeneralUpdate.Core.JsonContext.ProcessContractJsonContext.Default.ProcessContract)!;
        Assert.Equal(config.UpdateAttemptId, roundtrip.UpdateAttemptId);
        Assert.Equal(config.DiagnosticsDirectory, roundtrip.DiagnosticsDirectory);
        Assert.True(roundtrip.Monitoring!.Enabled);
        Assert.Equal(2, roundtrip.ReportType);
    }

    /// <summary>Keep handles to this test's peers only; cleanup must never terminate a process selected by a shared name.</summary>
    private void TrackHost(UpdateContext config)
    {
        var path = Path.Combine(AttemptPath(config), "ready.json");
        if (!File.Exists(path)) return;
        var id = Read(config, "ready.json")["host"]!["pid"]!.GetValue<int>();
        try { _processes.Add(Process.GetProcessById(id)); }
        catch (ArgumentException) { }
    }

    /// <summary>Bound inter-process observation so a broken fixture fails rather than leaving the suite hanging.</summary>
    private static async Task WaitForFile(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!File.Exists(path) && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.True(File.Exists(path), "Missing fixture output: " + path);
    }

    public void Dispose()
    {
        foreach (var process in _processes)
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
            process.Dispose();
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    /// <summary>Uses the real pipeline/error handling while substituting file work and the platform's self-exit behavior.</summary>
    private sealed class TestOs : AbstractStrategy
    {
        public bool Applied { get; private set; }
        public bool Launched { get; private set; }
        public Exception? LaunchError { get; init; }
        public Process? Application { get; init; }
        public Func<Task>? Apply { get; init; }
        public bool FailPipeline { get; init; }
        public int PipelinesBuilt { get; private set; }
        protected override PipelineBuilder BuildPipeline(PipelineContext context)
        {
            PipelinesBuilt++;
            var pipeline = new PipelineBuilder(context);
            return FailPipeline ? pipeline.UseMiddleware<FailingMiddleware>() : pipeline;
        }
        public override async Task ExecuteAsync()
        {
            Applied = true;
            if (Apply != null) await Apply();
            await base.ExecuteAsync();
        }

        public override async Task StartAppAsync()
        {
            if (LaunchError != null) throw LaunchError;
            Launched = true;
            using var app = Process.GetCurrentProcess();
            if (OnAppStarted != null) await OnAppStarted(Application ?? app);
        }
    }

    /// <summary>Supplies the same concrete error through normal and fallback pipelines to verify original evidence is retained.</summary>
    public sealed class FailingMiddleware : IMiddleware
    {
        public Task InvokeAsync(PipelineContext context) =>
            throw new InvalidDataException("original pipeline failure");
    }

    /// <summary>Deterministic version discovery without a server or network dependency.</summary>
    private sealed class AssetSource : IDownloadSource
    {
        public Task<DownloadSourceResult> ListAsync(CancellationToken token = default) =>
            Task.FromResult(new DownloadSourceResult
            {
                Assets = [new DownloadAsset("sample.zip", "https://example.test/sample.zip", 1, "hash", "2.0.0",
                    AppType: (int)AppType.Client) { RecordId = 42 }],
                HasMainUpdate = true
            });
    }

    /// <summary>Return a failed batch rather than throwing, exercising the caller's required result inspection.</summary>
    private sealed class FailedDownload : IDownloadOrchestrator
    {
        public Task<DownloadReport> ExecuteAsync(DownloadPlan plan, string destDir, int maxConcurrency = 3,
            IProgress<DownloadProgress>? progress = null, CancellationToken token = default) =>
            Task.FromResult(new DownloadReport(
                plan.Assets.Select(asset => new DownloadResult(asset, Path.Combine(destDir, asset.Name),
                    0, TimeSpan.Zero, 0, false, "simulated download error")).ToArray(),
                0, TimeSpan.Zero, 0, plan.Assets.Count));
    }

    /// <summary>Captures richer reporter objects so tests can distinguish evidence semantics from the HTTP wire shape.</summary>
    private sealed class CaptureReporter : IUpdateReporter
    {
        public List<UpdateReport> Reports { get; } = [];
        public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }
    }

    /// <summary>Inspects the actual serialized HTTP body and controls acknowledgment without making a network request.</summary>
    private sealed class CaptureHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = status;
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status);
        }
    }
}
