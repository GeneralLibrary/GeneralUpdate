using System.IO.Compression;
using System.Text.Json;
using GeneralUpdate.Core.Diagnostics;

namespace CoreTest.Diagnostics;

/// <summary>
///     Real-time bundle packing: the in-archive schema-2 manifest.json (server parsed-evidence
///     contract), the generated GUID that doubles as the server idempotency key, and the
///     channel identity propagation.
/// </summary>
public sealed class DiagnosticsPackagerTests : IDisposable
{
    private readonly string _installPath = Path.Combine(Path.GetTempPath(), $"gu-diag-pack-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_installPath, true); } catch { /* best effort */ }
    }

    private DiagnosticsOptions CreateOptions() => new()
    {
        Enabled = true,
        Url = "https://server.example.test/Fault/UploadArtifact",
        AppKey = "ak-123",
        ProductId = "p-001",
        Platform = 1,
        TargetName = "MyApp",
    };

    [Fact]
    public async Task CollectAsync_GeneratesGuid_AndWritesSchema2ManifestInsideZip()
    {
        Directory.CreateDirectory(_installPath);
        var collector = new DiagnosticsCollector(CreateOptions());

        var bundle = await collector.CollectAsync(new DiagnosticsCollectRequest(
            _installPath,
            DiagnosticsKinds.Rollback,
            "1.0.2",
            "self-check failed: process exited with code -1",
            RecordId: 42,
            RecordType: 1,
            RollbackResultJson: "{ \"filesRestored\": true }"), CancellationToken.None);

        Assert.NotNull(bundle);
        Assert.False(string.IsNullOrWhiteSpace(bundle!.Manifest.Guid));
        Assert.Equal("selfcheck", bundle.Manifest.Kind);
        Assert.Equal("ak-123", bundle.Manifest.AppKey);
        Assert.Equal("p-001", bundle.Manifest.ProductId);
        Assert.Equal(1, bundle.Manifest.Platform);
        Assert.Equal("MyApp", bundle.Manifest.TargetName);
        Assert.Equal(42, bundle.Manifest.RecordId);
        Assert.Equal(1, bundle.Manifest.RecordType);

        // In-archive manifest: same schema-2 disk contract as the Bowl outbox.
        using var archive = ZipFile.OpenRead(bundle.ZipPath);
        var manifestEntry = Assert.Single(archive.Entries, e => e.FullName == "manifest.json");
        using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<OutboxManifest>(stream);
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest!.Schema);
        Assert.Equal(bundle.Manifest.Guid, manifest.Guid);
        Assert.Equal("selfcheck", manifest.Kind);
        Assert.Equal("1.0.2", manifest.Version);
        Assert.Equal("MyApp", manifest.TargetName);

        TryDelete(bundle.ZipPath);
    }

    [Fact]
    public async Task CollectAsync_BareInstallPath_StillProducesManifestOnlyBundle()
    {
        // The in-archive manifest.json is always written, so even an install path without any
        // other artifact yields a deliverable (server-parseable) bundle — the "nothing to send"
        // case only remains for packs that fail the size cap.
        Directory.CreateDirectory(_installPath);
        var collector = new DiagnosticsCollector(CreateOptions());
        var request = new DiagnosticsCollectRequest(_installPath, DiagnosticsKinds.SelfCheck, "1.0.2", "reason");

        var bundle = await collector.CollectAsync(request, CancellationToken.None);

        Assert.NotNull(bundle);
        using var archive = ZipFile.OpenRead(bundle!.ZipPath);
        Assert.Contains(archive.Entries, e => e.FullName == "manifest.json");
        TryDelete(bundle.ZipPath);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
