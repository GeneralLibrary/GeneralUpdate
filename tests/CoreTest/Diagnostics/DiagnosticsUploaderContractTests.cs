using System.Net;
using System.Text;
using GeneralUpdate.Core.Diagnostics;

namespace CoreTest.Diagnostics;

/// <summary>
///     Wire-contract tests of the diagnostics upload against the GeneralSpace
///     <c>POST /Fault/UploadArtifact</c> endpoint: multipart form-data layout, the guid
///     idempotency key, kind vocabulary and the "2xx = delivered" rule.
/// </summary>
public sealed class DiagnosticsUploaderContractTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"gu-diag-up-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { /* best effort */ }
    }

    private string CreateBundleZip()
    {
        Directory.CreateDirectory(_tempRoot);
        var zipPath = Path.Combine(_tempRoot, $"{Guid.NewGuid():N}.zip");
        using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
        var entry = archive.CreateEntry("sysinfo.txt");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("os=unit-test");
        return zipPath;
    }

    private static CapturingHandler CreateHandler(HttpStatusCode status)
    {
        var handler = new CapturingHandler { ResponseStatus = status };
        return handler;
    }

    private static DiagnosticsManifest CreateManifest() => new()
    {
        Guid = "b3c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7",
        Kind = DiagnosticsKinds.SelfCheck,
        Version = "1.0.2",
        AppKey = "ak-123",
        ProductId = "p-001",
        Platform = 1,
        TargetName = "MyApp",
        RecordId = 42,
        RecordType = 1,
        Reason = "self-check failed: process exited with code -1",
        Files = ["sysinfo.txt"],
    };

    [Fact]
    public async Task UploadAsync_SendsMultipartContractFields()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true,
            Url = "https://server.example.test/Fault/UploadArtifact",
        }, new HttpClient(handler));
        var zip = CreateBundleZip();

        var uploaded = await uploader.UploadAsync(new DiagnosticsBundle(zip, CreateManifest()), CancellationToken.None);

        Assert.True(uploaded);
        var fields = handler.Requests.Single().FormField;
        Assert.Equal("b3c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7", fields["guid"]);
        Assert.Equal("selfcheck", fields["kind"]);
        Assert.Equal("2", fields["schema"]);
        Assert.Equal("1.0.2", fields["version"]);
        Assert.Equal("ak-123", fields["appKey"]);
        Assert.Equal("p-001", fields["productId"]);
        Assert.Equal("1", fields["platform"]);
        Assert.Equal("MyApp", fields["targetName"]);
        Assert.Equal("42", fields["recordId"]);
        Assert.Equal("1", fields["recordType"]);
        Assert.Contains("self-check failed", fields["reason"]);

        var filePart = handler.Requests.Single().FilePart;
        Assert.Equal("file", filePart.Name);
        Assert.Equal("b3c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7.zip", filePart.FileName);
    }

    [Fact]
    public async Task UploadAsync_OptionalUnknownFields_AreOmitted()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true,
            Url = "https://server.example.test/Fault/UploadArtifact",
        }, new HttpClient(handler));
        var zip = CreateBundleZip();
        var manifest = new DiagnosticsManifest
        {
            Guid = "a" + Guid.NewGuid().ToString("D")[1..],
            Kind = DiagnosticsKinds.Crash,
        };

        var uploaded = await uploader.UploadAsync(new DiagnosticsBundle(zip, manifest), CancellationToken.None);

        Assert.True(uploaded);
        var fields = handler.Requests.Single().FormField;
        Assert.Equal("crash", fields["kind"]);
        Assert.False(fields.ContainsKey("appKey"));
        Assert.False(fields.ContainsKey("version"));
        Assert.False(fields.ContainsKey("targetName"));
        Assert.False(fields.ContainsKey("productId"));
        Assert.False(fields.ContainsKey("platform"));
        Assert.False(fields.ContainsKey("recordId"));
        Assert.False(fields.ContainsKey("recordType"));
        Assert.False(fields.ContainsKey("reason"));
    }

    [Fact]
    public async Task UploadAsync_Non2xx_ReturnsFalse()
    {
        var handler = CreateHandler(HttpStatusCode.InternalServerError);
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true,
            Url = "https://server.example.test/Fault/UploadArtifact",
        }, new HttpClient(handler));
        var zip = CreateBundleZip();

        var uploaded = await uploader.UploadAsync(new DiagnosticsBundle(zip, CreateManifest()), CancellationToken.None);

        Assert.False(uploaded);
    }

    [Fact]
    public async Task UploadAsync_NotConfigured_SkipsAndReturnsFalse()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true,
            Url = string.Empty, // no endpoint
        }, new HttpClient(handler));
        var zip = CreateBundleZip();

        var uploaded = await uploader.UploadAsync(new DiagnosticsBundle(zip, CreateManifest()), CancellationToken.None);

        Assert.False(uploaded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void KindVocabulary_MatchesServerContract()
    {
        // GeneralSpace /Fault/UploadArtifact contract values (§3.2 of the adaptation design).
        Assert.Equal("crash", DiagnosticsKinds.Crash);
        Assert.Equal("selfcheck", DiagnosticsKinds.SelfCheck);
        Assert.Equal("update", DiagnosticsKinds.Update);
        // Rollback evidence is a self-check failure diagnostic — same bucket on the wire.
        Assert.Equal(DiagnosticsKinds.SelfCheck, DiagnosticsKinds.Rollback);
        // The real-time manifest aligns with the Bowl outbox disk contract (schema 2).
        Assert.Equal(2, DiagnosticsManifest.CurrentSchema);
    }

    /// <summary>Captures the multipart body of every request as form fields + the file part.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var captured = new CapturedRequest();
            if (request.Content is System.Net.Http.MultipartFormDataContent multipart)
            {
                foreach (var part in multipart)
                {
                    var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                    var fileName = part.Headers.ContentDisposition?.FileName?.Trim('"');
                    var text = await part.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(fileName))
                        captured.FilePart = (name ?? string.Empty, fileName);
                    else if (name != null)
                        captured.FormField[name] = text;
                }
            }

            Requests.Add(captured);
            return new HttpResponseMessage(ResponseStatus)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class CapturedRequest
    {
        public Dictionary<string, string> FormField { get; } = new();
        public (string Name, string FileName) FilePart { get; set; }
    }
}
