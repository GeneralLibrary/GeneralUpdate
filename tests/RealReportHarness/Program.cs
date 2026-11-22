using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using GeneralUpdate.Core.Download.Reporting;

if (args.Length != 5 || !int.TryParse(args[4], out var recordId))
    throw new ArgumentException("Expected mode, outbox, report URL, certificate, record ID.");

var mode = args[0];
using var root = X509Certificate2.CreateFromPemFile(args[3]);
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
using var client = new HttpClient(new LostResponseHandler(transport, mode == "lost"));
client.DefaultRequestHeaders.Add("X-Tenant-Id", "ordered-report-ci");
var reporter = new OrderedUpdateReporter(args[1], client, args[2], "ordered-report-ci/test-database", true);
switch (mode)
{
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

sealed class LostResponseException : HttpRequestException;

sealed class LostResponseHandler(HttpMessageHandler inner, bool drop) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var response = await base.SendAsync(request, token);
        if (!drop) return response;
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
