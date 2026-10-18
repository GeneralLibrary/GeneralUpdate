using System.Text.Json;
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
