using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="UpdateOptions"/> — static option key definitions.
/// Validates the default values as defined in the source.
/// </summary>
public class UpdateOptionsStaticTests
{
    #region Static option key existence & defaults

    [Fact]
    public void AppType_HasCorrectDefault()
    {
        Assert.Equal(GeneralUpdate.Core.Configuration.AppType.Client, UpdateOptions.AppType.DefaultValue);
    }

    [Fact]
    public void DiffMode_HasCorrectDefault()
    {
        Assert.Equal(DiffMode.Serial, UpdateOptions.DiffMode.DefaultValue);
    }

    [Fact]
    public void Encoding_HasCorrectDefault()
    {
        Assert.Equal(System.Text.Encoding.UTF8, UpdateOptions.Encoding.DefaultValue);
    }

    [Fact]
    public void Format_HasCorrectDefault()
    {
        Assert.Equal(Format.Zip, UpdateOptions.Format.DefaultValue);
    }

    [Fact]
    public void DownloadTimeout_HasCorrectDefault()
    {
        Assert.Equal(30, UpdateOptions.DownloadTimeout.DefaultValue);
    }

    [Fact]
    public void PatchEnabled_HasCorrectDefault()
    {
        Assert.True(UpdateOptions.PatchEnabled.DefaultValue);
    }

    [Fact]
    public void BackupEnabled_HasCorrectDefault()
    {
        Assert.True(UpdateOptions.BackupEnabled.DefaultValue);
    }

    [Fact]
    public void Silent_HasCorrectDefault()
    {
        Assert.False(UpdateOptions.Silent.DefaultValue);
    }

    [Fact]
    public void SilentPollIntervalMinutes_HasCorrectDefault()
    {
        Assert.Equal(60, UpdateOptions.SilentPollIntervalMinutes.DefaultValue);
    }

    [Fact]
    public void MaxConcurrency_HasCorrectDefault()
    {
        Assert.Equal(3, UpdateOptions.MaxConcurrency.DefaultValue);
    }

    [Fact]
    public void EnableResume_HasCorrectDefault()
    {
        Assert.True(UpdateOptions.EnableResume.DefaultValue);
    }

    [Fact]
    public void RetryCount_HasCorrectDefault()
    {
        Assert.Equal(3, UpdateOptions.RetryCount.DefaultValue);
    }

    [Fact]
    public void VerifyChecksum_HasCorrectDefault()
    {
        Assert.True(UpdateOptions.VerifyChecksum.DefaultValue);
    }

    [Fact]
    public void RetryInterval_HasCorrectDefault()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), UpdateOptions.RetryInterval.DefaultValue);
    }

    #endregion

    #region Singleton identity

    [Fact]
    public void AppType_RepeatedAccess_ReturnsSameInstance()
    {
        var a = UpdateOptions.AppType;
        var b = UpdateOptions.AppType;
        Assert.Same(a, b);
    }

    [Fact]
    public void AllOptions_RepeatedAccess_ReturnsSameInstance()
    {
        Assert.Same(UpdateOptions.DiffMode, UpdateOptions.DiffMode);
        Assert.Same(UpdateOptions.Format, UpdateOptions.Format);
        Assert.Same(UpdateOptions.MaxConcurrency, UpdateOptions.MaxConcurrency);
        Assert.Same(UpdateOptions.RetryCount, UpdateOptions.RetryCount);
    }

    #endregion
}
