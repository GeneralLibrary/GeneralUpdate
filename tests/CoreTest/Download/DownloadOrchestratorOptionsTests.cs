using System;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Models;
using Xunit;

namespace CoreTest.Download;

/// <summary>
/// Unit tests for <see cref="DownloadOrchestratorOptions"/> covering:
///   - default values
///   - SanitizeMaxConcurrency clamping
///   - From(UpdateContext) mapping
/// </summary>
public class DownloadOrchestratorOptionsTests
{
    #region Defaults

    [Fact]
    public void Defaults_AreAsSpecified()
    {
        var opts = new DownloadOrchestratorOptions();

        Assert.Equal(2, opts.MaxConcurrency);
        Assert.True(opts.EnableResume);
        Assert.Equal(3, opts.RetryCount);
        Assert.Equal(TimeSpan.FromSeconds(1), opts.RetryInterval);
        Assert.True(opts.VerifyChecksum);
        Assert.Equal(DiffMode.Serial, opts.DiffMode);
        Assert.Equal(TimeSpan.FromSeconds(30), opts.DownloadTimeout);
    }

    #endregion

    #region SanitizeMaxConcurrency

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-100, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(100, 1)]
    public void SanitizeMaxConcurrency_ClampsCorrectly(int input, int expectedMin)
    {
        var result = DownloadOrchestratorOptions.SanitizeMaxConcurrency(input);

        Assert.True(result >= 1, $"Expected result >= 1, got {result}");
        var max = Math.Max(1, Environment.ProcessorCount * 2);
        Assert.True(result <= max, $"Expected result <= {max}, got {result}");

        if (input <= 1)
            Assert.Equal(expectedMin, result);
    }

    [Fact]
    public void SanitizeMaxConcurrency_DoesNotExceedProcessorCountTimes2()
    {
        var max = Math.Max(1, Environment.ProcessorCount * 2);
        var result = DownloadOrchestratorOptions.SanitizeMaxConcurrency(int.MaxValue);
        Assert.Equal(max, result);
    }

    [Fact]
    public void SanitizeMaxConcurrency_NormalValue_Unchanged()
    {
        var normalValue = Math.Min(3, Math.Max(1, Environment.ProcessorCount * 2));
        var result = DownloadOrchestratorOptions.SanitizeMaxConcurrency(normalValue);
        Assert.Equal(normalValue, result);
    }

    #endregion

    #region From(UpdateContext)

    [Fact]
    public void From_CopiesAllFields()
    {
        var config = new UpdateContext
        {
            MaxConcurrency = 5,
            EnableResume = false,
            RetryCount = 7,
            RetryInterval = TimeSpan.FromSeconds(2),
            VerifyChecksum = false,
            DiffMode = DiffMode.Parallel,
            DownloadTimeOut = 60,
        };

        var opts = DownloadOrchestratorOptions.From(config);

        Assert.Equal(5, opts.MaxConcurrency);
        Assert.False(opts.EnableResume);
        Assert.Equal(7, opts.RetryCount);
        Assert.Equal(TimeSpan.FromSeconds(2), opts.RetryInterval);
        Assert.False(opts.VerifyChecksum);
        Assert.Equal(DiffMode.Parallel, opts.DiffMode);
        Assert.Equal(TimeSpan.FromSeconds(60), opts.DownloadTimeout);
    }

    [Fact]
    public void From_DefaultsWhenConfigIsMinimal()
    {
        var config = new UpdateContext();

        var opts = DownloadOrchestratorOptions.From(config);

        Assert.Equal(2, opts.MaxConcurrency);
        Assert.True(opts.EnableResume);
        Assert.Equal(3, opts.RetryCount);
    }

    [Fact]
    public void From_SanitizesMaxConcurrency()
    {
        var config = new UpdateContext { MaxConcurrency = -5 };
        var opts = DownloadOrchestratorOptions.From(config);
        Assert.Equal(1, opts.MaxConcurrency);
    }

    [Fact]
    public void From_ClampsNegativeRetryCount()
    {
        var config = new UpdateContext { RetryCount = -1 };
        var opts = DownloadOrchestratorOptions.From(config);
        Assert.Equal(0, opts.RetryCount);
    }

    [Fact]
    public void From_FallsBackDownloadTimeoutWhenZero()
    {
        var config = new UpdateContext { DownloadTimeOut = 0 };
        var opts = DownloadOrchestratorOptions.From(config);
        Assert.Equal(TimeSpan.FromSeconds(30), opts.DownloadTimeout);
    }

    [Fact]
    public void From_SerialMode()
    {
        var config = new UpdateContext { DiffMode = DiffMode.Serial };
        var opts = DownloadOrchestratorOptions.From(config);
        Assert.Equal(DiffMode.Serial, opts.DiffMode);
    }

    #endregion

    #region All Properties Settable

    [Fact]
    public void AllProperties_AreSettable()
    {
        var opts = new DownloadOrchestratorOptions
        {
            MaxConcurrency = 8,
            EnableResume = false,
            RetryCount = 5,
            RetryInterval = TimeSpan.FromSeconds(3),
            VerifyChecksum = false,
            DiffMode = DiffMode.Parallel,
            DownloadTimeout = TimeSpan.FromSeconds(120),
        };

        Assert.Equal(8, opts.MaxConcurrency);
        Assert.False(opts.EnableResume);
        Assert.Equal(5, opts.RetryCount);
        Assert.Equal(TimeSpan.FromSeconds(3), opts.RetryInterval);
        Assert.False(opts.VerifyChecksum);
        Assert.Equal(DiffMode.Parallel, opts.DiffMode);
        Assert.Equal(TimeSpan.FromSeconds(120), opts.DownloadTimeout);
    }

    #endregion
}
