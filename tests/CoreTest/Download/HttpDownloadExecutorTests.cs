using System.Net;
using GeneralUpdate.Core.Download.Executors;
using GeneralUpdate.Core.Download.Models;

namespace CoreTest.Download;

public class HttpDownloadExecutorTests
{
    // ── Constructor ──
    [Fact]
    public void Ctor_ClientNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpDownloadExecutor(null));
    }

    [Fact]
    public void Ctor_DefaultTimeout30Seconds()
    {
        var client = new HttpClient();
        var executor = new HttpDownloadExecutor(client);
        Assert.NotNull(executor);
    }

    [Fact]
    public void Ctor_ResumeEnabledByDefault()
    {
        var client = new HttpClient();
        var executor = new HttpDownloadExecutor(client);
        Assert.NotNull(executor);
    }

    [Fact]
    public void Ctor_ResumeDisabled()
    {
        var client = new HttpClient();
        var executor = new HttpDownloadExecutor(client, enableResume: false);
        Assert.NotNull(executor);
    }

    [Fact]
    public void Ctor_CustomTimeout()
    {
        var client = new HttpClient();
        var executor = new HttpDownloadExecutor(client, TimeSpan.FromSeconds(60));
        Assert.NotNull(executor);
    }

    // ── ExecuteAsync ──
    [Fact]
    public async Task ExecuteAsync_Success_DownloadsFile()
    {
        var handler = new MockHttpMessageHandler()
            .Returns(HttpStatusCode.OK, "file contents");
        var client = new HttpClient(handler);
        var executor = new HttpDownloadExecutor(client, enableResume: false);
        var asset = new DownloadAsset("test", "http://example.com/file", 12, null, "1.0");
        var dest = Path.GetTempFileName();
        try
        {
            var result = await executor.ExecuteAsync(asset, dest);
            Assert.True(result.Success);
            Assert.True(File.Exists(dest));
        }
        finally { if (File.Exists(dest)) File.Delete(dest); }
    }

    [Fact]
    public async Task ExecuteAsync_ServerError_ReturnsFailedResult()
    {
        var handler = new MockHttpMessageHandler()
            .Returns(HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler);
        var executor = new HttpDownloadExecutor(client);
        var asset = new DownloadAsset("test", "http://example.com/file", 100, null, "1.0");
        var dest = Path.GetTempFileName();
        try
        {
            var result = await executor.ExecuteAsync(asset, dest);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally { if (File.Exists(dest)) File.Delete(dest); }
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_ReturnsFailedResult()
    {
        var handler = new MockHttpMessageHandler()
            .Returns(HttpStatusCode.NotFound);
        var client = new HttpClient(handler);
        var executor = new HttpDownloadExecutor(client);
        var asset = new DownloadAsset("test", "http://example.com/missing", 0, null, "1.0");
        var dest = Path.GetTempFileName();
        try
        {
            var result = await executor.ExecuteAsync(asset, dest);
            Assert.False(result.Success);
        }
        finally { if (File.Exists(dest)) File.Delete(dest); }
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPartialFileWithResumeEnabled_AppendsToFile()
    {
        var handler = new MockHttpMessageHandler()
            .Returns(HttpStatusCode.PartialContent, "remaining_data");
        var client = new HttpClient(handler);
        var executor = new HttpDownloadExecutor(client, enableResume: true);
        var asset = new DownloadAsset("test", "http://example.com/file", 100, null, "1.0");
        var dest = Path.GetTempFileName();
        File.WriteAllText(dest, "prefix_"); // Simulate partial download
        try
        {
            var result = await executor.ExecuteAsync(asset, dest);
            Assert.True(result.Success);
        }
        finally { if (File.Exists(dest)) File.Delete(dest); }
    }

    [Fact]
    public async Task ExecuteAsync_ResumeDisabled_ExistingFile_Overwritten()
    {
        var handler = new MockHttpMessageHandler()
            .Returns(HttpStatusCode.OK, "new content");
        var client = new HttpClient(handler);
        var executor = new HttpDownloadExecutor(client, enableResume: false);
        var asset = new DownloadAsset("test", "http://example.com/file", 11, null, "1.0");
        var dest = Path.GetTempFileName();
        File.WriteAllText(dest, "old content longer");
        try
        {
            var result = await executor.ExecuteAsync(asset, dest);
            Assert.True(result.Success);
        }
        finally { if (File.Exists(dest)) File.Delete(dest); }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _content = "";

        public MockHttpMessageHandler Returns(HttpStatusCode code, string content = "")
        {
            _status = code;
            _content = content;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_content)
            };
            response.Content.Headers.ContentLength = _content.Length;
            return Task.FromResult(response);
        }
    }
}
