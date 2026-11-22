using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using GeneralUpdate.Core.Download.Reporting;

namespace CoreTest.Download;

/// <summary>
///     Wire projection of <see cref="HttpUpdateReporter"/> against the GeneralSpace ReportDTO:
///     the baseline trio stays byte-identical for plain reports; failure attribution
///     (phase/errorCode/message) only appears when populated.
/// </summary>
public sealed class HttpUpdateReporterWireTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string? Body { get; private set; }
        public string ResponseBody { get; set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody) };
        }
    }

    [Fact]
    public async Task PlainReport_KeepsLegacyThreeFieldPayload()
    {
        var handler = new CaptureHandler();
        var reporter = new HttpUpdateReporter(new HttpClient(handler), "https://reports.example.test/status");

        await reporter.ReportAsync(new UpdateReport(7, (int)UpdateStatus.Failure, 1));

        var payload = JsonNode.Parse(handler.Body!)!.AsObject();
        Assert.Equal(new[] { "recordId", "status", "type" }, payload.Select(p => p.Key));
        Assert.Equal(7, (int?)payload["recordId"]);
        Assert.Equal(3, (int?)payload["status"]);
    }

    [Fact]
    public async Task FailureAttribution_RidesTheWireWhenPopulated()
    {
        var handler = new CaptureHandler();
        var reporter = new HttpUpdateReporter(new HttpClient(handler), "https://reports.example.test/status");

        await reporter.ReportAsync(new UpdateReport(9, (int)UpdateStatus.Failure, 2)
        {
            Phase = UpdatePhases.Startup,
            Error = new UpdateFailure
            {
                Category = "healthCheckFailure",
                ExceptionType = "System.InvalidOperationException",
                Message = "self-check failed: process exited with code -1",
            }
        });

        var payload = JsonNode.Parse(handler.Body!)!.AsObject();
        Assert.Equal(new[] { "recordId", "status", "type", "phase", "errorCode", "message" }, payload.Select(p => p.Key));
        Assert.Equal("Startup", (string?)payload["phase"]);
        Assert.Equal("System.InvalidOperationException", (string?)payload["errorCode"]);
        Assert.Equal("self-check failed: process exited with code -1", (string?)payload["message"]);
    }

    [Fact]
    public async Task RollbackReport_CarriesRollbackPhase()
    {
        var handler = new CaptureHandler();
        var reporter = new HttpUpdateReporter(new HttpClient(handler), "https://reports.example.test/status");

        // Shape used by RollbackManager: phase=Rollback with the redacted failure reason as message.
        await reporter.ReportAsync(new UpdateReport(11, (int)UpdateStatus.Failure, 1)
        {
            Phase = UpdatePhases.Rollback,
            Error = new UpdateFailure { Category = "healthCheckFailure", Message = "[REDACTED]" }
        });

        var payload = JsonNode.Parse(handler.Body!)!.AsObject();
        Assert.Equal("Rollback", (string?)payload["phase"]);
        Assert.Equal("[REDACTED]", (string?)payload["message"]);
    }

    [Fact]
    public async Task EmptyReportUrl_IsNoOp()
    {
        var handler = new CaptureHandler();
        var reporter = new HttpUpdateReporter(new HttpClient(handler), string.Empty);

        await reporter.ReportAsync(new UpdateReport(1, (int)UpdateStatus.Failure, 1)
        {
            Phase = UpdatePhases.Rollback,
        });

        Assert.Null(handler.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"code\":400,\"isSuccess\":false,\"body\":false}")]
    [InlineData("{\"code\":200,\"body\":true}")]
    [InlineData("{\"code\":200,\"isSuccess\":true,\"body\":false}")]
    public async Task CredentialedReports_RejectUnconfirmedBusinessResponses(string response)
    {
        var handler = new CaptureHandler { ResponseBody = response };
        var reporter = new HttpUpdateReporter(new HttpClient(handler), "https://reports.example.test/status")
        {
            AppKey = "test-credential"
        };
        await Assert.ThrowsAnyAsync<Exception>(() => reporter.ReportAsync(new UpdateReport(7)));
        Assert.Equal("test-credential", (string?)JsonNode.Parse(handler.Body!)!["appKey"]);
    }

    [Fact]
    public async Task CredentialedReport_AcceptsConfirmedResponseAndRejectsRemoteHttp()
    {
        var handler = new CaptureHandler { ResponseBody = "{\"code\":200,\"isSuccess\":true,\"body\":true}" };
        var reporter = new HttpUpdateReporter(new HttpClient(handler), "https://reports.example.test/status")
        {
            AppKey = "test-credential"
        };
        await reporter.ReportAsync(new UpdateReport(7));
        reporter.ReportUrl = "http://reports.example.test/status";
        await Assert.ThrowsAsync<InvalidOperationException>(() => reporter.ReportAsync(new UpdateReport(7)));
        reporter.ReportUrl = "http://127.0.0.1/status";
        await reporter.ReportAsync(new UpdateReport(7));
    }
}
