using System.Net;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Orchestrators;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;
using Moq;

namespace CoreTest.Strategy;

[Collection("NonParallel_EventManager")]
public class FailureReportingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"GU_Failures_{Guid.NewGuid():N}");
    private readonly IBlackMatcher? _matcher = StorageManager.BlackMatcher;

    public FailureReportingTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Client_DownloadAndVerificationFailures_ReportCorrectRecordsAndKeepOriginalErrors()
    {
        var networkError = new HttpRequestException("secret-token https://example.test/?password=secret");
        var handler = new CaptureHandler(networkError);
        using var http = new HttpClient(handler);
        var reporter = new HttpUpdateReporter(http, "https://report.test/Upgrade/Report");
        var assets = new[]
        {
            new DownloadAsset("network", "https://download.test/network", 1, null, "2.0.0", RecordId: 101, AppType: (int)AppType.Client),
            new DownloadAsset("checksum", "https://download.test/checksum", 1, "wrong", "2.0.0", RecordId: 202, AppType: (int)AppType.Upgrade)
        };
        var source = new Mock<IDownloadSource>();
        source.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadSourceResult { Assets = assets });
        var client = new ClientStrategy { DownloadSource = source.Object, Reporter = reporter, LaunchAfterPrepare = false };
        client.SetOrchestrator(new DefaultDownloadOrchestrator(http, new DownloadOrchestratorOptions { VerifyChecksum = true }));
        client.SetReportType(2);
        client.Create(NewContext());
        var errors = new List<MultiDownloadErrorEventArgs>();
        void OnError(object sender, MultiDownloadErrorEventArgs e) { if (ReferenceEquals(sender, client)) errors.Add(e); }
        EventManager.Instance.AddListener<MultiDownloadErrorEventArgs>(OnError);
        try { await client.ExecuteAsync(); }
        finally { EventManager.Instance.RemoveListener<MultiDownloadErrorEventArgs>(OnError); }

        Assert.Equal(2, errors.Count);
        Assert.Same(networkError, errors.Single(e => ((DownloadAsset)e.Version).RecordId == 101).Exception);
        Assert.IsType<InvalidDataException>(errors.Single(e => ((DownloadAsset)e.Version).RecordId == 202).Exception);
        var failures = handler.Reports.Where(r => r.GetProperty("status").GetInt32() == 3).ToArray();
        Assert.Equal(2, failures.Length);
        AssertReport(failures.Single(r => r.GetProperty("recordId").GetInt32() == 101), 101, 2, 0, "Download");
        AssertReport(failures.Single(r => r.GetProperty("recordId").GetInt32() == 202), 202, 2, 1, "Verify");
        Assert.DoesNotContain("secret", string.Join("", failures.Select(r => r.GetRawText())), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("verify", FailureCategory.Verify)]
    [InlineData("extract", FailureCategory.Extract)]
    [InlineData("apply", FailureCategory.Apply)]
    public async Task Updater_RealMiddlewareFailure_ReportsItsRecordAndStage(string stage, FailureCategory category)
    {
        var reporter = new RecordingReporter();
        var platform = new StagePlatform(stage);
        var context = NewContext();
        context.ReportType = 2;
        context.LaunchClientAfterUpdate = false;
        context.UpdateVersions = new() { new() { Name = "package", Version = "2.0.0", RecordId = 303, AppType = (int)AppType.Client, Hash = "bad" } };
        await File.WriteAllTextAsync(Path.Combine(_root, "package.zip"), "not a zip");
        var updater = new UpdateStrategy { Reporter = reporter };
        updater.SetOsStrategy(platform);
        updater.Create(context);
        var errors = new List<ExceptionEventArgs>();
        void OnError(object sender, ExceptionEventArgs e) { if (ReferenceEquals(sender, platform)) errors.Add(e); }
        EventManager.Instance.AddListener<ExceptionEventArgs>(OnError);
        try { await updater.ExecuteAsync(); }
        finally { EventManager.Instance.RemoveListener<ExceptionEventArgs>(OnError); }
        var report = Assert.Single(reporter.Reports);
        Assert.Equal(303, report.RecordId);
        Assert.Equal(2, report.Type);
        Assert.Equal(category, report.Category);
        Assert.Equal(category.ToString(), report.Phase);
        Assert.Equal((int)UpdateStatus.Failure, report.Status);
        var error = Assert.Single(errors);
        Assert.NotNull(error.Exception);
        Assert.Equal(303, error.RecordId);
        Assert.Equal(category, error.Category);
    }

    [Fact]
    public async Task RollbackFailure_ReportsOriginalFailureThenRollbackWithSameRecord()
    {
        var backup = Path.Combine(_root, "backup");
        Directory.CreateDirectory(backup);
        Directory.CreateDirectory(Path.Combine(backup, "blocked"));
        await File.WriteAllTextAsync(Path.Combine(_root, "blocked"), "prevents directory creation");
        var reporter = new RecordingReporter();
        var context = NewContext();
        context.BackupDirectory = backup;
        context.ReportType = 2;
        context.UpdateVersions = new() { new() { Name = "missing", Version = "2.0.0", RecordId = 404, Hash = "bad" } };
        var platform = new StagePlatform("verify") { Reporter = reporter };
        platform.Create(context);
        var errors = new List<ExceptionEventArgs>();
        void OnError(object sender, ExceptionEventArgs e) { if (ReferenceEquals(sender, platform)) errors.Add(e); }
        EventManager.Instance.AddListener<ExceptionEventArgs>(OnError);
        try { await platform.ExecuteAsync(); }
        finally { EventManager.Instance.RemoveListener<ExceptionEventArgs>(OnError); }
        Assert.Equal(new FailureCategory?[] { FailureCategory.Verify, FailureCategory.Rollback }, reporter.Reports.Select(r => r.Category));
        Assert.All(reporter.Reports, r => { Assert.Equal(404, r.RecordId); Assert.Equal(2, r.Type); });
        Assert.Equal(new FailureCategory?[] { FailureCategory.Verify, FailureCategory.Rollback }, errors.Select(e => e.Category));
        Assert.All(errors, e => { Assert.Equal(404, e.RecordId); Assert.NotNull(e.Exception); });
    }

    [Fact]
    public async Task FallbackFailure_ReportsFallbackStageForOriginalRecord()
    {
        var reporter = new RecordingReporter();
        var context = NewContext();
        context.ReportType = 2;
        context.UpdateVersions = new()
        {
            new() { Name = "chain", Version = "2.0.0", RecordId = 505, Hash = "bad",
                PackageType = (int)PackageType.Chain, FallbackFullName = "full" }
        };
        await File.WriteAllTextAsync(Path.Combine(_root, "full.zip"), "not a zip");
        var platform = new FallbackPlatform { Reporter = reporter };
        platform.Create(context);
        await platform.ExecuteAsync();
        var report = Assert.Single(reporter.Reports);
        Assert.Equal(505, report.RecordId);
        Assert.Equal(2, report.Type);
        Assert.Equal(FailureCategory.Extract, report.Category);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealFallback_CoveredChainCannotOverwriteFailureOrReportDurableSuccess(bool durable)
    {
        var packages = Path.Combine(_root, "packages");
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(packages);
        Directory.CreateDirectory(install);
        var corrupt = Path.Combine(packages, "broken.zip");
        await File.WriteAllTextAsync(corrupt, "not a zip");
        var full = Path.Combine(packages, "full.zip");
        using (var archive = ZipFile.Open(full, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("applied.txt").Open()))
            writer.Write("fallback applied");
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        var reporter = new RecordingReporter();
        var context = NewContext();
        context.InstallPath = install;
        context.TempPath = packages;
        context.ReportType = 2;
        context.DurableUpdateAttemptId = durable ? Guid.NewGuid() : null;
        context.DisableLegacyRollback = durable;
        context.UpdateVersions = new()
        {
            new() { Name = "broken", Version = "1.5.0", RecordId = 303, AppType = (int)AppType.Client,
                PackageType = (int)PackageType.Full, Hash = Hash(corrupt) },
            new() { Name = "missing-chain", Version = "2.0.0", RecordId = 101, AppType = (int)AppType.Upgrade,
                PackageType = (int)PackageType.Chain, FallbackFullName = "full",
                FallbackFullVersion = "3.0.0", FallbackFullHash = Hash(full) },
            new() { Name = "covered", Version = "2.5.0", RecordId = 303, AppType = (int)AppType.Client,
                PackageType = (int)PackageType.Chain },
            new() { Name = "also-covered", Version = "3.0.0", RecordId = 404, AppType = (int)AppType.Upgrade,
                PackageType = (int)PackageType.Chain }
        };
        var platform = new WindowsStrategy { Reporter = reporter };
        platform.Create(context);

        await platform.ExecuteAsync();

        Assert.Equal("fallback applied", await File.ReadAllTextAsync(Path.Combine(install, "applied.txt")));
        var failure = Assert.Single(reporter.Reports.Where(r => r.RecordId == 303));
        Assert.Equal((int)UpdateStatus.Failure, failure.Status);
        Assert.Equal(2, failure.Type);
        Assert.Equal(FailureCategory.Extract, failure.Category);
        if (durable)
            Assert.DoesNotContain(reporter.Reports, r => r.Status == (int)UpdateStatus.Success);
        else
            Assert.Equal(new[] { 101, 404 }, reporter.Reports.Where(r => r.Status == (int)UpdateStatus.Success)
                .Select(r => r.RecordId));
    }

    [Fact]
    public async Task CustomMiddlewareFailure_IsNotGuessed()
    {
        var reporter = new RecordingReporter();
        var context = NewContext();
        context.UpdateVersions = new() { new() { Name = "custom", Version = "2.0.0", RecordId = 606 } };
        var platform = new CustomPlatform { Reporter = reporter };
        platform.Create(context);
        await platform.ExecuteAsync();
        var report = Assert.Single(reporter.Reports);
        Assert.Equal(606, report.RecordId);
        Assert.Null(report.Category);
        Assert.Null(report.Phase);
        Assert.Null(report.ErrorCode);
        Assert.Null(report.Message);
    }

    [Fact]
    public async Task HttpReporter_OldPayloadUnchangedAndOldDtoIgnoresOptionalFields()
    {
        var handler = new CaptureHandler(new HttpRequestException());
        using var http = new HttpClient(handler);
        var reporter = new HttpUpdateReporter(http, "https://report.test/Upgrade/Report");
        await reporter.ReportAsync(new UpdateReport(17, 1, 2));
        await reporter.ReportAsync(new UpdateReport(17, 3, 2) { Category = FailureCategory.Extract, Phase = "Extract", ErrorCode = "EXTRACT", Message = "Extract failed." });
        Assert.Equal(3, handler.Reports[0].EnumerateObject().Count());
        var legacy = JsonSerializer.Deserialize<LegacyReport>(handler.Reports[1].GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal((17, 3, 2), (legacy!.RecordId, legacy.Status, legacy.Type));
        AssertReport(handler.Reports[1], 17, 2, 2, "Extract");
        var replay = JsonSerializer.Deserialize(handler.Reports[1].GetRawText(), UpdateReportJsonContext.Default.UpdateReport);
        Assert.Equal(new UpdateReport(17, 3, 2) { Category = FailureCategory.Extract, Phase = "Extract", ErrorCode = "EXTRACT", Message = "Extract failed." }, replay);
    }

    private static void AssertReport(JsonElement report, int id, int type, int category, string phase)
    {
        Assert.Equal(id, report.GetProperty("recordId").GetInt32());
        Assert.Equal(type, report.GetProperty("type").GetInt32());
        Assert.Equal(category, report.GetProperty("category").GetInt32());
        Assert.Equal(phase, report.GetProperty("phase").GetString());
        Assert.Equal(phase.ToUpperInvariant(), report.GetProperty("errorCode").GetString());
    }

    private UpdateContext NewContext() => new()
    {
        InstallPath = _root, TempPath = _root, ClientVersion = "1.0.0", UpgradeClientVersion = "1.0.0",
        MainAppName = "app", UpdateAppName = "updater", Encoding = Encoding.UTF8, Format = Format.Zip,
        BackupEnabled = false, VerifyChecksum = true
    };

    private sealed class StagePlatform(string stage) : AbstractStrategy
    {
        protected override PipelineBuilder BuildPipeline(PipelineContext context) => stage switch
        {
            "verify" => new PipelineBuilder(context).UseMiddleware<HashMiddleware>(),
            "extract" => new PipelineBuilder(context).UseMiddleware<CompressMiddleware>(),
            _ => new PipelineBuilder(context).UseMiddleware<PatchMiddleware>()
        };
    }

    private sealed class FallbackPlatform : AbstractStrategy
    {
        private int _calls;
        protected override PipelineBuilder BuildPipeline(PipelineContext context) => _calls++ == 0
            ? new PipelineBuilder(context).UseMiddleware<HashMiddleware>()
            : new PipelineBuilder(context).UseMiddleware<CompressMiddleware>();
    }

    private sealed class CustomPlatform : AbstractStrategy
    {
        protected override PipelineBuilder BuildPipeline(PipelineContext context) =>
            new PipelineBuilder(context).UseMiddleware<CustomMiddleware>();
    }

    public sealed class CustomMiddleware : IMiddleware
    {
        public Task InvokeAsync(PipelineContext context) => Task.FromException(new IOException("custom failure"));
    }

    private sealed class RecordingReporter : IUpdateReporter
    {
        public List<UpdateReport> Reports { get; } = new();
        public Task ReportAsync(UpdateReport report, CancellationToken token = default) { Reports.Add(report); return Task.CompletedTask; }
    }

    private sealed class CaptureHandler(Exception failure) : HttpMessageHandler
    {
        public List<JsonElement> Reports { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "report.test")
            {
                using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Reports.Add(doc.RootElement.Clone());
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (request.RequestUri.AbsolutePath.EndsWith("network", StringComparison.Ordinal)) throw failure;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("incorrect contents") };
        }
    }

    private sealed class LegacyReport
    {
        public int RecordId { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
    }

    public void Dispose()
    {
        StorageManager.BlackMatcher = _matcher;
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
