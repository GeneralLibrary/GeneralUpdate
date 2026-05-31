using System;
using System.IO;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using Xunit;

namespace CoreTest.Configuration;

/// <summary>
/// Verifies that <see cref="UpdateContext"/> properties are correctly
/// populated from <see cref="Option"/> via
/// <see cref="GeneralUpdateBootstrap.ApplyRuntimeOptions"/>.
/// </summary>
public class UpdateContextWiringTests
{
    #region UpdateContext Default Values

    [Fact]
    public void UpdateContext_MaxConcurrency_DefaultsTo2()
    {
        var config = new UpdateContext();
        Assert.Equal(2, config.MaxConcurrency);
    }

    [Fact]
    public void UpdateContext_EnableResume_DefaultsToTrue()
    {
        var config = new UpdateContext();
        Assert.True(config.EnableResume);
    }

    [Fact]
    public void UpdateContext_RetryCount_DefaultsTo3()
    {
        var config = new UpdateContext();
        Assert.Equal(3, config.RetryCount);
    }

    [Fact]
    public void UpdateContext_RetryInterval_DefaultsToOneSecond()
    {
        var config = new UpdateContext();
        Assert.Equal(TimeSpan.FromSeconds(1), config.RetryInterval);
    }

    [Fact]
    public void UpdateContext_VerifyChecksum_DefaultsToTrue()
    {
        var config = new UpdateContext();
        Assert.True(config.VerifyChecksum);
    }

    [Fact]
    public void UpdateContext_BackupEnabled_DefaultsToNull()
    {
        var config = new UpdateContext();
        Assert.Null(config.BackupEnabled);
    }

    [Fact]
    public void UpdateContext_PatchEnabled_DefaultsToNull()
    {
        var config = new UpdateContext();
        Assert.Null(config.PatchEnabled);
    }

    [Fact]
    public void UpdateContext_DiffMode_DefaultsToSerial()
    {
        var config = new UpdateContext();
        Assert.Equal(DiffMode.Serial, config.DiffMode);
    }

    [Fact]
    public void UpdateContext_AllDownloadProperties_HaveReasonableDefaults()
    {
        var config = new UpdateContext();
        Assert.True(config.MaxConcurrency >= 1);
        Assert.True(config.EnableResume);
        Assert.True(config.RetryCount >= 0);
        Assert.True(config.RetryInterval > TimeSpan.Zero);
        Assert.True(config.VerifyChecksum);
    }

    #endregion

    #region Bootstrap Option → GetOption Roundtrip

    /// <summary>Test subclass that exposes the protected GetOption method.</summary>
    private sealed class TestableBootstrap : GeneralUpdateBootstrap
    {
        public T PublicGetOption<T>(Option<T>? option) => GetOption(option);
    }

    [Fact]
    public void Bootstrap_GetOption_MaxConcurrency_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.MaxConcurrency, 8);
        Assert.Equal(8, b.PublicGetOption(Option.MaxConcurrency));
    }

    [Fact]
    public void Bootstrap_GetOption_EnableResume_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.EnableResume, false);
        Assert.False(b.PublicGetOption(Option.EnableResume));
    }

    [Fact]
    public void Bootstrap_GetOption_RetryCount_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.RetryCount, 10);
        Assert.Equal(10, b.PublicGetOption(Option.RetryCount));
    }

    [Fact]
    public void Bootstrap_GetOption_RetryInterval_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.RetryInterval, TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(5), b.PublicGetOption(Option.RetryInterval));
    }

    [Fact]
    public void Bootstrap_GetOption_VerifyChecksum_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.VerifyChecksum, false);
        Assert.False(b.PublicGetOption(Option.VerifyChecksum));
    }

    [Fact]
    public void Bootstrap_GetOption_BackupEnabled_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.BackupEnabled, false);
        Assert.False(b.PublicGetOption(Option.BackupEnabled));
    }

    [Fact]
    public void Bootstrap_GetOption_PatchEnabled_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.PatchEnabled, true);
        Assert.True(b.PublicGetOption(Option.PatchEnabled));
    }

    [Fact]
    public void Bootstrap_GetOption_DiffMode_ReturnsSetValue()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.DiffMode, DiffMode.Parallel);
        Assert.Equal(DiffMode.Parallel, b.PublicGetOption(Option.DiffMode));
    }

    [Fact]
    public void Bootstrap_AllEightOptions_SetWithoutError()
    {
        var b = new TestableBootstrap();
        b.SetOption(Option.MaxConcurrency, 6)
         .SetOption(Option.EnableResume, false)
         .SetOption(Option.RetryCount, 5)
         .SetOption(Option.RetryInterval, TimeSpan.FromSeconds(3))
         .SetOption(Option.VerifyChecksum, false)
         .SetOption(Option.BackupEnabled, false)
         .SetOption(Option.PatchEnabled, true)
         .SetOption(Option.DiffMode, DiffMode.Parallel);

        // Verify each option was stored correctly
        Assert.Equal(6, b.PublicGetOption(Option.MaxConcurrency));
        Assert.False(b.PublicGetOption(Option.EnableResume));
        Assert.Equal(5, b.PublicGetOption(Option.RetryCount));
        Assert.Equal(TimeSpan.FromSeconds(3), b.PublicGetOption(Option.RetryInterval));
        Assert.False(b.PublicGetOption(Option.VerifyChecksum));
        Assert.False(b.PublicGetOption(Option.BackupEnabled));
        Assert.True(b.PublicGetOption(Option.PatchEnabled));
        Assert.Equal(DiffMode.Parallel, b.PublicGetOption(Option.DiffMode));
    }

    #endregion

    #region Option Defaults Match Option

    [Fact]
    public void Options_MaxConcurrency_DefaultIs3()
        => Assert.Equal(3, Option.MaxConcurrency.DefaultValue);

    [Fact]
    public void Options_EnableResume_DefaultIsTrue()
        => Assert.True(Option.EnableResume.DefaultValue);

    [Fact]
    public void Options_RetryCount_DefaultIs3()
        => Assert.Equal(3, Option.RetryCount.DefaultValue);

    [Fact]
    public void Options_RetryInterval_DefaultIsOneSecond()
        => Assert.Equal(TimeSpan.FromSeconds(1), Option.RetryInterval.DefaultValue);

    [Fact]
    public void Options_VerifyChecksum_DefaultIsTrue()
        => Assert.True(Option.VerifyChecksum.DefaultValue);

    [Fact]
    public void Options_BackupEnabled_DefaultIsTrue()
        => Assert.True(Option.BackupEnabled.DefaultValue);

    [Fact]
    public void Options_PatchEnabled_DefaultIsTrue()
        => Assert.True(Option.PatchEnabled.DefaultValue);

    [Fact]
    public void Options_DiffMode_DefaultIsSerial()
        => Assert.Equal(DiffMode.Serial, Option.DiffMode.DefaultValue);

    #endregion

    #region Helpers

    private static GeneralUpdateBootstrap MakeBootstrap()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"GU_Wiring_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        return new GeneralUpdateBootstrap().SetConfig(new UpdateRequest
        {
            UpdateUrl = "https://api.example.com",
            MainAppName = "MyApp.exe",
            ClientVersion = "1.0.0",
            InstallPath = testDir,
            AppSecretKey = "secret",
            Scheme = "https",
            Token = "token",
        });
    }

    #endregion
}
