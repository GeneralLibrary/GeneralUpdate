using System;
using System.Linq;
using System.Reflection;
using GeneralUpdate.Core.Network;
using Xunit;

namespace CoreTest.Network;

/// <summary>
/// Tests the REAL <c>VersionService.ShouldRetry</c> classifier (reached via reflection
/// because it is a private static helper) instead of a re-implemented copy, so the
/// production decision table cannot drift away from the tested behavior.
/// </summary>
public class VersionServiceRetryTests
{
    private static readonly Func<Exception, bool> ShouldRetry = CreateClassifier();

    private static Func<Exception, bool> CreateClassifier()
    {
        var type = typeof(VersionService).GetNestedType("VersionService", BindingFlags.NonPublic | BindingFlags.Public)
            ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "VersionService" && t.GetMethods().Any(m => m.Name == "Validate"))
            ?? throw new InvalidOperationException("VersionService type not found.");

        var method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name is "ShouldRetry" or "IsRetryable");
        return ex => (bool)method.Invoke(null, new object?[] { ex })!;
    }

    [Fact]
    public void IsRetryable_OperationCanceledException_ReturnsFalse()
    {
        // Caller-initiated cancellation must never be retried.
        Assert.False(ShouldRetry(new OperationCanceledException("cancel")));
    }

    [Fact]
    public void IsRetryable_TaskCanceledException_WithoutInner_ReturnsFalse()
    {
        // A plain TaskCanceledException (no inner timeout) is treated as caller
        // cancellation — TaskCanceledException derives from OperationCanceledException.
        Assert.False(ShouldRetry(new TaskCanceledException("timeout")));
    }

    [Fact]
    public void IsRetryable_TaskCanceledException_WrappingTimeout_ReturnsTrue()
    {
        // Regression: HttpClient timeouts surface as TaskCanceledException whose inner
        // exception is TimeoutException. The classifier used to hit the
        // OperationCanceledException branch first and never retried version validation
        // timeouts; it must classify the wrapped timeout as retryable.
        var timedOut = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout",
            new TimeoutException("The operation was canceled."));
        Assert.True(ShouldRetry(timedOut));
    }

    [Fact]
    public void IsRetryable_TimeoutException_ReturnsTrue()
    {
        Assert.True(ShouldRetry(new TimeoutException("timeout")));
    }

    [Fact]
    public void IsRetryable_IOException_ReturnsTrue()
    {
        Assert.True(ShouldRetry(new IOException("network down")));
    }

    [Fact]
    public void IsRetryable_HttpRequestExceptionWithTimeout_ReturnsTrue()
    {
        Assert.True(ShouldRetry(new HttpRequestException("The request failed due to a timeout")));
    }

    [Fact]
    public void IsRetryable_HttpRequestExceptionWithoutTimeout_ReturnsFalse()
    {
        Assert.False(ShouldRetry(new HttpRequestException("Forbidden 403")));
        Assert.False(ShouldRetry(new HttpRequestException("Not Found 404")));
        Assert.False(ShouldRetry(new HttpRequestException("Connection refused")));
    }

    [Fact]
    public void IsRetryable_InvalidOperationException_ReturnsFalse()
    {
        Assert.False(ShouldRetry(new InvalidOperationException("boom")));
    }

    [Fact]
    public void IsRetryable_NullReferenceException_ReturnsFalse()
    {
        Assert.False(ShouldRetry(new NullReferenceException()));
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
}
