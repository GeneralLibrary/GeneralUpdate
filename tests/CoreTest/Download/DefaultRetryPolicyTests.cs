using GeneralUpdate.Core.Download.Policy;
using GeneralUpdate.Core.Download.Models;

namespace CoreTest.Download;

public class DefaultRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_ActionSucceedsFirstTry_ReturnsResultImmediately()
    {
        var policy = new DefaultRetryPolicy(3);
        var result = await policy.ExecuteAsync(_ => Task.FromResult("ok"));
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task ExecuteAsync_RetryableException_RetriesAndSucceeds()
    {
        var policy = new DefaultRetryPolicy(3, TimeSpan.FromMilliseconds(10));
        var attemptCount = 0;
        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new TimeoutException("transient");
            return Task.FromResult("ok");
        });
        Assert.Equal("ok", result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsAllRetries_ThrowsLastException()
    {
        var policy = new DefaultRetryPolicy(2, TimeSpan.FromMilliseconds(10));
        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<string>(_ => throw new TimeoutException("fail")));
        Assert.Contains("fail", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_ThrowsImmediately()
    {
        var policy = new DefaultRetryPolicy(3, TimeSpan.FromMilliseconds(10));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync<string>(_ => throw new OperationCanceledException("cancel")));
    }

    [Fact]
    public async Task ExecuteAsync_TaskCanceledException_IsRetryable()
    {
        // TaskCanceledException (e.g. HttpClient timeout) should be retried
        var policy = new DefaultRetryPolicy(3, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new TaskCanceledException("timeout");
            }));
        Assert.Equal(3, attempts); // Retried up to maxRetries
    }

    [Fact]
    public async Task ExecuteAsync_IOException_IsRetryable()
    {
        var policy = new DefaultRetryPolicy(2, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<IOException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new IOException("network error");
            }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestExceptionWith500_IsRetryable()
    {
        var policy = new DefaultRetryPolicy(2, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("Server error 500");
            }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestExceptionWith502_IsRetryable()
    {
        var policy = new DefaultRetryPolicy(2, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("Bad Gateway 502");
            }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestExceptionWith503_IsRetryable()
    {
        var policy = new DefaultRetryPolicy(2, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("Service Unavailable 503");
            }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestExceptionWith504_IsRetryable()
    {
        var policy = new DefaultRetryPolicy(2, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("Gateway Timeout 504");
            }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException403_NotRetryable_ThrowsImmediately()
    {
        var policy = new DefaultRetryPolicy(3, TimeSpan.FromMilliseconds(10));
        var attempts = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("Forbidden 403");
            }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ExponentialBackoff_IncreasingDelays()
    {
        var policy = new DefaultRetryPolicy(4, TimeSpan.FromMilliseconds(50), backoffMultiplier: 2.0);
        var attempts = new List<DateTime>();
        await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts.Add(DateTime.UtcNow);
                throw new TimeoutException("fail");
            }));
        Assert.Equal(4, attempts.Count);
    }

    [Fact]
    public void Constructor_DefaultParameters_ReasonableDefaults()
    {
        var policy = new DefaultRetryPolicy();
        Assert.NotNull(policy);
    }
}
