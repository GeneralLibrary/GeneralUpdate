using System.Net;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Diagnostics;

namespace CoreTest.Diagnostics;

public class ArtifactUploadClientTests
{
    private const string Id = "86c1ae61-89e9-450d-93b0-a7f512c06768";
    private static readonly byte[] Zip = [0x50, 0x4b, 0x03, 0x04];

    [Fact]
    public async Task Upload_UsesExplicitIdentityAndMultipart()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("tenant-a", request.Headers.GetValues("X-Tenant-Id").Single());
            Assert.Equal("multipart/form-data", request.Content!.Headers.ContentType!.MediaType);
            Assert.True(request.Content.Headers.ContentLength < 20_971_520);
            var payload = await request.Content.ReadAsStringAsync();
            Assert.Contains("name=Guid", payload);
            Assert.Contains(Id, payload);
            Assert.Contains("name=AppKey", payload);
            Assert.Contains("app-a", payload);
            Assert.Contains("name=file", payload);
            return Response(Success());
        });
        var result = await Client(handler).UploadAsync(Metadata(), Zip);
        Assert.False(result.Duplicate);
        Assert.True(result.Parsed);
        Assert.False(result.HashVerified);
    }

    [Theory]
    [InlineData("{\"code\":400,\"isSuccess\":false,\"body\":null}")]
    [InlineData("{\"code\":500,\"isSuccess\":false,\"body\":null}")]
    [InlineData("{\"code\":200,\"isSuccess\":false,\"body\":{}}")]
    [InlineData("{\"code\":200,\"body\":{}}")]
    [InlineData("{\"code\":200,\"isSuccess\":true,\"body\":{\"artifactId\":\"other\",\"duplicate\":false,\"parsed\":true}}")]
    [InlineData("{\"code\":200,\"isSuccess\":true,\"body\":{\"artifactId\":\"86c1ae61-89e9-450d-93b0-a7f512c06768\"}}")]
    public async Task Http200WithInvalidBusinessResponse_IsRejected(string body)
    {
        var client = Client(new Handler(_ => Task.FromResult(Response(body))));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.UploadAsync(Metadata(), Zip));
    }

    [Fact]
    public async Task FailedUploadCanBeRetriedWithSameBytes_AndDuplicateIsNotHidden()
    {
        var attempts = 0;
        var payloads = new List<byte[]>();
        var handler = new Handler(async request =>
        {
            var parts = (MultipartFormDataContent)request.Content!;
            var file = parts.Single(p => p.Headers.ContentDisposition!.Name == "file");
            payloads.Add(await file.ReadAsByteArrayAsync());
            attempts++;
            return attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Response(Success(duplicate: true, parsed: false));
        });
        var client = Client(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.UploadAsync(Metadata(), Zip));
        var result = await client.UploadAsync(Metadata(), Zip);
        Assert.True(result.Duplicate);
        Assert.False(result.Parsed);
        Assert.False(result.HashVerified);
        Assert.Equal(payloads[0], payloads[1]);
    }

    [Fact]
    public async Task ServerHashMustMatchTheZip()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Zip)).ToLowerInvariant();
        var matching = Client(new Handler(_ => Task.FromResult(Response(Success(duplicate: true, hash: hash)))));
        Assert.True((await matching.UploadAsync(Metadata(), Zip)).HashVerified);
        var wrong = Client(new Handler(_ => Task.FromResult(Response(Success(hash: new string('0', 64))))));
        await Assert.ThrowsAsync<InvalidDataException>(() => wrong.UploadAsync(Metadata(), Zip));
    }

    [Fact]
    public async Task CallerMutationAfterSendDoesNotChangeTheSnapshot()
    {
        var bytes = (byte[])Zip.Clone();
        var expected = (byte[])bytes.Clone();
        var handler = new Handler(async request =>
        {
            bytes[0] = 0;
            var file = ((MultipartFormDataContent)request.Content!).Single(p => p.Headers.ContentDisposition!.Name == "file");
            Assert.Equal(expected, await file.ReadAsByteArrayAsync());
            return Response(Success(hash: Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant()));
        });
        Assert.True((await Client(handler).UploadAsync(Metadata(), bytes)).HashVerified);
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        var client = Client(new Handler(_ => throw new OperationCanceledException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UploadAsync(Metadata(), Zip));
    }

    [Fact]
    public async Task UnknownSchemaAndOversizedInputAreRejectedBeforeSending()
    {
        var handler = new Handler(_ => throw new Exception("Unexpected network request"));
        var client = Client(handler, maxZipBytes: 3);
        await Assert.ThrowsAsync<ArgumentException>(() => client.UploadAsync(Metadata(), Zip));
        var metadata = Metadata();
        metadata.Schema = 3;
        await Assert.ThrowsAsync<ArgumentException>(() => Client(handler).UploadAsync(metadata, Zip));
    }

    [Fact]
    public async Task MultipartOverheadCountsTowardServerLimit()
    {
        var handler = new Handler(_ => throw new Exception("Unexpected network request"));
        var client = Client(handler, maxZipBytes: 20_971_520);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.UploadAsync(Metadata(), new byte[20_971_520]));
    }

    [Fact]
    public async Task OversizedOrMalformedResponseIsRejected()
    {
        var oversized = Client(new Handler(_ => Task.FromResult(Response(new string('x', 65537)))));
        await Assert.ThrowsAsync<InvalidDataException>(() => oversized.UploadAsync(Metadata(), Zip));
        var malformed = Client(new Handler(_ => Task.FromResult(Response("{"))));
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => malformed.UploadAsync(Metadata(), Zip));
    }

    [Fact]
    public void RequiresHttpsAndExplicitIdentity()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response(Success()))));
        Assert.Throws<ArgumentException>(() => new ArtifactUploadClient(http, new Uri("http://example.test"), "tenant", "app"));
        Assert.Throws<ArgumentException>(() => new ArtifactUploadClient(http, new Uri("https://example.test"), "", "app"));
        Assert.Throws<ArgumentException>(() => new ArtifactUploadClient(http, new Uri("https://example.test"), "tenant", ""));
    }

    private static ArtifactUploadClient Client(Handler handler, long maxZipBytes = 1024) =>
        new(new HttpClient(handler), new Uri("https://example.test/Fault/UploadArtifact"), "tenant-a", "app-a", maxZipBytes);

    private static ArtifactUploadMetadata Metadata() => new()
    {
        Guid = Id, Kind = "crash", Schema = 2, Version = "1.0", TargetName = "client"
    };

    private static string Success(bool duplicate = false, bool parsed = true, string? hash = null) =>
        $"{{\"code\":200,\"isSuccess\":true,\"body\":{{\"artifactId\":\"{Id}\",\"duplicate\":{duplicate.ToString().ToLowerInvariant()},\"parsed\":{parsed.ToString().ToLowerInvariant()}{(hash == null ? "" : $",\"contentSha256\":\"{hash}\"")}}}}}";

    private static HttpResponseMessage Response(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
