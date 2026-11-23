using System.Diagnostics;
using System.Net;
using System.Text.Json;
using GeneralUpdate.Core.Download.Reporting;
using Microsoft.Data.Sqlite;

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
    public async Task TerminalRejectionIsOptInAndPreservesPerRecordOrderingAcrossRestart()
    {
        var handler = new Handler { TerminalRecord = 10 };
        var legacy = Create(handler);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => legacy.ReportAsync(new UpdateReport(10, 3)));
        Assert.Empty(await legacy.GetQuarantinedAsync());
        var reporter = Create(handler, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reporter.ReplayAsync());
        var quarantined = Assert.Single(await Create(handler, true).GetQuarantinedAsync());
        Assert.Equal(handler.Bodies[0], quarantined.Payload);
        Assert.Equal("ReportTerminalConflict", quarantined.Reason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reporter.ReportAsync(new UpdateReport(10, 2)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reporter.ReportAsync(new UpdateReport(11, 3)));
        Assert.Equal(new[] { 10, 10, 11 }, handler.Bodies.Select(RecordId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => legacy.ReplayAsync());
        handler.TerminalRecord = null;
        await reporter.RetryQuarantinedAsync(quarantined.Id);
        await Create(handler, true).ReplayAsync();
        Assert.Equal(new[] { 10, 10, 11, 10, 10 }, handler.Bodies.Select(RecordId));
        Assert.Equal(quarantined.Payload, handler.Bodies[3]);
        Assert.Equal(2, Sequence(handler.Bodies[4]));
        Assert.Empty(await reporter.GetQuarantinedAsync());
    }

    [Theory]
    [InlineData("{\"code\":400,\"body\":false,\"errorCode\":\"Conflict\"}", 200)]
    [InlineData("{\"code\":200,\"body\":true,\"errorCode\":\"ReportTerminalConflict\"}", 200)]
    [InlineData("{\"code\":400,\"body\":false,\"errorCode\":\"ReportTerminalConflict\"}", 409)]
    [InlineData("{\"code\":400,\"body\":false,\"errorCode\":\"Other\"}", 200)]
    public async Task OtherResponsesNeverQuarantine(string body, int status)
    {
        var handler = new Handler { ResponseBody = body, ResponseStatus = status };
        var reporter = Create(handler, true);
        await Assert.ThrowsAnyAsync<Exception>(() => reporter.ReportAsync(new UpdateReport(10, 3)));
        Assert.Empty(await reporter.GetQuarantinedAsync());
        handler.ResponseBody = null;
        handler.ResponseStatus = 200;
        await reporter.ReplayAsync();
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }

    [Fact]
    public async Task QuarantineSurvivesProcessRestartAndOtherRecordIsDelivered()
    {
        using (var worker = StartWorker("terminal"))
        {
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            Assert.Equal(0, worker.ExitCode);
        }
        using (var worker = StartWorker("inspect"))
        {
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            Assert.Equal(0, worker.ExitCode);
        }
        var handler = new Handler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler, true).ReportAsync(new UpdateReport(11, 3)));
        Assert.Equal(11, RecordId(Assert.Single(handler.Bodies)));
        Assert.Single(await Create(handler, true).GetQuarantinedAsync());
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
    public async Task SlowDeliveryDoesNotBlockAnotherProcessFromCommitting()
    {
        using var first = StartWorker("hold");
        Process? second = null;
        try
        {
            Assert.Equal("committed", await first.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60)));
            second = StartWorker("offline");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = Path.Combine(_directory, "ordered-reports.db"),
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM outbox;";
                if ((long)command.ExecuteScalar()! == 2) break;
                await Task.Delay(100, timeout.Token);
            }
            Assert.False(first.HasExited);
        }
        finally
        {
            if (!first.HasExited) first.Kill(entireProcessTree: true);
            await first.WaitForExitAsync();
            if (second != null)
            {
                if (!second.HasExited) second.Kill(entireProcessTree: true);
                await second.WaitForExitAsync();
                second.Dispose();
            }
        }
        var handler = new Handler();
        await Create(handler).ReplayAsync();
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

    private OrderedUpdateReporter Create(Handler handler, bool terminalConfirmed = false) =>
        new(_directory, new HttpClient(handler, false), "https://example.test/report", "tenant/database", true,
            terminalRejectionConfirmed: terminalConfirmed);

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
    private static int RecordId(string body) => JsonDocument.Parse(body).RootElement.GetProperty("recordId").GetInt32();

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public bool Fail { get; set; }
        public bool AcceptBeforeFailure { get; set; }
        public bool Conflict { get; set; }
        public int? TerminalRecord { get; set; }
        public string? ResponseBody { get; set; }
        public int ResponseStatus { get; set; } = 200;
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
            return new HttpResponseMessage((HttpStatusCode)ResponseStatus)
            {
                Content = new StringContent(ResponseBody ?? (TerminalRecord == json.RootElement.GetProperty("recordId").GetInt32()
                    ? "{\"code\":400,\"errorCode\":\"ReportTerminalConflict\",\"body\":false}"
                    : Conflict
                    ? "{\"code\":400,\"errorCode\":\"Conflict\",\"body\":false}"
                    : "{\"code\":200,\"body\":true}"))
            };
        }
    }
}
