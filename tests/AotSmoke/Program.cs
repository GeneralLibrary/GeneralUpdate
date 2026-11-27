using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Network;
using GeneralUpdate.Differential.Differ;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var directory = Path.Combine(Path.GetTempPath(), "GeneralUpdateAotSmoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    Check(!JsonSerializer.IsReflectionEnabledByDefault, "JSON reflection must be disabled");
    var manifestPath = Path.Combine(directory, ManifestInfo.FileName);
    new ManifestInfo { MainAppName = "app", ClientVersion = "1.0", ProductId = "product" }.Save(manifestPath);
    Check(ManifestInfo.Load(manifestPath)?.ToUpdateRequest().ProductId == "product", "Manifest round trip failed");

    var ipc = new EncryptedFileProcessContractProvider(directory);
    await ipc.SendAsync(new ProcessContract
    {
        AppName = "app",
        CurrentVersion = "1.0",
        UpdateVersions = new List<VersionEntry> { new() { Version = "2.0", RecordId = 42 } }
    });
    var contract = await ipc.ReceiveAsync();
    Check(contract?.AppName == "app" && contract.UpdateVersions[0].RecordId == 42, "IPC round trip failed");
    Check(!File.Exists(EncryptedFileProcessContractProvider.GetDefaultFilePath(directory)), "IPC file was not removed");

    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = RespondToVersionRequest(listener);
    using var versionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var response = await VersionService.Validate($"http://127.0.0.1:{port}/validate", "1.0",
        AppType.Client, "", PlatformType.Windows, "product", ct: versionTimeout.Token);
    await server;
    Check(response.Body?.Count == 1 && response.Body[0].RecordId == 42, "Version response failed");

    using var handler = new RecordingHandler();
    using var client = new HttpClient(handler);
    var source = new OssDownloadSource(client, "http://localhost/versions");
    var assets = await source.ListAsync();
    Check(assets.Assets.Count == 1 && assets.Assets[0].Version == "2.0", "OSS version parsing failed");
    var reporter = new HttpUpdateReporter(client, "http://localhost/report");
    await reporter.ReportAsync(new UpdateReport(42, 2, 1));
    Check(handler.Report == "{\"recordId\":42,\"status\":2,\"type\":1}", "Update report payload failed");

    var crashPath = await new CrashReporter().GenerateReportAsync(new BowlContext
    {
        FailDirectory = directory,
        FailFileName = "crash.json",
        ProcessNameOrId = "app",
        ExtendedField = "2.0"
    }, new[] { "dump captured" }, CancellationToken.None);
    using var crash = JsonDocument.Parse(await File.ReadAllTextAsync(crashPath));
    Check(crash.RootElement.GetProperty("ProcessNameOrId").GetString() == "app" &&
        crash.RootElement.GetProperty("ProcdumpOutPutLines")[0].GetString() == "dump captured",
        "Crash report failed");

    var oldPath = Path.Combine(directory, "old.txt");
    var newPath = Path.Combine(directory, "new.txt");
    var patchPath = Path.Combine(directory, "patch.bin");
    var resultPath = Path.Combine(directory, "result.txt");
    await File.WriteAllTextAsync(oldPath, "old version of the application");
    await File.WriteAllTextAsync(newPath, "new version of the application with changes");
    var differ = new BsdiffDiffer();
    await differ.Clean(oldPath, newPath, patchPath);
    await differ.Dirty(oldPath, resultPath, patchPath);
    Check(await File.ReadAllTextAsync(resultPath) == await File.ReadAllTextAsync(newPath), "Binary patch failed");

    Console.WriteLine("Native AOT smoke passed");
}
finally
{
    Directory.Delete(directory, recursive: true);
}

static async Task RespondToVersionRequest(TcpListener listener)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
    using var stream = connection.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
    var requestLine = await reader.ReadLineAsync(timeout.Token);
    Check(requestLine?.StartsWith("POST /validate ", StringComparison.Ordinal) == true, "Wrong version request");
    var length = 0;
    string? line;
    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(timeout.Token)))
    {
        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            length = int.Parse(line["Content-Length:".Length..].Trim());
    }
    var body = new char[length];
    await reader.ReadBlockAsync(body.AsMemory(), timeout.Token);
    using var json = JsonDocument.Parse(new string(body));
    Check(json.RootElement.GetProperty("version").GetString() == "1.0" &&
        json.RootElement.GetProperty("productId").GetString() == "product", "Version request payload failed");
    const string response = "{\"code\":0,\"body\":[{\"recordId\":42,\"version\":\"2.0\"}],\"message\":\"ok\"}";
    var bytes = Encoding.UTF8.GetBytes(response);
    var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
    await stream.WriteAsync(headers, timeout.Token);
    await stream.WriteAsync(bytes, timeout.Token);
}

sealed class RecordingHandler : HttpMessageHandler
{
    public string? Report { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post)
        {
            Report = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"PacketName\":\"app\",\"Version\":\"2.0\",\"Url\":\"http://localhost/app.zip\",\"Hash\":\"abc\",\"PubTime\":\"2025-01-01T00:00:00\"}]")
        };
    }
}
