using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using GeneralUpdate.Core.Download.Reporting;

if (args.Length is not (5 or 6) || !int.TryParse(args[4], out var recordId))
    throw new ArgumentException("Expected mode, outbox, report URL, certificate, record ID, optional fresh record ID.");

var mode = args[0];
using var root = X509Certificate2.CreateFromPem(File.ReadAllText(args[3]));
using var transport = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
transport.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
{
    if (certificate is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        return false;
    using var chain = new X509Chain();
    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    chain.ChainPolicy.CustomTrustStore.Add(root);
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    return chain.Build(certificate);
};
using var handler = new TestHandler(transport, mode);
using var client = new HttpClient(handler);
client.DefaultRequestHeaders.Add("X-Tenant-Id", "ordered-report-ci");
if (mode == "legacy")
{
    await new HttpUpdateReporter(client, args[2]).SendAsync(new UpdateReport(recordId, 2));
    return;
}

var reporter = new OrderedUpdateReporter(args[1], client, args[2], "ordered-report-ci/test-database", true,
    terminalRejectionConfirmed: mode.StartsWith("terminal-", StringComparison.Ordinal) || mode == "conflict");
switch (mode)
{
    case "terminal-seed":
        try
        {
            await reporter.ReportAsync(new UpdateReport(recordId, 3)
            {
                Category = FailureCategory.Apply, Phase = "Apply", ErrorCode = "CI-TERMINAL", Message = "CI failure"
            });
            throw new InvalidDataException("Seed delivery was not interrupted.");
        }
        catch (BeforeDeliveryException)
        {
            Console.WriteLine("Committed original payload before transport.");
        }
        break;
    case "terminal-reject":
        await ExpectReviewAsync(() => reporter.ReplayAsync());
        var rejected = await reporter.GetQuarantinedAsync();
        if (rejected.Count != 1 || rejected[0].Reason != "ReportTerminalConflict" ||
            handler.SentRecords.Count != 1 || handler.TerminalResponses != 1)
            throw new InvalidDataException("The real terminal response was not quarantined.");
        break;
    case "terminal-restart":
        if (args.Length != 6 || !int.TryParse(args[5], out var freshRecordId) || freshRecordId == recordId)
            throw new ArgumentException("terminal-restart requires a different fresh record ID.");
        var retained = await reporter.GetQuarantinedAsync();
        if (retained.Count != 1 || retained[0].Reason != "ReportTerminalConflict")
            throw new InvalidDataException("Quarantine did not survive process restart.");
        await ExpectReviewAsync(() => reporter.ReplayAsync());
        await ExpectReviewAsync(() => reporter.ReportAsync(new UpdateReport(recordId, 2)));
        if (handler.SentRecords.Count != 0)
            throw new InvalidDataException("The quarantined record was sent without operator action.");
        await ExpectReviewAsync(() => reporter.ReportAsync(new UpdateReport(freshRecordId, 2)));
        if (!handler.SentRecords.SequenceEqual(new[] { freshRecordId }))
            throw new InvalidDataException("Fresh record was not delivered independently.");
        if (!(await reporter.GetQuarantinedAsync()).SequenceEqual(retained))
            throw new InvalidDataException("Original quarantine changed after unrelated delivery.");
        await reporter.RetryQuarantinedAsync(retained[0].Id);
        await ExpectReviewAsync(() => reporter.ReplayAsync());
        if (!(await reporter.GetQuarantinedAsync()).SequenceEqual(retained) ||
            !handler.SentRecords.SequenceEqual(new[] { freshRecordId, recordId }) || handler.TerminalResponses != 1)
            throw new InvalidDataException("Operator retry did not preserve and re-quarantine the original event.");
        break;
    case "conflict":
        await ExpectReviewAsync(() => reporter.ReplayAsync());
        if ((await reporter.GetQuarantinedAsync()).Count != 0 ||
            handler.SentRecords.Count != 1 || handler.ConflictResponses != 1)
            throw new InvalidDataException("An ordinary conflict was quarantined or not sent.");
        break;
    case "lost":
        try
        {
            await reporter.ReportAsync(new UpdateReport(recordId, 3)
            {
                Category = FailureCategory.Apply, Phase = "Apply", ErrorCode = "CI-APPLY", Message = "CI failure"
            });
            throw new InvalidOperationException("The response was not dropped.");
        }
        catch (LostResponseException)
        {
            Console.WriteLine("Confirmed server success, then dropped response.");
        }
        break;
    case "replay":
        await reporter.ReplayAsync();
        break;
    case "success":
        await reporter.ReportAsync(new UpdateReport(recordId, 2));
        break;
    default:
        throw new ArgumentException("Unknown mode.");
}

static async Task ExpectReviewAsync(Func<Task> action)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new InvalidDataException("Expected explicit rejection/operator-review failure.");
}

sealed class LostResponseException : HttpRequestException;
sealed class BeforeDeliveryException : HttpRequestException;

sealed class TestHandler(HttpMessageHandler inner, string mode) : DelegatingHandler(inner)
{
    public List<int> SentRecords { get; } = [];
    public int TerminalResponses { get; private set; }
    public int ConflictResponses { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (mode == "terminal-seed") throw new BeforeDeliveryException();
        using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
        SentRecords.Add(payload.RootElement.GetProperty("recordId").GetInt32());
        if (mode == "legacy")
        {
            if (payload.RootElement.TryGetProperty("eventId", out _) ||
                payload.RootElement.TryGetProperty("sequence", out _))
                throw new InvalidDataException("Legacy report included ordered protocol fields.");
        }
        var response = await base.SendAsync(request, token);
        if (mode.StartsWith("terminal-", StringComparison.Ordinal) || mode == "conflict")
        {
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (response.StatusCode == HttpStatusCode.OK &&
                result.RootElement.GetProperty("code").GetInt32() == 400 &&
                result.RootElement.GetProperty("body").ValueKind == JsonValueKind.False)
            {
                var error = result.RootElement.GetProperty("errorCode").GetString();
                if (error == "ReportTerminalConflict") TerminalResponses++;
                if (error == "Conflict") ConflictResponses++;
            }
        }
        if (mode != "lost") return response;
        using (response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException("Server did not return HTTP success before response loss.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (json.RootElement.GetProperty("code").GetInt32() != 200 ||
                !json.RootElement.GetProperty("body").GetBoolean())
                throw new InvalidOperationException("Server did not confirm report before response loss.");
        }
        throw new LostResponseException();
    }
}
