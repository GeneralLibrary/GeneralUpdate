using GeneralUpdate.Drivelution.Core.Pipeline;
using GeneralUpdate.Drivelution.Abstractions.Configuration;

namespace DrivelutionTest.Pipeline;

/// <summary>
/// RetryPolicy 测试
/// 分支覆盖点:
/// - 构造函数：正确设置 MaxRetries, Delay, UseExponentialBackoff
/// - Default 静态属性：3次重试, 5秒间隔, 无回退
/// - NoRetry 静态属性：0次重试, 零延迟
/// - FromOptions: options为null返回Default, options值有效则构建, 值为0/负使用默认值
/// - ExecuteAsync: 成功操作直接返回, 失败操作重试, OperationCanceledException不重试直接抛出
/// - ExecuteWithRetryAsync: 成功返回true, 失败重试, 耗尽重试返回false
/// - 指数退避：Delay按2^(attempt-1)翻倍
/// - 边界：MaxRetries=0时无重试
/// 触发条件：构造和执行操作
/// 预期结果：重试行为正确
/// </summary>
public class RetryPolicyTests
{
    [Fact(DisplayName = "RetryPolicy_构造函数_正确设置属性")]
    public void RetryPolicy_Constructor_SetsPropertiesCorrectly()
    {
        var policy = new RetryPolicy(5, TimeSpan.FromSeconds(10));

        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(10), policy.Delay);
        Assert.False(policy.UseExponentialBackoff);
    }

    [Fact(DisplayName = "RetryPolicy_构造函数_UseExponentialBackoff为true")]
    public void RetryPolicy_Constructor_ExponentialBackoffTrue()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1), true);

        Assert.Equal(3, policy.MaxRetries);
        Assert.True(policy.UseExponentialBackoff);
    }

    [Fact(DisplayName = "RetryPolicy_Default_返回3次重试5秒间隔")]
    public void RetryPolicy_Default_Returns3Retries5Seconds()
    {
        var policy = RetryPolicy.Default;

        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.Delay);
        Assert.False(policy.UseExponentialBackoff);
    }

    [Fact(DisplayName = "RetryPolicy_NoRetry_返回0次重试零延迟")]
    public void RetryPolicy_NoRetry_ReturnsZeroRetries()
    {
        var policy = RetryPolicy.NoRetry;

        Assert.Equal(0, policy.MaxRetries);
        Assert.Equal(TimeSpan.Zero, policy.Delay);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_null参数返回Default")]
    public void RetryPolicy_FromOptions_NullOptions_ReturnsDefault()
    {
        var policy = RetryPolicy.FromOptions(null);

        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.Delay);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_有效值返回对应策略")]
    public void RetryPolicy_FromOptions_ValidOptions_ReturnsCorrespondingPolicy()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 10,
            DefaultRetryIntervalSeconds = 15,
            UseExponentialBackoff = true
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(10, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(15), policy.Delay);
        Assert.True(policy.UseExponentialBackoff);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_RetryCount为0使用默认3")]
    public void RetryPolicy_FromOptions_RetryCountZero_UsesDefault3()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 0,
            DefaultRetryIntervalSeconds = 5
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(3, policy.MaxRetries);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_RetryCount为负数使用默认3")]
    public void RetryPolicy_FromOptions_RetryCountNegative_UsesDefault3()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = -5,
            DefaultRetryIntervalSeconds = 5
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(3, policy.MaxRetries);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_RetryInterval为0使用默认5")]
    public void RetryPolicy_FromOptions_RetryIntervalZero_UsesDefault5()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 3,
            DefaultRetryIntervalSeconds = 0
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(TimeSpan.FromSeconds(5), policy.Delay);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_RetryInterval为负数使用默认5")]
    public void RetryPolicy_FromOptions_RetryIntervalNegative_UsesDefault5()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 3,
            DefaultRetryIntervalSeconds = -2
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(TimeSpan.FromSeconds(5), policy.Delay);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_所有值为0使用默认值")]
    public void RetryPolicy_FromOptions_AllZero_UsesDefaults()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 0,
            DefaultRetryIntervalSeconds = 0
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.Delay);
        Assert.False(policy.UseExponentialBackoff);
    }

    [Fact(DisplayName = "RetryPolicy_FromOptions_使用指数退避")]
    public void RetryPolicy_FromOptions_UseExponentialBackoff()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 5,
            DefaultRetryIntervalSeconds = 2,
            UseExponentialBackoff = true
        };

        var policy = RetryPolicy.FromOptions(options);

        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), policy.Delay);
        Assert.True(policy.UseExponentialBackoff);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteAsync_成功操作立即返回结果")]
    public async Task RetryPolicy_ExecuteAsync_SuccessfulOperation_ReturnsImmediately()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromMilliseconds(10));
        int callCount = 0;

        var result = await policy.ExecuteAsync(_ =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteAsync_失败后重试成功")]
    public async Task RetryPolicy_ExecuteAsync_RetriesAfterFailure()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromMilliseconds(10));
        int callCount = 0;

        var result = await policy.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount < 3)
                throw new InvalidOperationException("transient");
            return Task.FromResult("success");
        });

        Assert.Equal("success", result);
        Assert.Equal(3, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteAsync_超过重试次数仍抛出最后异常")]
    public async Task RetryPolicy_ExecuteAsync_ExceedsMaxRetries_Rethrows()
    {
        var policy = new RetryPolicy(2, TimeSpan.FromMilliseconds(5));
        int callCount = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                callCount++;
                throw new InvalidOperationException($"fail {callCount}");
            })
        );

        Assert.Contains("fail 3", ex.Message); // 1st call + 2 retries = 3 total
        Assert.Equal(3, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteAsync_OperationCanceledException不重试直接抛出")]
    public async Task RetryPolicy_ExecuteAsync_Cancellation_ThrowsImmediately()
    {
        var policy = new RetryPolicy(5, TimeSpan.FromMilliseconds(10));
        int callCount = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                callCount++;
                throw new OperationCanceledException("cancelled");
            })
        );

        Assert.Equal(1, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteAsync_MaxRetries为0不重试")]
    public async Task RetryPolicy_ExecuteAsync_ZeroMaxRetries_NoRetry()
    {
        var policy = RetryPolicy.NoRetry;
        int callCount = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                callCount++;
                throw new InvalidOperationException("fail");
            })
        );

        Assert.Equal(1, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteWithRetryAsync_成功返回true")]
    public async Task RetryPolicy_ExecuteWithRetryAsync_Success_ReturnsTrue()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromMilliseconds(5));
        var result = await policy.ExecuteWithRetryAsync(_ => Task.FromResult(true));
        Assert.True(result);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteWithRetryAsync_最终失败返回false")]
    public async Task RetryPolicy_ExecuteWithRetryAsync_AlwaysFalse_ReturnsFalse()
    {
        var policy = new RetryPolicy(2, TimeSpan.FromMilliseconds(5));
        int callCount = 0;

        var result = await policy.ExecuteWithRetryAsync(_ =>
        {
            callCount++;
            return Task.FromResult(false);
        });

        Assert.False(result);
        Assert.Equal(3, callCount); // 1 + 2 retries
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteWithRetryAsync_重试后成功返回true")]
    public async Task RetryPolicy_ExecuteWithRetryAsync_RetrySuccess_ReturnsTrue()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromMilliseconds(5));
        int callCount = 0;

        var result = await policy.ExecuteWithRetryAsync(_ =>
        {
            callCount++;
            if (callCount < 3)
                throw new Exception("transient");
            return Task.FromResult(true);
        });

        Assert.True(result);
        Assert.Equal(3, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_ExecuteWithRetryAsync_OperationCanceledException不重试")]
    public async Task RetryPolicy_ExecuteWithRetryAsync_Cancellation_Rethrows()
    {
        var policy = new RetryPolicy(5, TimeSpan.FromMilliseconds(5));
        int callCount = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteWithRetryAsync(_ =>
            {
                callCount++;
                throw new OperationCanceledException();
            })
        );

        Assert.Equal(1, callCount);
    }

    [Fact(DisplayName = "RetryPolicy_使用CancellationToken取消时立即停止")]
    public async Task RetryPolicy_WithCancellationToken_CancelsImmediately()
    {
        var policy = new RetryPolicy(10, TimeSpan.FromMilliseconds(100));
        using var cts = new CancellationTokenSource();

        int callCount = 0;
        var task = policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            cts.Cancel();
            throw new InvalidOperationException("fail");
        }, cts.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal(1, callCount);
    }
}
