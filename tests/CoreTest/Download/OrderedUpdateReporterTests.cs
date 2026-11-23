using System.Diagnostics;
using System.Net;
using System.Text.Json;
using GeneralUpdate.Core.Download.Reporting;

namespace CoreTest.Download;

public sealed class OrderedUpdateReporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gu-ordered-" + Guid.NewGuid());

    [Fact]
    public async Task ReplaysExactEventAfterLostResponseAndKeepsCountersAfterDrain()
    {
        var handler = new Handler { Fail = true, AcceptBeforeFailure = true };
        var reporter = Create(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => reporter.ReportAsync(new UpdateReport(10, 3)
        {
            Category = FailureCategory.Apply, Phase = "Apply", Message = "Apply failed."
        }));
        handler.Fail = false;
        await Create(handler).ReplayAsync();
        await Create(handler).ReportAsync(new UpdateReport(10, 2));
        await Create(handler).ReportAsync(new UpdateReport(10, 1, 2));
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
        Assert.Equal(new long[] { 1, 1, 2, 1 }, handler.Bodies.Select(Sequence));
        Assert.Equal(EventId(handler.Bodies[0]), EventId(handler.Bodies[1]));
        Assert.Equal(3, handler.Applied.Count);
        Assert.NotEqual(EventId(handler.Bodies[1]), EventId(handler.Bodies[2]));
        Assert.DoesNotContain("example.test", File.ReadAllText(Path.Combine(_directory, "ordered-reports.db")));
    }

    [Fact]
    public async Task LegacySenderDoesNotAddProtocolFields()
    {
        var handler = new Handler();
        await new HttpUpdateReporter(new HttpClient(handler, false), "https://example.test/report")
            .SendAsync(new UpdateReport(10));
        using var json = JsonDocument.Parse(Assert.Single(handler.Bodies));
        Assert.False(json.RootElement.TryGetProperty("eventId", out _));
        Assert.False(json.RootElement.TryGetProperty("sequence", out _));
    }

    [Fact]
    public async Task AuthorizationIdentityAndConflictFailClosed()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler, false);
        Assert.Throws<InvalidOperationException>(() => new OrderedUpdateReporter(_directory, client, "https://example.test/report", "tenant/database", false));
        await Create(handler).ReportAsync(new UpdateReport(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OrderedUpdateReporter(_directory, client, "https://example.test/report", "different tenant", true).ReplayAsync());
        Assert.Single(handler.Bodies);

        handler.Conflict = true;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).ReportAsync(new UpdateReport(10)));
        Assert.Contains("conflict", error.Message);
        handler.Conflict = false;
        await Create(handler).ReplayAsync();
        Assert.Equal(handler.Bodies[1], handler.Bodies[2]);
    }

    [Fact]
    public async Task ConcurrentProcessesAllocateUniqueSequences()
    {
        var workers = Enumerable.Range(0, 6).Select(_ => StartWorker("offline")).ToArray();
        foreach (var worker in workers)
        {
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            Assert.Equal(0, worker.ExitCode);
            worker.Dispose();
        }
        var handler = new Handler();
        await Create(handler).ReplayAsync();
        Assert.Equal(Enumerable.Range(1, 6).Select(i => (long)i), handler.Bodies.Select(Sequence));
        Assert.Equal(6, handler.Bodies.Select(EventId).Distinct().Count());
    }

    [Fact]
    public async Task KilledProcessReplaysCommittedPayload()
    {
        using var worker = StartWorker("hold");
        try
        {
            Assert.Equal("committed", await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60)));
        }
        finally
        {
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync();
        }
        var handler = new Handler();
        await Create(handler).ReplayAsync();
        await Create(handler).ReportAsync(new UpdateReport(10, 2));
        Assert.Equal(new long[] { 1, 2 }, handler.Bodies.Select(Sequence));
    }

    [Fact]
    public async Task ConcurrentInstancesAllocatePerRecord()
    {
        var handler = new Handler { Fail = true };
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
            await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).ReportAsync(new UpdateReport(10, 3)))));
        handler.Fail = false;
        handler.Bodies.Clear();
        await Create(handler).ReplayAsync();
        Assert.Equal(Enumerable.Range(1, 12).Select(i => (long)i), handler.Bodies.Select(Sequence));
        Assert.Equal(12, handler.Bodies.Select(EventId).Distinct().Count());
    }

    private OrderedUpdateReporter Create(Handler handler) =>
        new(_directory, new HttpClient(handler, false), "https://example.test/report", "tenant/database", true);

    private Process StartWorker(string mode)
    {
        var testAssembly = typeof(OrderedUpdateReporterTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"), typeof(ReportWorker.Marker).Assembly.Location, _directory, mode })
            start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    private static long Sequence(string body) => JsonDocument.Parse(body).RootElement.GetProperty("sequence").GetInt64();
    private static Guid EventId(string body) => JsonDocument.Parse(body).RootElement.GetProperty("eventId").GetGuid();

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public bool Fail { get; set; }
        public bool AcceptBeforeFailure { get; set; }
        public bool Conflict { get; set; }
        public List<string> Bodies { get; } = [];
        public HashSet<Guid> Applied { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = await request.Content!.ReadAsStringAsync(token);
            Bodies.Add(body);
            using var json = JsonDocument.Parse(body);
            if ((!Fail || AcceptBeforeFailure) && !Conflict &&
                json.RootElement.TryGetProperty("eventId", out var eventId))
                Applied.Add(eventId.GetGuid());
            if (Fail) throw new HttpRequestException("lost response");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Conflict
                    ? "{\"code\":400,\"errorCode\":\"Conflict\",\"body\":false}"
                    : "{\"code\":200,\"body\":true}")
            };
        }
    }
}
