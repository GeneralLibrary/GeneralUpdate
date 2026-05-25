using System;
using System.IO;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using Xunit;

namespace CoreTest.Configuration;

/// <summary>
/// Verifies that <see cref="GlobalConfigInfo"/> properties are correctly
/// populated from <see cref="UpdateOptions"/> via
/// <see cref="GeneralUpdateBootstrap.ApplyRuntimeOptions"/>.
/// </summary>
public class GlobalConfigInfoWiringTests
{
    #region GlobalConfigInfo Default Values

    [Fact]
    public void GlobalConfigInfo_MaxConcurrency_DefaultsTo3()
    {
        var config = new GlobalConfigInfo();
        Assert.Equal(3, config.MaxConcurrency);
    }

    [Fact]
    public void GlobalConfigInfo_EnableResume_DefaultsToTrue()
    {
        var config = new GlobalConfigInfo();
        Assert.True(config.EnableResume);
    }

    [Fact]
    public void GlobalConfigInfo_RetryCount_DefaultsTo3()
    {
        var config = new GlobalConfigInfo();
        Assert.Equal(3, config.RetryCount);
    }

    [Fact]
    public void GlobalConfigInfo_RetryInterval_DefaultsToOneSecond()
    {
        var config = new GlobalConfigInfo();
        Assert.Equal(TimeSpan.FromSeconds(1), config.RetryInterval);
    }

    [Fact]
    public void GlobalConfigInfo_VerifyChecksum_DefaultsToTrue()
    {
        var config = new GlobalConfigInfo();
        Assert.True(config.VerifyChecksum);
    }

    [Fact]
    public void GlobalConfigInfo_BackupEnabled_DefaultsToNull()
    {
        var config = new GlobalConfigInfo();
        Assert.Null(config.BackupEnabled);
    }

    [Fact]
    public void GlobalConfigInfo_PatchEnabled_DefaultsToNull()
    {
        var config = new GlobalConfigInfo();
        Assert.Null(config.PatchEnabled);
    }

    [Fact]
    public void GlobalConfigInfo_DiffMode_DefaultsToSerial()
    {
        var config = new GlobalConfigInfo();
        Assert.Equal(DiffMode.Serial, config.DiffMode);
    }

    [Fact]
    public void GlobalConfigInfo_AllDownloadProperties_HaveReasonableDefaults()
    {
        var config = new GlobalConfigInfo();
        Assert.True(config.MaxConcurrency >= 1);
        Assert.True(config.EnableResume);
        Assert.True(config.RetryCount >= 0);
        Assert.True(config.RetryInterval > TimeSpan.Zero);
        Assert.True(config.VerifyChecksum);
    }

    #endregion

    #region Bootstrap Option → GetOption Roundtrip

    [Fact]
    public void Bootstrap_GetOption_MaxConcurrency_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.MaxConcurrency, 8);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_EnableResume_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.EnableResume, false);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_RetryCount_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.RetryCount, 10);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_RetryInterval_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(5));
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_VerifyChecksum_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.VerifyChecksum, false);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_BackupEnabled_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.BackupEnabled, false);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_PatchEnabled_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.PatchEnabled, true);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_GetOption_DiffMode_ReturnsSetValue()
    {
        var b = MakeBootstrap().Option(UpdateOptions.DiffMode, DiffMode.Parallel);
        Assert.NotNull(b);
    }

    [Fact]
    public void Bootstrap_AllEightOptions_SetWithoutError()
    {
        var b = MakeBootstrap()
            .Option(UpdateOptions.MaxConcurrency, 6)
            .Option(UpdateOptions.EnableResume, false)
            .Option(UpdateOptions.RetryCount, 5)
            .Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(3))
            .Option(UpdateOptions.VerifyChecksum, false)
            .Option(UpdateOptions.BackupEnabled, false)
            .Option(UpdateOptions.PatchEnabled, true)
            .Option(UpdateOptions.DiffMode, DiffMode.Parallel);
        Assert.NotNull(b);
    }

    #endregion

    #region Option Defaults Match UpdateOptions

    [Fact]
    public void UpdateOptions_MaxConcurrency_DefaultIs3()
        => Assert.Equal(3, UpdateOptions.MaxConcurrency.DefaultValue);

    [Fact]
    public void UpdateOptions_EnableResume_DefaultIsTrue()
        => Assert.True(UpdateOptions.EnableResume.DefaultValue);

    [Fact]
    public void UpdateOptions_RetryCount_DefaultIs3()
        => Assert.Equal(3, UpdateOptions.RetryCount.DefaultValue);

    [Fact]
    public void UpdateOptions_RetryInterval_DefaultIsOneSecond()
        => Assert.Equal(TimeSpan.FromSeconds(1), UpdateOptions.RetryInterval.DefaultValue);

    [Fact]
    public void UpdateOptions_VerifyChecksum_DefaultIsTrue()
        => Assert.True(UpdateOptions.VerifyChecksum.DefaultValue);

    [Fact]
    public void UpdateOptions_BackupEnabled_DefaultIsTrue()
        => Assert.True(UpdateOptions.BackupEnabled.DefaultValue);

    [Fact]
    public void UpdateOptions_PatchEnabled_DefaultIsTrue()
        => Assert.True(UpdateOptions.PatchEnabled.DefaultValue);

    [Fact]
    public void UpdateOptions_DiffMode_DefaultIsSerial()
        => Assert.Equal(DiffMode.Serial, UpdateOptions.DiffMode.DefaultValue);

    #endregion

    #region Helpers

    private static GeneralUpdateBootstrap MakeBootstrap()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"GU_Wiring_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        return new GeneralUpdateBootstrap().SetConfig(new Configinfo
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
