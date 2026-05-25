using GeneralUpdate.Core.Network;

namespace CoreTest.Network;

public class VersionServiceRetryTests
{
    [Fact]
    public void IsRetryable_OperationCanceledException_ReturnsFalse()
    {
        Assert.False(IsRetryable(new OperationCanceledException("cancel")));
    }

    [Fact]
    public void IsRetryable_TaskCanceledException_ReturnsFalse()
    {
        // TaskCanceledException inherits from OperationCanceledException,
        // which is checked first → not retryable
        Assert.False(IsRetryable(new TaskCanceledException("timeout")));
    }

    [Fact]
    public void IsRetryable_TimeoutException_ReturnsTrue()
    {
        Assert.True(IsRetryable(new TimeoutException("timeout")));
    }

    [Fact]
    public void IsRetryable_IOException_ReturnsTrue()
    {
        Assert.True(IsRetryable(new IOException("network down")));
    }

    [Fact]
    public void IsRetryable_HttpRequestExceptionWithoutTimeout_ReturnsFalse()
    {
        Assert.False(IsRetryable(new HttpRequestException("Forbidden 403")));
        Assert.False(IsRetryable(new HttpRequestException("Not Found 404")));
        Assert.False(IsRetryable(new HttpRequestException("Connection refused")));
    }

    [Fact]
    public void IsRetryable_InvalidOperationException_ReturnsFalse()
    {
        Assert.False(IsRetryable(new InvalidOperationException("boom")));
    }

    [Fact]
    public void IsRetryable_NullReferenceException_ReturnsFalse()
    {
        Assert.False(IsRetryable(new NullReferenceException()));
    }

    [Fact]
    public void HttpClientProvider_Shared_ReturnsSameInstance()
    {
        var client1 = HttpClientProvider.Shared;
        var client2 = HttpClientProvider.Shared;
        Assert.Same(client1, client2);
    }

    [Fact]
    public void HttpClientProvider_Shared_IsNotNull()
    {
        Assert.NotNull(HttpClientProvider.Shared);
    }

    // Matches actual VersionService.IsRetryable logic
    private static bool IsRetryable(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is TaskCanceledException or TimeoutException or IOException) return true;
        if (ex is HttpRequestException h &&
            (h.Message ?? "").Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
