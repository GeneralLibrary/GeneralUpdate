using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Executors;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Policy;
using Xunit;

namespace CoreTest.Download;

public class DownloadRobustnessTests
{
    [Fact]
    public async Task DefaultRetryPolicy_RetriesOnTransientFailure()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));
        int attempts = 0;

        var result = await policy.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < 3) throw new HttpRequestException("timeout");
            return "ok";
        }, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task DefaultRetryPolicy_DoesNotRetryOnPermanentFailure()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));
        int attempts = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("404 Not Found");
            }, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task DefaultRetryPolicy_ExponentialBackoff()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(10), backoffMultiplier: 2.0);
        int attempts = 0;
        var start = DateTime.UtcNow;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("timeout");
            }, CancellationToken.None));

        var elapsed = DateTime.UtcNow - start;
        // 3 attempts = 2 retries: delay 10ms + 20ms = at least 30ms
        Assert.InRange(elapsed.TotalMilliseconds, 25, 500);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RetryPolicy_RespectsCancellation()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 5, initialDelay: TimeSpan.FromSeconds(1));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                throw new HttpRequestException("timeout");
            }, cts.Token));
    }

    [Fact]
    public async Task RetryPolicy_RetriesOnIOException()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 2, initialDelay: TimeSpan.FromMilliseconds(1));
        int attempts = 0;

        var result = await policy.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < 2) throw new IOException("Network stream error");
            return "recovered";
        }, CancellationToken.None);

        Assert.Equal("recovered", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task RetryPolicy_ReturnsOnSuccessFirstTry()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));
        int attempts = 0;

        var result = await policy.ExecuteAsync(_ =>
        {
            attempts++;
            return Task.FromResult("first");
        }, CancellationToken.None);

        Assert.Equal("first", result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public void DefaultRetryPolicy_RetryableStatusCodes()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 3);
        int attempts = 0;

        Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("503 Service Unavailable");
            }, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.Equal(3, attempts);
    }

    [Fact]
    public void DefaultRetryPolicy_NoRetryOn402()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 3);
        int attempts = 0;

        Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("402 Payment Required");
            }, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.Equal(1, attempts);
    }
}
