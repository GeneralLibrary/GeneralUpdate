using System.Text.Json;
using System.Net;
using System.Text;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.SelfCheck;

namespace CoreTest.Diagnostics;

/// <summary>
///     PendingDiagnosticsScanner tests over the Bowl outbox disk contract:
///     schema tolerance, upload-and-delete on success, retry counting and the giveup state.
/// </summary>
public sealed class PendingDiagnosticsScannerTests : IDisposable
{
    private readonly string _installPath;

    public PendingDiagnosticsScannerTests()
    {
        _installPath = Path.Combine(Path.GetTempPath(), $"gu-diag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_installPath, true); } catch { /* best effort */ }
    }

    private string CreateOutboxCase(string version, string state = "pending", int attempts = 0, int schema = 2)
    {
        var caseDir = Path.Combine(_installPath, "fail", version, Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(caseDir);
        File.WriteAllText(Path.Combine(caseDir, "1.0.2_fail.json"), "{ \"crash\": true }");
        File.WriteAllText(Path.Combine(caseDir, "manifest.json"),
            $$"""{ "schema": {{schema}}, "guid": "{{Path.GetFileName(caseDir)}}", "kind": "crash", "version": "{{version}}", "state": "{{state}}", "attempts": {{attempts}} }""");
        return caseDir;
    }

    private PendingDiagnosticsScanner CreateScanner(FakeUploader uploader) =>
        new(_installPath, new DiagnosticsOptions
        {
            Enabled = true,
            Url = "https://diag.example.test/upload",
            MaxRetry = 3,
        }, uploader);

    [Fact]
    public async Task ScanAsync_UploadSucceeds_CaseDirectoryIsRemoved()
    {
        var caseDir = CreateOutboxCase("1.0.2");
        var uploader = new FakeUploader(succeeds: true);
        var scanner = CreateScanner(uploader);

        await scanner.ScanAsync(CancellationToken.None);

        Assert.False(Directory.Exists(caseDir)); // uploaded means deleted
        var uploaded = Assert.Single(uploader.Bundles);
        Assert.Contains("1.0.2_fail.json", uploaded.Manifest.Files);
        Assert.Equal("crash", uploaded.Manifest.Kind);
    }

    [Fact]
    public async Task ScanAsync_UnsupportedSchema_SkippedUntouched()
    {
        var caseDir = CreateOutboxCase("1.0.2", schema: 99);
        var uploader = new FakeUploader(succeeds: true);
        var scanner = CreateScanner(uploader);

        await scanner.ScanAsync(CancellationToken.None);

        Assert.True(Directory.Exists(caseDir));
        Assert.Empty(uploader.Bundles);
    }

    [Fact]
    public async Task ScanAsync_UploadFails_AttemptsIncremented_EvidenceKept()
    {
        var caseDir = CreateOutboxCase("1.0.2");
        var uploader = new FakeUploader(succeeds: false);
        var scanner = CreateScanner(uploader);

        await scanner.ScanAsync(CancellationToken.None);

        Assert.True(Directory.Exists(caseDir)); // evidence never deleted after a failure
        Assert.Single(uploader.Bundles);
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json")));
        Assert.Equal(1, manifest.RootElement.GetProperty("attempts").GetInt32());
        Assert.Equal("pending", manifest.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ScanAsync_RetryBudgetExhausted_MarkedGiveupAndNeverRetried()
    {
        var caseDir = CreateOutboxCase("1.0.2", attempts: 2); // one failure away from MaxRetry=3
        var uploader = new FakeUploader(succeeds: false);
        var scanner = CreateScanner(uploader);

        await scanner.ScanAsync(CancellationToken.None);
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json")));
        Assert.Equal("giveup", manifest.RootElement.GetProperty("state").GetString());

        // The next scan must not touch giveup cases at all.
        uploader.Bundles.Clear();
        await scanner.ScanAsync(CancellationToken.None);
        Assert.Empty(uploader.Bundles);
        Assert.True(Directory.Exists(caseDir));
    }

    [Fact]
    public async Task ScanAsync_MissingFailRoot_IsANoOp()
    {
        var scanner = CreateScanner(new FakeUploader(succeeds: true));
        await scanner.ScanAsync(CancellationToken.None); // must not throw
        Assert.True(true);
    }

    [Fact]
    public async Task ScanAsync_StateRootCase_RetriesWithoutGiveupAndDeletesOnlyOnSuccess()
    {
        var state = Path.Combine(_installPath, "state");
        var guid = Guid.NewGuid().ToString("D");
        var caseDir = Path.Combine(state, "crash-outbox", "1.0.2", guid);
        Directory.CreateDirectory(caseDir);
        File.WriteAllText(Path.Combine(caseDir, "crash.dmp"), "dump");
        File.WriteAllText(Path.Combine(caseDir, "manifest.json"),
            $$"""{ "schema": 2, "guid": "{{guid}}", "kind": "crash", "version": "1.0.2", "state": "pending", "attempts": 5 }""");
        var options = new DiagnosticsOptions { Enabled = true, MaxRetry = 0 };

        await new PendingDiagnosticsScanner(_installPath, options, new FakeUploader(false), stateRoot: state)
            .ScanAsync(CancellationToken.None);
        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json"))))
        {
            Assert.Equal(6, manifest.RootElement.GetProperty("attempts").GetInt32());
            Assert.Equal("pending", manifest.RootElement.GetProperty("state").GetString());
        }

        await new PendingDiagnosticsScanner(_installPath, options, new FakeUploader(true), stateRoot: state)
            .ScanAsync(CancellationToken.None);
        Assert.False(Directory.Exists(caseDir));
    }

    [Fact]
    public async Task ScanAsync_StateRootCaseWithoutDump_RetainsEvidence()
    {
        var state = Path.Combine(_installPath, "state");
        var guid = Guid.NewGuid().ToString("D");
        var caseDir = Path.Combine(state, "crash-outbox", "1.0.2", guid);
        Directory.CreateDirectory(caseDir);
        File.WriteAllText(Path.Combine(caseDir, "manifest.json"),
            $$"""{ "schema": 2, "guid": "{{guid}}", "kind": "crash", "state": "pending" }""");
        var uploader = new FakeUploader(true);

        await new PendingDiagnosticsScanner(_installPath, new DiagnosticsOptions { MaxRetry = 0 },
            uploader, stateRoot: state).ScanAsync(CancellationToken.None);

        Assert.Empty(uploader.Bundles);
        Assert.True(Directory.Exists(caseDir));
    }

    [Fact]
    public async Task ScanAsync_UnpublishedOrMismatchedCase_IsNeverUploaded()
    {
        var caseDir = CreateOutboxCase("1.0.2");
        var manifestPath = Path.Combine(caseDir, "manifest.json");
        File.WriteAllText(manifestPath,
            $$"""{ "schema": 2, "guid": "{{Guid.NewGuid():D}}", "kind": "crash", "state": "pending" }""");
        var uploader = new FakeUploader(true);

        await CreateScanner(uploader).ScanAsync(CancellationToken.None);

        Assert.Empty(uploader.Bundles);
        Assert.True(Directory.Exists(caseDir));
    }

    [Fact]
    public async Task ScanAsync_ActualUploader_OnlyDeletesAfterBusinessAcknowledgment()
    {
        var state = Path.Combine(_installPath, "state");
        var guid = Guid.NewGuid().ToString("D");
        var caseDir = Path.Combine(state, "crash-outbox", "1.0.2", guid);
        Directory.CreateDirectory(caseDir);
        File.WriteAllText(Path.Combine(caseDir, "crash.dmp"), "dump");
        File.WriteAllText(Path.Combine(caseDir, "manifest.json"),
            $$"""{ "schema": 2, "guid": "{{guid}}", "kind": "crash", "version": "1.0.2", "state": "pending" }""");
        var handler = new BusinessResponseHandler();
        using var client = new HttpClient(handler);
        var options = new DiagnosticsOptions { Enabled = true, Url = "https://example.test/Fault/UploadArtifact", MaxRetry = 0 };
        var scanner = new PendingDiagnosticsScanner(_installPath, options, new DiagnosticsUploader(options, client),
            stateRoot: state);

        handler.Response = $$"""{ "code": 400, "body": { "artifactId": "{{guid}}" } }""";
        await scanner.ScanAsync(CancellationToken.None);
        Assert.True(Directory.Exists(caseDir));
        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json"))))
            Assert.Equal(1, manifest.RootElement.GetProperty("attempts").GetInt32());

        handler.Response = $$"""{ "code": 200, "body": { "artifactId": "{{guid}}", "duplicate": true } }""";
        await scanner.ScanAsync(CancellationToken.None);
        Assert.False(Directory.Exists(caseDir));
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class BusinessResponseHandler : HttpMessageHandler
    {
        public string Response { get; set; } = string.Empty;
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Response, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeUploader(bool succeeds) : IDiagnosticsUploader
    {
        public List<DiagnosticsBundle> Bundles { get; } = new();

        public bool IsConfigured => true;

        public Task<bool> UploadAsync(DiagnosticsBundle bundle, CancellationToken ct)
        {
            Bundles.Add(bundle);
            return Task.FromResult(succeeds);
        }
    }
}
