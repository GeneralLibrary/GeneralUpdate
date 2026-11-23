using System.Net;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Diagnostics;

const string id = "86c1ae61-89e9-450d-93b0-a7f512c06768";
var root = Path.Combine(Path.GetTempPath(), "gu-aot-" + Guid.NewGuid().ToString("N"));
var casePath = Path.Combine(root, "1.0", id);
Directory.CreateDirectory(casePath);
try
{
    File.WriteAllText(Path.Combine(casePath, "log.txt"), "evidence");
    File.WriteAllText(Path.Combine(casePath, "manifest.json"),
        $"{{\"schema\":2,\"outboxProtocol\":1,\"guid\":\"{id}\",\"kind\":\"crash\",\"version\":\"1.0\",\"targetName\":\"TestApp\",\"state\":\"pending\",\"files\":[\"log.txt\"]}}");
    using var handler = new SmokeHandler(id);
    using var http = new HttpClient(handler);
    var client = new ArtifactUploadClient(http, new Uri("https://example.test/upload"), "tenant", "app");
    var consumer = new DiagnosticOutboxConsumer(root, client, TimeSpan.FromSeconds(5));
    if (await consumer.ProcessCaseAsync("1.0", id) || !Directory.Exists(casePath))
        throw new InvalidOperationException("Unconfirmed evidence was deleted.");
    var tombstone = Path.Combine(root, "2.0", ".generalupdate-uploaded-" + id);
    Directory.CreateDirectory(tombstone);
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var recovered = false;
    var host = new DiagnosticOutboxHost(new DiagnosticOutboxHostOptions
    {
        Enabled = true, FailDirectory = root, Endpoint = new Uri("https://example.test/upload"),
        TenantId = "tenant", AppKey = "app"
    }, e =>
    {
        if (e.Status == DiagnosticOutboxHostStatus.Error) throw e.Error!;
        if (e.Status == DiagnosticOutboxHostStatus.TombstoneDeleted) recovered = true;
        if (e.Status == DiagnosticOutboxHostStatus.ScanCompleted) stop.Cancel();
    }, http);
    await host.StartAsync(stop.Token);
    if (Directory.Exists(casePath) || handler.Attempts != 2)
        throw new InvalidOperationException("Host did not replay the persisted snapshot.");
    if (!recovered || Directory.Exists(tombstone))
        throw new InvalidOperationException("Host did not recover a confirmed tombstone.");
    Console.WriteLine("Outbox Native AOT smoke passed.");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, true);
}

sealed class SmokeHandler(string id) : HttpMessageHandler
{
    private byte[]? _first;
    public int Attempts { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var content = (MultipartFormDataContent)request.Content!;
        var file = content.Single(x => x.Headers.ContentDisposition!.Name == "file");
        var bytes = await file.ReadAsByteArrayAsync(token);
        Attempts++;
        if (_first != null && !_first.SequenceEqual(bytes)) throw new InvalidOperationException("Retry bytes changed.");
        _first = bytes;
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var hashField = Attempts == 1 ? "" : $",\"contentSha256\":\"{hash}\"";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"code\":200,\"isSuccess\":true,\"body\":{{\"artifactId\":\"{id}\",\"duplicate\":true,\"parsed\":true{hashField}}}}}", Encoding.UTF8, "application/json")
        };
    }
}
