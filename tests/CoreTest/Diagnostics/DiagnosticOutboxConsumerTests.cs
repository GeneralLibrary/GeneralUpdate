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
        var tombstone = Path.Combine(_root, "2.0", ".generalupdate-uploaded-" + Id);
        Directory.CreateDirectory(tombstone);
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
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var events = new List<DiagnosticOutboxHostEvent>();
            await Host(blockedHttp, e =>
            {
                events.Add(e);
                if (e.Status == DiagnosticOutboxHostStatus.ScanCompleted) stop.Cancel();
            }).StartAsync(stop.Token);
            Assert.Contains(events, e => e.Status == DiagnosticOutboxHostStatus.Deferred);
            Assert.True(Directory.Exists(tombstone));
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
        var recovered = await ScanOnce(http);
        Assert.Contains(recovered, e => e.Status == DiagnosticOutboxHostStatus.TombstoneDeleted);
        Assert.False(Directory.Exists(tombstone));
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

    [Fact]
    public async Task DisabledHostDoesNotRequireConfigurationOrTouchDiskOrNetwork()
    {
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        var events = new List<DiagnosticOutboxHostEvent>();
        var host = new DiagnosticOutboxHost(new DiagnosticOutboxHostOptions
        {
            FailDirectory = _root, ScanInterval = TimeSpan.Zero, UploadTimeout = TimeSpan.Zero
        }, events.Add, http);
        await host.StartAsync();
        Assert.False(Directory.Exists(_root));
        Assert.Equal(DiagnosticOutboxHostStatus.Disabled, Assert.Single(events).Status);
        Assert.Throws<InvalidOperationException>(() => { _ = host.StartAsync(); });
    }

    [Theory]
    [InlineData("endpoint")]
    [InlineData("tenant")]
    [InlineData("app")]
    [InlineData("http")]
    [InlineData("interval")]
    [InlineData("timeout")]
    public void EnabledHostRequiresExplicitTrustedConfiguration(string missing)
    {
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        var options = HostOptions();
        switch (missing)
        {
            case "endpoint": options.Endpoint = null; break;
            case "tenant": options.TenantId = ""; break;
            case "app": options.AppKey = ""; break;
            case "interval": options.ScanInterval = TimeSpan.Zero; break;
            case "timeout": options.UploadTimeout = TimeSpan.FromMinutes(3); break;
        }
        Assert.ThrowsAny<ArgumentException>(() =>
            new DiagnosticOutboxHost(options, _ => { }, missing == "http" ? null : http));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task StartupReplayPreservesBytesAcrossHostRestartAndDoesNotAttributeCrash()
    {
        CreateCase();
        var attempts = new List<byte[]>();
        using var http = Http(async (request, _) =>
        {
            Assert.Equal("tenant", Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
            var form = (MultipartFormDataContent)request.Content!;
            Assert.DoesNotContain(form, p => p.Headers.ContentDisposition!.Name is "RecordId" or "RecordType");
            Assert.Equal("app", await form.Single(p => p.Headers.ContentDisposition!.Name == "AppKey").ReadAsStringAsync());
            var bytes = await form.Single(p => p.Headers.ContentDisposition!.Name == "file").ReadAsByteArrayAsync();
            attempts.Add(bytes);
            return attempts.Count == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) :
                Response(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        });
        var first = await ScanOnce(http);
        Assert.Contains(first, e => e.Status == DiagnosticOutboxHostStatus.Error && e.Error is HttpRequestException);
        Assert.True(Directory.Exists(CasePath));
        var second = await ScanOnce(http);
        Assert.Contains(second, e => e.Status == DiagnosticOutboxHostStatus.ConfirmedAndDeleted);
        Assert.False(Directory.Exists(CasePath));
        Assert.Equal(2, attempts.Count);
        Assert.Equal(attempts[0], attempts[1]);
    }

    [Fact]
    public async Task PeriodicScanFindsNewCaseAndRetriesUnconfirmedSnapshot()
    {
        Directory.CreateDirectory(_root);
        var attempts = 0;
        var scans = 0;
        using var http = Http(async (request, _) =>
        {
            var file = ((MultipartFormDataContent)request.Content!).Single(p => p.Headers.ContentDisposition!.Name == "file");
            var bytes = await file.ReadAsByteArrayAsync();
            return Response(++attempts == 1 ? null : Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var events = new List<DiagnosticOutboxHostEvent>();
        var host = Host(http, e =>
        {
            events.Add(e);
            if (e.Status != DiagnosticOutboxHostStatus.ScanCompleted) return;
            if (++scans == 1) CreateCase();
            if (scans == 3) stop.Cancel();
        });
        await host.StartAsync(stop.Token);
        Assert.Equal(3, scans);
        Assert.Equal(2, attempts);
        Assert.Contains(events, e => e.Status == DiagnosticOutboxHostStatus.Deferred);
        Assert.False(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task TombstoneRecoveryAndInvalidCaseAreIsolated()
    {
        CreateCase();
        File.WriteAllText(Path.Combine(CasePath, "manifest.json"), "invalid");
        var tombstone = Path.Combine(_root, "2.0", ".generalupdate-uploaded-" + Id);
        Directory.CreateDirectory(tombstone);
        File.WriteAllText(Path.Combine(tombstone, "remaining.txt"), "confirmed");
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        var events = await ScanOnce(http);
        Assert.Contains(events, e => e.Status == DiagnosticOutboxHostStatus.Error && e.Path == CasePath);
        Assert.Contains(events, e => e.Status == DiagnosticOutboxHostStatus.TombstoneDeleted);
        Assert.True(Directory.Exists(CasePath));
        Assert.False(Directory.Exists(tombstone));
    }

    [Fact]
    public async Task MissingRootIsObservableAndRecoveredOnNextScan()
    {
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var scans = 0;
        var events = new List<DiagnosticOutboxHostEvent>();
        await Host(http, e =>
        {
            events.Add(e);
            if (e.Status != DiagnosticOutboxHostStatus.ScanCompleted) return;
            if (++scans == 1) Directory.CreateDirectory(_root);
            else stop.Cancel();
        }).StartAsync(stop.Token);
        Assert.Equal(2, scans);
        Assert.Single(events, e => e.Status == DiagnosticOutboxHostStatus.Error);
    }

    [Fact]
    public async Task CancellationStopsInFlightUploadWithoutBlockingStartOrDeletingEvidence()
    {
        CreateCase();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = Http(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new Exception("Unexpected upload completion");
        });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var events = new List<DiagnosticOutboxHostEvent>();
        var completion = Host(http, events.Add).StartAsync(stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(completion.IsCompleted);
        stop.Cancel();
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DiagnosticOutboxHostStatus.Stopped, events.Last().Status);
        Assert.DoesNotContain(events, e => e.Status == DiagnosticOutboxHostStatus.Error);
        Assert.True(Directory.Exists(CasePath));
        using var stripe = new FileStream(Path.Combine(_root, ".locks", "86.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task UploadTimeoutIsObservableAndRetainsEvidence()
    {
        CreateCase();
        using var http = Http(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new Exception("Unexpected upload completion");
        });
        var options = HostOptions();
        options.UploadTimeout = TimeSpan.FromMilliseconds(100);
        var events = await ScanOnce(http, options);
        Assert.Contains(events, e => e.Status == DiagnosticOutboxHostStatus.Error && e.Error is OperationCanceledException);
        Assert.True(Directory.Exists(CasePath));
    }

    [Fact]
    public async Task UnsafeVersionAndCaseLinksAreNotFollowed()
    {
        if (OperatingSystem.IsWindows()) return; // Windows symlink creation requires deployment-specific privileges.
        CreateCase();
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(external);
        var sentinel = Path.Combine(external, "sentinel");
        File.WriteAllText(sentinel, "preserve");
        Directory.CreateSymbolicLink(Path.Combine(_root, "linked-version"), external);
        Directory.CreateSymbolicLink(Path.Combine(_root, "1.0", "76c1ae61-89e9-450d-93b0-a7f512c06768"), external);
        using var http = Http((_, _) => Task.FromResult(Response(null)));
        var events = await ScanOnce(http);
        Assert.Equal(2, events.Count(e => e.Status == DiagnosticOutboxHostStatus.Error));
        Assert.Equal("preserve", File.ReadAllText(sentinel));
    }

    [Fact]
    public async Task ObserverFailureFaultsCompletionRatherThanBeingTreatedAsCaseError()
    {
        Directory.CreateDirectory(_root);
        using var http = Http((_, _) => throw new Exception("Unexpected upload"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Host(http, e =>
        {
            if (e.Status == DiagnosticOutboxHostStatus.ScanCompleted) throw new ArgumentException("observer failed");
        }).StartAsync());
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    private DiagnosticOutboxHostOptions HostOptions() => new()
    {
        Enabled = true, FailDirectory = _root, Endpoint = new Uri("https://example.test/upload"),
        TenantId = "tenant", AppKey = "app", ScanInterval = TimeSpan.FromMilliseconds(100)
    };

    private DiagnosticOutboxHost Host(HttpClient http, Action<DiagnosticOutboxHostEvent> notify) =>
        new(HostOptions(), notify, http);

    private async Task<List<DiagnosticOutboxHostEvent>> ScanOnce(HttpClient http, DiagnosticOutboxHostOptions? options = null)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var events = new List<DiagnosticOutboxHostEvent>();
        await new DiagnosticOutboxHost(options ?? HostOptions(), e =>
        {
            events.Add(e);
            if (e.Status == DiagnosticOutboxHostStatus.ScanCompleted) stop.Cancel();
        }, http).StartAsync(stop.Token);
        Assert.Contains(events, e => e.Status == DiagnosticOutboxHostStatus.ScanCompleted);
        return events;
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
