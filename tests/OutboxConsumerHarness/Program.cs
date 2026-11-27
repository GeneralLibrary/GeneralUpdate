using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using GeneralUpdate.Core.Diagnostics;

if (args.Length != 5)
{
    Console.Error.WriteLine("Usage: OutboxConsumerHarness <fail-directory> <version> <guid> <loopback-https-endpoint> <server-certificate-pem>");
    return 1;
}

if (!Uri.TryCreate(args[3], UriKind.Absolute, out var endpoint) ||
    endpoint.Scheme != Uri.UriSchemeHttps ||
    !(string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
      IPAddress.TryParse(endpoint.Host, out var address) && IPAddress.IsLoopback(address)))
{
    Console.Error.WriteLine("The endpoint must be HTTPS on a loopback address or localhost.");
    return 1;
}

var tenant = Environment.GetEnvironmentVariable("OUTBOX_TEST_TENANT");
var appKey = Environment.GetEnvironmentVariable("OUTBOX_TEST_APP_KEY");
if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(appKey))
{
    Console.Error.WriteLine("Set OUTBOX_TEST_TENANT and OUTBOX_TEST_APP_KEY.");
    return 1;
}

try
{
    using var pinned = X509Certificate2.CreateFromPem(File.ReadAllText(args[4]));
    var pinnedBytes = pinned.RawData;
    using var handler = new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            certificate != null &&
            (errors & (SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)) == 0 &&
            certificate.RawData.AsSpan().SequenceEqual(pinnedBytes)
    };
    using var http = new HttpClient(handler);
    var upload = new ArtifactUploadClient(http, endpoint, tenant, appKey);
    var consumer = new DiagnosticOutboxConsumer(args[0], upload, TimeSpan.FromSeconds(30));
    var deleted = await consumer.ProcessCaseAsync(args[1], args[2]);
    Console.WriteLine(deleted ? "CONFIRMED_AND_DELETED" : "NOT_CONFIRMED_OR_SKIPPED");
    return deleted ? 0 : 2;
}
catch (Exception error)
{
    Console.Error.WriteLine($"{error.GetType().Name}: {error.Message}");
    return 1;
}
