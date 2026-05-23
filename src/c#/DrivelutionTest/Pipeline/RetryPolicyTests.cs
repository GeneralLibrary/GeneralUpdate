using GeneralUpdate.Drivelution.Core.Pipeline;

namespace DrivelutionTest.Pipeline;

public class RetryPolicyTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var policy = RetryPolicy.Default;
        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.Delay);
        Assert.False(policy.UseExponentialBackoff);
    }

    [Fact]
    public void NoRetry_HasZeroMaxRetries()
    {
        var policy = RetryPolicy.NoRetry;
        Assert.Equal(0, policy.MaxRetries);
        Assert.Equal(TimeSpan.Zero, policy.Delay);
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt_ReturnsResult()
    {
        var policy = RetryPolicy.Default;
        int attempts = 0;
        
        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            await Task.CompletedTask;
            return 42;
        });

        Assert.Equal(42, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnFailure()
    {
        var policy = new RetryPolicy(2, TimeSpan.FromMilliseconds(10));
        int attempts = 0;

        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException($"Attempt {attempts}");
            await Task.CompletedTask;
            return "success";
        });

        Assert.Equal("success", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryOnCancellation()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromMilliseconds(100));
        int attempts = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(async ct =>
            {
                attempts++;
                ct.ThrowIfCancellationRequested();
                return 0;
            }, cts.Token));

        Assert.Equal(1, attempts); // single attempt, no retry on cancellation
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ReturnsFalseAfterExhaustingRetries()
    {
        var policy = new RetryPolicy(2, TimeSpan.FromMilliseconds(5));
        int attempts = 0;

        var result = await policy.ExecuteWithRetryAsync(async ct =>
        {
            attempts++;
            return false;
        });

        Assert.False(result);
        Assert.Equal(3, attempts); // 1 initial + 2 retries
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ReturnsTrueOnSuccess()
    {
        var policy = new RetryPolicy(2, TimeSpan.FromMilliseconds(5));
        int attempts = 0;

        var result = await policy.ExecuteWithRetryAsync(async ct =>
        {
            attempts++;
            return attempts >= 2;
        });

        Assert.True(result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void FromOptions_Null_ReturnsDefault()
    {
        var policy = RetryPolicy.FromOptions(null);
        Assert.Equal(RetryPolicy.Default.MaxRetries, policy.MaxRetries);
    }

    [Fact]
    public void FromOptions_UsesConfiguredValues()
    {
        var options = new GeneralUpdate.Drivelution.Abstractions.Configuration.DrivelutionOptions
        {
            DefaultRetryCount = 5,
            DefaultRetryIntervalSeconds = 10,
            UseExponentialBackoff = true
        };

        var policy = RetryPolicy.FromOptions(options);
        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(10), policy.Delay);
        Assert.True(policy.UseExponentialBackoff);
    }
}
