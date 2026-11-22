using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;
using Moq;
using CoreTest.Shared;

namespace CoreTest.Strategy;

[Collection("NonParallel_EventManager")]
public class ReportRoutingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GU_Reports_{Guid.NewGuid():N}");
    private readonly IBlackMatcher? _blackMatcher = StorageManager.BlackMatcher;

    public ReportRoutingTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    public async Task ClientAndUpdater_RouteReportsAndDoNotMarkStagingSuccessful(int reportType, bool main, bool upgrade)
    {
        var assets = new List<DownloadAsset>();
        if (main) assets.Add(Asset("main", 101, AppType.Client));
        if (upgrade) assets.Add(Asset("upgrade", 202, AppType.Upgrade));
        var source = new Mock<IDownloadSource>();
        source.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadSourceResult { Assets = assets });
        var orchestrator = new Mock<IDownloadOrchestrator>();
        orchestrator.Setup(o => o.ExecuteAsync(It.IsAny<DownloadPlan>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadPlan plan, string _, int _, IProgress<DownloadProgress>? _, CancellationToken _) =>
                new DownloadReport(Array.Empty<DownloadResult>(), 0, TimeSpan.Zero, plan.Assets.Count, 0));
        var reporter = new RecordingReporter();
        var platform = new TestPlatform();
        var ipc = new EncryptedFileProcessContractProvider(Path.Combine(_directory, "ipc"));
        var client = new ClientStrategy
        {
            DownloadSource = source.Object, Reporter = reporter, LaunchAfterPrepare = false, ProcessContractProvider = ipc
        };
        client.SetOrchestrator(orchestrator.Object);
        client.SetOsStrategy(platform);
        client.SetReportType(reportType);
        var context = NewContext();
        client.Create(context);

        await client.ExecuteAsync();

        Assert.All(reporter.Reports, r => Assert.Equal(reportType, r.Type));
        if (upgrade)
            Assert.Contains(reporter.Reports, r => r.RecordId == 202 && r.Status == (int)UpdateStatus.Success);
        if (!main)
        {
            Assert.All(reporter.Reports, r => Assert.Equal(202, r.RecordId));
            Assert.False(client.HasPreparedClientUpdate);
            return;
        }

        Assert.True(client.HasPreparedClientUpdate);
        Assert.DoesNotContain(reporter.Reports, r => r.RecordId == 101 && r.Status == (int)UpdateStatus.Success);
        var contract = await ipc.ReceiveAsync();
        Assert.NotNull(contract);
        var serialized = JsonSerializer.Deserialize(context.ProcessContract, ProcessContractJsonContext.Default.ProcessContract)!;
        Assert.Equal(reportType, serialized.ReportType);
        Assert.Equal(reportType, contract.ReportType);
        var updaterContext = NewContext();
        updaterContext.UpdateVersions = contract.UpdateVersions;
        updaterContext.ReportType = contract.ReportType;
        updaterContext.LaunchClientAfterUpdate = false;
        var updater = new UpdateStrategy { Reporter = reporter };
        updater.SetOsStrategy(new TestPlatform());
        updater.Create(updaterContext);
        await updater.ExecuteAsync();
        Assert.Equal((int)UpdateStatus.Success, reporter.Reports.Last(r => r.RecordId == 101).Status);
        Assert.All(reporter.Reports, r => Assert.Equal(reportType, r.Type));
    }

    [Theory]
    [InlineData(1, (int)AppType.Upgrade)]
    [InlineData(2, (int)AppType.Client)]
    public async Task PackagesSharingRecord_FailureCannotBeOverwrittenByLaterSuccess(int reportType, int appType)
    {
        var reporter = new RecordingReporter();
        var platform = new TestPlatform { FailFirst = true, Reporter = reporter };
        var context = NewContext();
        context.ReportType = reportType;
        context.UpdateVersions = new()
        {
            new() { Name = "first", Version = "2.0.0", AppType = appType, RecordId = 42 },
            new() { Name = "second", Version = "3.0.0", AppType = appType, RecordId = 42 }
        };
        platform.Create(context);
        await platform.ExecuteAsync();
        var report = Assert.Single(reporter.Reports);
        Assert.Equal(new UpdateReport(42, (int)UpdateStatus.Failure, reportType), report);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetterBeforeOrAfterCreate_UpdatesPlatformAndRoleReports(bool beforeCreate)
    {
        var reporter = new RecordingReporter();
        var context = NewContext();
        context.LaunchClientAfterUpdate = false;
        context.UpdateVersions = new() { new() { Name = "main", Version = "2.0.0", AppType = 1, RecordId = 42 } };
        var updater = new UpdateStrategy { Reporter = reporter };
        updater.SetOsStrategy(new TestPlatform());
        if (beforeCreate) updater.SetReportType(2);
        updater.Create(context);
        if (!beforeCreate) updater.SetReportType(2);
        await updater.ExecuteAsync();
        Assert.NotEmpty(reporter.Reports);
        Assert.All(reporter.Reports, r => Assert.Equal(2, r.Type));
    }

    [Fact]
    public async Task Updater_LaunchFailureReportsFailureAfterAppliedSuccess()
    {
        var reporter = new RecordingReporter();
        var context = NewContext();
        context.UpdateVersions = new() { new() { Name = "main", Version = "2.0.0", AppType = 1, RecordId = 42 } };
        var updater = new UpdateStrategy { Reporter = reporter };
        updater.SetOsStrategy(new TestPlatform { FailLaunch = true });
        updater.Create(context);
        await updater.ExecuteAsync();
        Assert.Equal((int)UpdateStatus.Success, reporter.Reports.First().Status);
        Assert.Equal((int)UpdateStatus.Failure, reporter.Reports.Last().Status);
    }

    [Fact]
    public void OldContractDefaultsToPolling()
    {
        var contract = JsonSerializer.Deserialize("{}", ProcessContractJsonContext.Default.ProcessContract);
        Assert.Equal(1, contract!.ReportType);
    }

    private static DownloadAsset Asset(string name, int id, AppType type) =>
        new(name, $"https://example.test/{name}", 1, null, "2.0.0", RecordId: id, AppType: (int)type);

    private UpdateContext NewContext() => new()
    {
        InstallPath = _directory, TempPath = _directory, ClientVersion = "1.0.0",
        UpgradeClientVersion = "1.0.0", MainAppName = "app", UpdateAppName = "updater",
        AppSecretKey = FixtureValues.SingleKey, Encoding = Encoding.UTF8, Format = Format.Zip, BackupEnabled = false
    };

    private sealed class RecordingReporter : IUpdateReporter
    {
        public List<UpdateReport> Reports { get; } = new();
        public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }
    }

    private sealed class TestPlatform : AbstractStrategy
    {
        private int _calls;
        public bool FailFirst { get; init; }
        public bool FailLaunch { get; init; }
        protected override PipelineBuilder BuildPipeline(PipelineContext context)
        {
            if (FailFirst && _calls++ == 0) throw new InvalidOperationException("test failure");
            return new PipelineBuilder(context);
        }
        public override Task StartAppAsync() =>
            FailLaunch ? Task.FromException(new InvalidOperationException("test launch failure")) : Task.CompletedTask;
    }

    public void Dispose()
    {
        StorageManager.BlackMatcher = _blackMatcher;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
