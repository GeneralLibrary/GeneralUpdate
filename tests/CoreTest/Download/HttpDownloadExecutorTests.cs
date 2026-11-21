using System.Diagnostics;
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

    // ── Inactivity (idle) timeout ──

    /// <summary>
    ///     Regression: the re-armed idle timer could not cancel BODY reads because the
    ///     download loop received the caller's token instead of the linked timeout token.
    ///     With ResponseHeadersRead, SendAsync has already completed and its cancellation
    ///     registration does not bound subsequent response-stream reads — a connection that
    ///     stalls after the headers occupied a download slot indefinitely.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StalledBodyAfterHeaders_IdleTimeoutCancelsRead()
    {
        var handler = new StallAfterHeadersHandler();
        var client = new HttpClient(handler);
        var executor = new HttpDownloadExecutor(client, TimeSpan.FromMilliseconds(400), enableResume: false);
        var asset = new DownloadAsset("test", "http://example.com/stall", 100, null, "1.0");
        var dest = Path.Combine(Path.GetTempPath(), $"stall-{Guid.NewGuid():N}.tmp");
        try
        {
            var sw = Stopwatch.StartNew();
            // The idle timeout surfaces as TaskCanceledException (retryable, same as a
            // headers-phase timeout) — not as a hung download.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => executor.ExecuteAsync(asset, dest));
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10),
                $"idle timeout did not interrupt the stalled body read (took {sw.Elapsed})");
        }
        finally { if (File.Exists(dest)) File.Delete(dest); }
    }

    [Fact]
    public async Task StreamDownloadAsync_IdleTimerRearms_AfterEachSuccessfulRead()
    {
        // The idle timer is re-armed after every successful read, so slow-but-alive
        // transfers keep making progress while a stall (> one idle window with no read)
        // is interrupted.
        using var idleCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var source = new ReadThenStallStream(readsBeforeStall: 3);
        using var dest = new MemoryStream();
        var sw = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HttpDownloadExecutor.StreamDownloadAsync(source, dest, -1, 0, "f", null, sw,
                idleCts.Token, null, idleCts, TimeSpan.FromMilliseconds(300)));

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10),
            $"re-armed idle timer did not interrupt the stall (took {sw.Elapsed})");
        Assert.Equal(3, dest.Length); // the three reads that made progress landed in the file
    }

    /// <summary>Returns a response whose body stream delivers nothing after the headers.</summary>
    private sealed class StallAfterHeadersHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ReadThenStallStream(readsBeforeStall: 0))
            };
            response.Content.Headers.ContentLength = 100;
            return Task.FromResult(response);
        }
    }

    /// <summary>Yields a few bytes, then stalls honoring the cancellation token (30 s cap).</summary>
    private sealed class ReadThenStallStream : Stream
    {
        private readonly int _readsBeforeStall;
        private int _reads;

        public ReadThenStallStream(int readsBeforeStall) => _readsBeforeStall = readsBeforeStall;

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (++_reads <= _readsBeforeStall)
            {
                buffer[offset] = (byte)'x';
                return 1;
            }
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
    }
}
