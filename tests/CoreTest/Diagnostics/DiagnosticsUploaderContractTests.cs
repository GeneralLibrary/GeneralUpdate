using System.Net;
using System.Text;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.Network;

namespace CoreTest.Diagnostics;

/// <summary>
///     Wire-contract tests of the diagnostics upload against the GeneralSpace
///     <c>POST /Fault/UploadArtifact</c> endpoint: multipart form-data layout, the guid
///     idempotency key, kind vocabulary and business-level acknowledgment.
/// </summary>
[Collection("NonParallel_EventManager")]
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
        Assert.Equal("[\"sysinfo.txt\"]", fields["files"]);

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

    [Theory]
    [InlineData("{\"code\":400,\"body\":{\"artifactId\":\"b3c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7\"}}")]
    [InlineData("{\"code\":500,\"body\":null}")]
    [InlineData("{\"code\":200,\"body\":{\"artifactId\":\"wrong\"}}")]
    [InlineData("{\"code\":200,\"body\":{}}")]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task UploadAsync_InvalidBusinessResponse_ReturnsFalse(string body)
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        handler.ResponseBody = body;
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true, Url = "https://server.example.test/Fault/UploadArtifact",
        }, new HttpClient(handler));

        Assert.False(await uploader.UploadAsync(new DiagnosticsBundle(CreateBundleZip(), CreateManifest()), CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_DuplicateResponseWithCaseInsensitiveKeys_IsAccepted()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        handler.ResponseBody = "{\"Code\":200,\"Body\":{\"ArtifactId\":\"b3c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7\",\"duplicate\":true}}";
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true, Url = "https://server.example.test/Fault/UploadArtifact",
        }, new HttpClient(handler));

        Assert.True(await uploader.UploadAsync(new DiagnosticsBundle(CreateBundleZip(), CreateManifest()), CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_AppliesConfiguredTenantHeader()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        var uploader = new DiagnosticsUploader(new DiagnosticsOptions
        {
            Enabled = true, Url = "https://server.example.test/Fault/UploadArtifact",
        }, new HttpClient(handler));
        HttpClientProvider.ExtraHeaders.TryGetValue("X-Tenant-Id", out var previous);
        try
        {
            HttpClientProvider.ExtraHeaders["X-Tenant-Id"] = "tenant-test";
            Assert.True(await uploader.UploadAsync(new DiagnosticsBundle(CreateBundleZip(), CreateManifest()), CancellationToken.None));
            Assert.Equal("tenant-test", handler.Requests.Single().TenantId);
        }
        finally
        {
            if (previous == null) HttpClientProvider.ExtraHeaders.TryRemove("X-Tenant-Id", out _);
            else HttpClientProvider.ExtraHeaders["X-Tenant-Id"] = previous;
        }
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
        public string? ResponseBody { get; set; }
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var captured = new CapturedRequest();
            if (request.Headers.TryGetValues("X-Tenant-Id", out var tenant))
                captured.TenantId = tenant.Single();
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
                Content = new StringContent(ResponseBody ??
                    $"{{\"code\":200,\"body\":{{\"artifactId\":\"{captured.FormField.GetValueOrDefault("guid")}\"}}}}",
                    Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class CapturedRequest
    {
        public Dictionary<string, string> FormField { get; } = new();
        public (string Name, string FileName) FilePart { get; set; }
        public string? TenantId { get; set; }
    }
}
