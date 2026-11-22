using System.Net;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Strategy;

namespace CoreTest.Download;

public sealed class PersistentUpdateReporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gu-report-" + Guid.NewGuid());

    [Theory]
    [InlineData(500, "{\"code\":200,\"body\":true}")]
    [InlineData(200, "{\"code\":500,\"body\":true}")]
    [InlineData(200, "{\"code\":200,\"body\":false}")]
    [InlineData(200, "{}")]
    [InlineData(200, "not json")]
    public async Task RejectedResponseIsPersistedAndReplayed(int status, string body)
    {
        var handler = new Handler(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) });
        var reporter = Create(handler);
        await Assert.ThrowsAnyAsync<Exception>(() => reporter.ReportAsync(new UpdateReport(10, 3)));
        Assert.Single(Directory.GetFiles(_directory, "*.json"));

        handler.Respond = _ => Success();
        await Create(handler).ReplayAsync();
        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
        Assert.Equal(new[] { 3, 3 }, handler.Statuses);
    }

    [Fact]
    public async Task OfflineThenRestartPreservesTerminalOrder()
    {
        var handler = new Handler(_ => throw new HttpRequestException("offline"));
        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(new UpdateReport(1, 3)));
        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(new UpdateReport(1, 2)));
        Assert.Equal(2, Directory.GetFiles(_directory, "*.json").Length);

        handler.Statuses.Clear();
        handler.Respond = _ => Success();
        await Create(handler).ReplayAsync();
        Assert.Equal(new[] { 3, 2 }, handler.Statuses);
        await Create(handler).ReplayAsync();
        Assert.Equal(new[] { 3, 2 }, handler.Statuses);
    }

    [Fact]
    public async Task FailureDetailsSurviveRestartAndReplayInOrder()
    {
        var reports = new[]
        {
            new UpdateReport(41, (int)UpdateStatus.Failure, 2)
            {
                Category = FailureCategory.Download,
                Phase = "Download",
                ErrorCode = "DOWNLOAD",
                Message = "Download failed."
            },
            new UpdateReport(42, (int)UpdateStatus.Failure, 1)
            {
                Category = FailureCategory.Apply,
                Phase = "Apply",
                ErrorCode = "APPLY",
                Message = "Apply failed."
            }
        };
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(reports[0]));
        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(reports[1]));

        var queuedJson = string.Join("", Directory.GetFiles(_directory, "*.json").Select(File.ReadAllText));
        Assert.DoesNotContain("example.test", queuedJson);
        Assert.DoesNotContain("secret", queuedJson);
        Assert.Equal(reports, Directory.GetFiles(_directory, "*.json")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize(File.ReadAllText(path), UpdateReportJsonContext.Default.UpdateReport)));

        handler.Bodies.Clear();
        handler.Statuses.Clear();
        handler.Respond = _ => Success();

        await Create(handler).ReplayAsync();

        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
        Assert.Equal(reports.Select(report => report.Status), handler.Statuses);
        Assert.Equal(reports.Length, handler.Bodies.Count);
        for (var index = 0; index < reports.Length; index++)
        {
            var json = handler.Bodies[index];
            var replayed = JsonSerializer.Deserialize(json, UpdateReportJsonContext.Default.UpdateReport);
            Assert.Equal(reports[index], replayed);

            var legacy = JsonSerializer.Deserialize<LegacyReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal((reports[index].RecordId, reports[index].Status, reports[index].Type),
                (legacy!.RecordId, legacy.Status, legacy.Type));
        }
    }

    [Fact]
    public async Task LostAcknowledgementCanDuplicateButCannotSkipFailure()
    {
        var handler = new Handler(_ => throw new HttpRequestException("response lost after server accepted"));
        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(new UpdateReport(2, 3)));
        handler.Respond = _ => Success();
        await Create(handler).ReplayAsync();
        Assert.Equal(new[] { 3, 3 }, handler.Statuses);
    }

    [Fact]
    public async Task ConcurrentReportersShareQueueAndNeverPersistCredentials()
    {
        var handler = new Handler(_ => throw new HttpRequestException("offline"));
        var tasks = Enumerable.Range(1, 8).Select(async i =>
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(new UpdateReport(i, 3)));
        });
        await Task.WhenAll(tasks);
        var files = Directory.GetFiles(_directory, "*.json");
        Assert.Equal(8, files.Length);
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            Assert.DoesNotContain("example.test", json);
            Assert.DoesNotContain("secret", json);
        }
        handler.Respond = _ => Success();
        await Create(handler).ReplayAsync();
        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
    }

    [Fact]
    public async Task StrictSenderRejectsUnconfiguredEndpoint()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpUpdateReporter().SendAsync(new UpdateReport(1)));
    }

    [Fact]
    public async Task CorruptQueueStopsWithoutSendingOrDeleting()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "00000000000000000001.json");
        File.WriteAllText(path, "{}");
        var handler = new Handler(_ => Success());
        await Assert.ThrowsAsync<InvalidDataException>(() => Create(handler).ReplayAsync());
        Assert.True(File.Exists(path));
        Assert.Empty(handler.Statuses);
    }

    [Fact]
    public void BootstrapRegistersConfiguredInstanceAndAllowsReplacement()
    {
        var bootstrap = new TestBootstrap();
        var reporter = Create(new Handler(_ => Success()));
        Assert.Same(reporter, bootstrap.UpdateReporter(reporter).ResolveReporter());
        Assert.IsType<HttpUpdateReporter>(bootstrap.UpdateReporter<HttpUpdateReporter>().ResolveReporter());
    }

    [Fact]
    public async Task CancellationAfterCommitKeepsReport()
    {
        using var cts = new CancellationTokenSource();
        var handler = new Handler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(handler).ReportAsync(new UpdateReport(1, 3), cts.Token));
        Assert.Single(Directory.GetFiles(_directory, "*.json"));
    }

    private PersistentUpdateReporter Create(Handler handler) =>
        new(_directory, new HttpUpdateReporter(new HttpClient(handler, false), "https://example.test/report?secret=value"));

    private static HttpResponseMessage Success() =>
        new(HttpStatusCode.OK) { Content = new StringContent("{\"code\":200,\"body\":true}", Encoding.UTF8, "application/json") };

    private sealed class TestBootstrap : AbstractBootstrap<TestBootstrap, IStrategy>
    {
        public IUpdateReporter? ResolveReporter() => ResolveExtension<IUpdateReporter>();
        public override Task<TestBootstrap> LaunchAsync() => Task.FromResult(this);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;
        public List<int> Statuses { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            using var json = JsonDocument.Parse(body);
            Statuses.Add(json.RootElement.GetProperty("status").GetInt32());
            return Respond(request);
        }
    }

    private sealed class LegacyReport
    {
        public int RecordId { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
    }
}
