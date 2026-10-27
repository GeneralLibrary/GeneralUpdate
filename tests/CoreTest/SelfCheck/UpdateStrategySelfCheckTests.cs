using System.Diagnostics;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;

namespace CoreTest.SelfCheck;

/// <summary>
///     The self-check switch matrix from the design's test plan (docs/self-check-design.md §12):
///     SelfCheckEnabled on/off × pipeline success/failure × verdict pass/fail, asserting the
///     manifest, verify-marker, report and launch behaviour of the verified-launch flow.
///     The stub main-application processes reuse the simulation commands from
///     <see cref="ProcessLaunchRuleTests"/> (immediate non-zero exit = unusable, survivor = usable).
/// </summary>
public sealed class UpdateStrategySelfCheckTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gu-selfcheck-{Guid.NewGuid():N}");
    private readonly List<Process> _processes = [];

    public void Dispose()
    {
        foreach (var process in _processes)
        {
            try { if (!process.HasExited) process.Kill(); } catch { /* best effort */ }
            process.Dispose();
        }
        try { Environments.SetEnvironmentVariable("UpgradeFail", string.Empty); } catch { /* best effort */ }
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private string InstallPath => Path.Combine(_root, "install");
    private string BackupPath => Path.Combine(_root, "backup");
    private string ManifestPath => Path.Combine(InstallPath, ManifestInfo.FileName);
    private string MarkerPath => Path.Combine(InstallPath, "generalupdate.verify.json");
    private const string MainAppName = "MyApp.exe";

    /// <summary>Seed the install tree: presence-rule target (new payload) + rollback material (old payload).</summary>
    private UpdateContext Config(bool selfCheck, bool failPipeline = false)
    {
        Directory.CreateDirectory(InstallPath);
        Directory.CreateDirectory(Path.Combine(_root, "state"));
        Directory.CreateDirectory(Path.Combine(_root, "staging"));
        Directory.CreateDirectory(BackupPath);
        File.WriteAllText(Path.Combine(InstallPath, MainAppName), "new-version-payload");
        File.WriteAllText(Path.Combine(BackupPath, MainAppName), "old-version-payload");
        new ManifestInfo { ClientVersion = "1.0.0" }.Save(ManifestPath);

        return new UpdateContext
        {
            InstallPath = InstallPath,
            DiagnosticsDirectory = Path.Combine(_root, "state"),
            TempPath = Path.Combine(_root, "staging"),
            ClientVersion = "1.0.0",
            LastVersion = "2.0.0",
            MainAppName = MainAppName,
            LaunchClientAfterUpdate = true,
            BackupEnabled = true,
            BackupDirectory = BackupPath,
            Format = Format.Zip,
            SelfCheckEnabled = selfCheck,
            SelfCheckStartupWindowSeconds = 1,
            UpdateVersions = [new VersionEntry { RecordId = 42, Version = "2.0.0", Name = "sample.zip" }],
        };
    }

    private SelfCheckTestOs CreateOs(bool failPipeline = false) => new(_processes) { FailPipeline = failPipeline };

    private static async Task<CaptureReporter> RunAsync(UpdateContext config, SelfCheckTestOs os)
    {
        var reporter = new CaptureReporter();
        var strategy = new UpdateStrategy { Reporter = reporter };
        strategy.SetOsStrategy(os);
        strategy.Create(config);
        await strategy.ExecuteAsync();
        return reporter;
    }

    [Fact]
    public async Task SelfCheckOff_PipelineSuccess_LegacyLaunchPath()
    {
        var os = CreateOs();
        var config = Config(selfCheck: false);

        var reporter = await RunAsync(config, os);

        Assert.True(os.Launched);                       // plain StartAppAsync path
        Assert.False(os.VerificationStarted);           // no verified launch
        Assert.False(File.Exists(MarkerPath));          // no verify marker
        Assert.Equal("2.0.0", ManifestInfo.Load(ManifestPath)!.ClientVersion);
        Assert.Contains(reporter.Reports, r => r.Status == (int)UpdateStatus.Success);
        Assert.DoesNotContain(reporter.Reports, r => r.Status == (int)UpdateStatus.Failure);
    }

    [Fact]
    public async Task SelfCheckOn_VerificationPasses_MarkerClearedAndAppKept()
    {
        var os = CreateOs();                            // stub survives the window → lenient pass
        var config = Config(selfCheck: true);

        var reporter = await RunAsync(config, os);

        Assert.True(os.VerificationStarted);
        Assert.False(os.Launched);                      // verified path does not use StartAppAsync
        Assert.False(File.Exists(MarkerPath));          // verdict rendered → marker deleted
        Assert.Equal("2.0.0", ManifestInfo.Load(ManifestPath)!.ClientVersion);
        Assert.Contains(reporter.Reports, r => r.Status == (int)UpdateStatus.Success);
        Assert.DoesNotContain(reporter.Reports, r => r.Status == (int)UpdateStatus.Failure);
        Assert.Equal("new-version-payload", File.ReadAllText(Path.Combine(InstallPath, MainAppName)));
    }

    [Fact]
    public async Task SelfCheckOn_VerificationFails_RollsBackAndReportsBothPhases()
    {
        var os = CreateOs();
        os.Verdict = ProcessLaunchRuleTests.ImmediateExit(3);   // start-and-die → unusable version
        var config = Config(selfCheck: true);

        var reporter = await RunAsync(config, os);

        Assert.True(os.VerificationStarted);
        Assert.False(File.Exists(MarkerPath));          // marker consumed by the verdict
        Assert.Equal("1.0.0", ManifestInfo.Load(ManifestPath)!.ClientVersion);   // manifest reverted
        Assert.Equal("old-version-payload", File.ReadAllText(Path.Combine(InstallPath, MainAppName))); // backup restored
        Assert.Equal("2.0.0", Environments.GetEnvironmentVariable("UpgradeFail"));               // re-arm the guard

        // Two evidence points of the same incident: the Startup judgment and the Rollback execution.
        var failures = reporter.Reports.Where(r => r.Status == (int)UpdateStatus.Failure).ToList();
        Assert.Contains(failures, r => r.Phase == UpdatePhases.Startup);
        Assert.Contains(failures, r => r.Phase == UpdatePhases.Rollback);
        Assert.All(failures, r => Assert.Equal(42, r.RecordId));
    }

    [Fact]
    public async Task SelfCheckOn_PipelineFails_NoVerificationNoManifestAdvance()
    {
        var os = CreateOs(failPipeline: true);
        var config = Config(selfCheck: true);

        var reporter = await RunAsync(config, os);

        Assert.False(os.VerificationStarted);           // pipeline failure is not a self-check matter
        Assert.False(File.Exists(MarkerPath));
        Assert.Equal("1.0.0", ManifestInfo.Load(ManifestPath)!.ClientVersion);   // never advanced
        Assert.DoesNotContain(reporter.Reports, r => r.Status == (int)UpdateStatus.Success);
        Assert.Contains(reporter.Reports, r => r.Status == (int)UpdateStatus.Failure);
    }

    /// <summary>OS-strategy double: empty pipeline (or failing middleware), stubbed verified launch.</summary>
    private sealed class SelfCheckTestOs(List<Process> processes) : AbstractStrategy
    {
        public bool Launched { get; private set; }
        public bool VerificationStarted { get; private set; }
        public bool FailPipeline { get; init; }
        public (string FileName, string Arguments) Verdict { get; set; } = ProcessLaunchRuleTests.StayAlive();

        protected override PipelineBuilder BuildPipeline(PipelineContext context)
        {
            var pipeline = new PipelineBuilder(context);
            return FailPipeline ? pipeline.UseMiddleware<FailingMiddleware>() : pipeline;
        }

        public override async Task ExecuteAsync()
        {
            // The empty pipeline (or the failing middleware) drives AllPackagesSucceeded via the
            // base loop; no real package files are needed because no middleware touches them.
            await base.ExecuteAsync();
        }

        public override async Task StartAppAsync()
        {
            Launched = true;
            // The strategy attaches OnAppStarted to record completion + deliver the Success report.
            if (OnAppStarted != null)
            {
                using var app = Process.GetCurrentProcess();
                await OnAppStarted(app);
            }
        }

        internal override Process StartProcessForVerification(string appName)
        {
            VerificationStarted = true;
            var process = ProcessLaunchRuleTests.StartStub(Verdict);
            processes.Add(process);
            return process;
        }
    }

    /// <summary>Pipeline middleware that always fails (pipeline-failure leg of the matrix).</summary>
    public sealed class FailingMiddleware : IMiddleware
    {
        public Task InvokeAsync(PipelineContext context) =>
            throw new InvalidDataException("original pipeline failure");
    }

    /// <summary>Captures richer reporter objects so tests can assert phases, not just statuses.</summary>
    private sealed class CaptureReporter : IUpdateReporter
    {
        public List<UpdateReport> Reports { get; } = [];

        public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }
    }
}
