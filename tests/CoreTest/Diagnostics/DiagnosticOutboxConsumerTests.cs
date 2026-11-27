using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Diagnostics;

namespace CoreTest.Diagnostics;

public sealed class DiagnosticOutboxConsumerTests : IDisposable
{
    private const string Id = "86c1ae61-89e9-450d-93b0-a7f512c06768";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gu-outbox-" + Guid.NewGuid().ToString("N"));
    private string CasePath => Path.Combine(_root, "1.0", Id);

    [Fact]
    public async Task RetryAfterRestartRetainsIdenticalBytesUntilHashConfirmed()
    {
        CreateCase();
        var sent = new List<byte[]>();
        var count = 0;
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            var file = ((MultipartFormDataContent)request.Content!).Single(p => p.Headers.ContentDisposition!.Name == "file");
            var bytes = await file.ReadAsByteArrayAsync();
            sent.Add(bytes);
            return ++count switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                2 => Response(null),
                _ => Response(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())
            };
        }));
        await Assert.ThrowsAsync<HttpRequestException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
        Assert.False(await Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
        Assert.True(await Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.False(Directory.Exists(CasePath));
        Assert.Equal(3, sent.Count);
        Assert.Equal(sent[0], sent[1]);
        Assert.Equal(sent[0], sent[2]);
    }

    [Fact]
    public async Task CorruptOrIncompleteSnapshotNeverRebuilds()
    {
        CreateCase();
        using var http = Http((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<HttpRequestException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        File.WriteAllBytes(Path.Combine(CasePath, ".generalupdate-outbox.zip"), [1, 2]);
        await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        File.Delete(Path.Combine(CasePath, ".generalupdate-outbox.zip"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task UnmarkedCasesRequireExplicitMigration()
    {
        CreateCase(marked: false);
        using var http = Http((_, _) => Task.FromResult(Response(null)));
        Assert.False(await Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.False(File.Exists(Path.Combine(CasePath, ".generalupdate-outbox.zip")));
        Assert.False(await Consumer(http, legacy: true).ProcessCaseAsync("1.0", Id));
        Assert.True(File.Exists(Path.Combine(CasePath, ".generalupdate-outbox.zip")));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("manifest.json")]
    [InlineData(".generalupdate-outbox.zip")]
    public async Task UnsafeEvidenceNamesAreRejected(string name)
    {
        CreateCase(evidenceName: name);
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        if (name == "../secret")
            await Assert.ThrowsAsync<ArgumentException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task UnexpectedEvidencePreventsUploadAndDeletion()
    {
        CreateCase();
        File.WriteAllText(Path.Combine(CasePath, "unexpected.txt"), "not in manifest");
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task MissingEvidenceAndUnpublishedCasesAreNotUploaded()
    {
        CreateCase();
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        File.Delete(Path.Combine(CasePath, "log.txt"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        File.Delete(Path.Combine(CasePath, "manifest.json"));
        Assert.False(await Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task WrongServerHashKeepsEvidence()
    {
        CreateCase();
        using var http = Http((_, _) => Task.FromResult(Response(new string('0', 64))));
        await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
        Assert.True(File.Exists(Path.Combine(CasePath, ".generalupdate-outbox.zip")));
    }

    [Fact]
    public async Task OversizedEvidenceIsRejected()
    {
        CreateCase();
        using (var file = File.OpenWrite(Path.Combine(CasePath, "log.txt"))) file.SetLength(19 * 1024 * 1024);
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task TimeoutKeepsSnapshotAndReleasesLock()
    {
        CreateCase();
        using var http = Http(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new Exception("Unexpected completion");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Consumer(http, timeout: TimeSpan.FromMilliseconds(100)).ProcessCaseAsync("1.0", Id));
        Assert.True(Directory.Exists(CasePath));
        using var stripe = new FileStream(Path.Combine(_root, ".locks", "86.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task AnotherProcessHoldingStripeMakesConsumerSkip()
    {
        CreateCase();
        Directory.CreateDirectory(Path.Combine(_root, ".locks"));
        var path = Path.Combine(_root, ".locks", "86.lock");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CoreTest.runtimeconfig.json"));
        start.ArgumentList.Add(typeof(OutboxLockHelper.Program).Assembly.Location);
        start.ArgumentList.Add(path);
        using var process = Process.Start(start)!;
        try
        {
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)));
            using var blockedHttp = Http((_, _) => throw new Exception("Unexpected upload"));
            Assert.False(await Consumer(blockedHttp).ProcessCaseAsync("1.0", Id));
            Assert.True(Directory.Exists(CasePath));
        }
        finally
        {
            await process.StandardInput.WriteLineAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        Assert.Equal(0, process.ExitCode);
        using var http = Http(async (request, _) =>
        {
            var file = ((MultipartFormDataContent)request.Content!).Single(p => p.Headers.ContentDisposition!.Name == "file");
            var bytes = await file.ReadAsByteArrayAsync();
            return Response(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        });
        Assert.True(await Consumer(http).ProcessCaseAsync("1.0", Id));
        Assert.False(Directory.Exists(CasePath));
    }

    [Fact]
    public void ConfirmedTombstoneCanBeCleanedWithoutTouchingPendingCase()
    {
        CreateCase();
        var tombstone = Path.Combine(_root, "1.0", ".generalupdate-uploaded-" + Id);
        Directory.CreateDirectory(tombstone);
        File.WriteAllText(Path.Combine(tombstone, "remaining.txt"), "already confirmed");
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        var consumer = Consumer(http);
        Assert.True(consumer.CleanupConfirmedCase("1.0", Id));
        Assert.False(Directory.Exists(tombstone));
        Assert.True(Directory.Exists(CasePath));
        Assert.False(consumer.CleanupConfirmedCase("1.0", Id));
    }

    private void CreateCase(bool marked = true, string evidenceName = "log.txt")
    {
        Directory.CreateDirectory(CasePath);
        if (evidenceName == "log.txt") File.WriteAllText(Path.Combine(CasePath, evidenceName), "diagnostic evidence");
        File.WriteAllText(Path.Combine(CasePath, "manifest.json"),
            $"{{\"schema\":2,{(marked ? "\"outboxProtocol\":1," : "")}\"guid\":\"{Id}\",\"kind\":\"crash\",\"version\":\"1.0\",\"targetName\":\"TestApp\",\"state\":\"pending\",\"files\":[\"{evidenceName}\"]}}");
    }

    private DiagnosticOutboxConsumer Consumer(HttpClient http, bool legacy = false, TimeSpan? timeout = null) =>
        new(_root, new ArtifactUploadClient(http, new Uri("https://example.test/upload"), "tenant", "app"),
            timeout ?? TimeSpan.FromSeconds(5), legacy);

    private static HttpClient Http(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Handler(send));

    private static HttpResponseMessage Response(string? hash) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($"{{\"code\":200,\"isSuccess\":true,\"body\":{{\"artifactId\":\"{Id}\",\"duplicate\":true,\"parsed\":true{(hash == null ? "" : $",\"contentSha256\":\"{hash}\"")}}}}}", Encoding.UTF8, "application/json")
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
