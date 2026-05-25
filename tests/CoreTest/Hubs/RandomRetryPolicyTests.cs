using GeneralUpdate.Core.Hubs;
using Microsoft.AspNetCore.SignalR.Client;

namespace CoreTest.Hubs;

/// <summary>
/// Unit tests for <see cref="RandomRetryPolicy"/> following AAAT (Arrange-Act-Assert-TearDown).
/// Tests the SignalR connection retry behaviour of the RandomRetryPolicy.
/// </summary>
public class RandomRetryPolicyTests
{
    private readonly RandomRetryPolicy _policy = new();

    #region NextRetryDelay — within 60-second window

    [Fact]
    public void NextRetryDelay_ElapsedLessThan60Seconds_ReturnsDelayBetween0And10Seconds()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.FromSeconds(15),
            PreviousRetryCount = 2,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert
        Assert.NotNull(delay);
        Assert.True(delay >= TimeSpan.Zero,
            $"Expected delay >= 0, but got {delay}");
        Assert.True(delay <= TimeSpan.FromSeconds(10),
            $"Expected delay <= 10 seconds, but got {delay?.TotalSeconds}s");
    }

    [Fact]
    public void NextRetryDelay_ElapsedZero_StillReturnsDelayWithinRange()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.Zero,
            PreviousRetryCount = 0,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert
        Assert.NotNull(delay);
        Assert.True(delay >= TimeSpan.Zero);
        Assert.True(delay <= TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void NextRetryDelay_ElapsedNear60Seconds_StillProvidesDelay()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.FromSeconds(59.5),
            PreviousRetryCount = 10,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert
        Assert.NotNull(delay);
        Assert.True(delay <= TimeSpan.FromSeconds(10));
    }

    #endregion

    #region NextRetryDelay — beyond 60-second window

    [Fact]
    public void NextRetryDelay_Elapsed60Seconds_ReturnsNull_StopsReconnecting()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.FromSeconds(60),
            PreviousRetryCount = 20,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert
        Assert.Null(delay);
    }

    [Fact]
    public void NextRetryDelay_ElapsedMoreThan60Seconds_ReturnsNull()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.FromSeconds(120),
            PreviousRetryCount = 50,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert
        Assert.Null(delay);
    }

    #endregion

    #region NextRetryDelay — statistical distribution

    [Fact]
    public void NextRetryDelay_MultipleCalls_ProducesVariedResults()
    {
        // Arrange
        var delays = new HashSet<double>();
        const int iterations = 100;

        // Act
        for (var i = 0; i < iterations; i++)
        {
            var context = new RetryContext
            {
                ElapsedTime = TimeSpan.FromSeconds(i * 0.5), // varying elapsed time within 60s window
                PreviousRetryCount = i,
                RetryReason = null!
            };
            var delay = _policy.NextRetryDelay(context);
            Assert.NotNull(delay);
            delays.Add(delay!.Value.TotalMilliseconds);
        }

        // Assert — random results should not all be identical
        Assert.True(delays.Count > 1,
            $"Expected varied random delays, but only got {delays.Count} unique values across {iterations} calls.");
    }

    #endregion

    #region NextRetryDelay — edge cases

    [Fact]
    public void NextRetryDelay_ExactlyAtBoundary_60Seconds_ReturnsNull()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.FromSeconds(60),
            PreviousRetryCount = 0,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert — "less than 60" means exactly 60 is NOT less than 60
        Assert.Null(delay);
    }

    [Fact]
    public void NextRetryDelay_ExactlyOneMillisecondBeforeBoundary_ReturnsDelay()
    {
        // Arrange
        var context = new RetryContext
        {
            ElapsedTime = TimeSpan.FromSeconds(60) - TimeSpan.FromMilliseconds(1),
            PreviousRetryCount = 0,
            RetryReason = null!
        };

        // Act
        var delay = _policy.NextRetryDelay(context);

        // Assert
        Assert.NotNull(delay);
        Assert.True(delay <= TimeSpan.FromSeconds(10));
    }

    #endregion
}
