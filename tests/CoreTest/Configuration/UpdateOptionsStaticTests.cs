using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="Options"/> — static option key definitions.
/// Validates the default values as defined in the source.
/// </summary>
public class OptionStaticTests
{
    #region Static option key existence & defaults

    [Fact]
    public void AppType_HasCorrectDefault()
    {
        Assert.Equal(GeneralUpdate.Core.Configuration.AppType.Client, Option.AppType.DefaultValue);
    }

    [Fact]
    public void DiffMode_HasCorrectDefault()
    {
        Assert.Equal(DiffMode.Serial, Option.DiffMode.DefaultValue);
    }

    [Fact]
    public void Encoding_HasCorrectDefault()
    {
        Assert.Equal(System.Text.Encoding.UTF8, Option.Encoding.DefaultValue);
    }

    [Fact]
    public void Format_HasCorrectDefault()
    {
        Assert.Equal(Format.Zip, Option.Format.DefaultValue);
    }

    [Fact]
    public void DownloadTimeout_HasCorrectDefault()
    {
        Assert.Equal(30, Option.DownloadTimeout.DefaultValue);
    }

    [Fact]
    public void PatchEnabled_HasCorrectDefault()
    {
        Assert.True(Option.PatchEnabled.DefaultValue);
    }

    [Fact]
    public void BackupEnabled_HasCorrectDefault()
    {
        Assert.True(Option.BackupEnabled.DefaultValue);
    }

    [Fact]
    public void Silent_HasCorrectDefault()
    {
        Assert.False(Option.Silent.DefaultValue);
    }

    [Fact]
    public void SilentPollIntervalMinutes_HasCorrectDefault()
    {
        Assert.Equal(60, Option.SilentPollIntervalMinutes.DefaultValue);
    }

    [Fact]
    public void MaxConcurrency_HasCorrectDefault()
    {
        Assert.Equal(3, Option.MaxConcurrency.DefaultValue);
    }

    [Fact]
    public void EnableResume_HasCorrectDefault()
    {
        Assert.True(Option.EnableResume.DefaultValue);
    }

    [Fact]
    public void RetryCount_HasCorrectDefault()
    {
        Assert.Equal(3, Option.RetryCount.DefaultValue);
    }

    [Fact]
    public void VerifyChecksum_HasCorrectDefault()
    {
        Assert.True(Option.VerifyChecksum.DefaultValue);
    }

    [Fact]
    public void RetryInterval_HasCorrectDefault()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), Option.RetryInterval.DefaultValue);
    }

    #endregion

    #region Singleton identity

    [Fact]
    public void AppType_RepeatedAccess_ReturnsSameInstance()
    {
        var a = Option.AppType;
        var b = Option.AppType;
        Assert.Same(a, b);
    }

    [Fact]
    public void AllOptions_RepeatedAccess_ReturnsSameInstance()
    {
        Assert.Same(Option.DiffMode, Option.DiffMode);
        Assert.Same(Option.Format, Option.Format);
        Assert.Same(Option.MaxConcurrency, Option.MaxConcurrency);
        Assert.Same(Option.RetryCount, Option.RetryCount);
    }

    #endregion
}
