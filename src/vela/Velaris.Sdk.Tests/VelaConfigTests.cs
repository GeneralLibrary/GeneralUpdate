using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests;

public class VelaConfigTests
{
    [Fact]
    public void Default_HubBaseUrl_ContainsVelaOta()
    {
        var config = new VelaConfig();
        Assert.Contains("vela-ota", config.HubBaseUrl);
    }

    [Fact]
    public void Default_PollInterval_Is300Seconds()
    {
        var config = new VelaConfig();
        Assert.Equal(300, config.PollIntervalSeconds);
    }

    [Fact]
    public void Default_WatchdogEnabled_IsTrue()
    {
        var config = new VelaConfig();
        Assert.True(config.WatchdogEnabled);
    }

    [Fact]
    public void Default_PulseInterval_Is300Seconds()
    {
        var config = new VelaConfig();
        Assert.Equal(300, config.PulseIntervalSeconds);
    }

    [Fact]
    public void Default_MockMode_IsFalse()
    {
        var config = new VelaConfig();
        Assert.False(config.MockMode);
    }

    [Fact]
    public void Default_DownloadDir_ContainsVela()
    {
        var config = new VelaConfig();
        Assert.Contains("vela", config.DownloadDir);
    }

    [Fact]
    public void Default_BlockDevice_IsMmcblk0()
    {
        var config = new VelaConfig();
        Assert.Equal("/dev/mmcblk0", config.BlockDevice);
    }

    [Fact]
    public void CustomConfig_AllPropertiesSet()
    {
        var config = new VelaConfig
        {
            HubBaseUrl = "https://custom.example.com",
            PollIntervalSeconds = 60,
            AuthToken = "token-abc",
            DownloadDir = "/tmp/vela",
            BlockDevice = "/dev/sda",
            IdentityKeyPath = "/etc/vela/key.pem",
            WatchdogEnabled = false,
            PreferredPlatform = VelaPlatform.WindowsIoT,
            PulseIntervalSeconds = 120,
            MockMode = true,
        };

        Assert.Equal("https://custom.example.com", config.HubBaseUrl);
        Assert.Equal(60, config.PollIntervalSeconds);
        Assert.Equal("token-abc", config.AuthToken);
        Assert.Equal("/tmp/vela", config.DownloadDir);
        Assert.Equal("/dev/sda", config.BlockDevice);
        Assert.Equal("/etc/vela/key.pem", config.IdentityKeyPath);
        Assert.False(config.WatchdogEnabled);
        Assert.Equal(VelaPlatform.WindowsIoT, config.PreferredPlatform);
        Assert.Equal(120, config.PulseIntervalSeconds);
        Assert.True(config.MockMode);
    }

    [Fact]
    public void AuthToken_DefaultIsNull()
    {
        var config = new VelaConfig();
        Assert.Null(config.AuthToken);
    }

    [Fact]
    public void IdentityKeyPath_DefaultIsNull()
    {
        var config = new VelaConfig();
        Assert.Null(config.IdentityKeyPath);
    }

    [Fact]
    public void PreferredPlatform_DefaultIsNull()
    {
        var config = new VelaConfig();
        Assert.Null(config.PreferredPlatform);
    }
}
