using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="HubConfig"/> — default values and property mutation.
/// Covers: default construction, property get/set, boundary time spans, negative reconnects.
/// </summary>
public class HubConfigTests
{
    [Fact]
    public void Ctor_Default_UrlIsEmpty()
    {
        var config = new HubConfig();
        Assert.Equal(string.Empty, config.Url);
    }

    [Fact]
    public void Ctor_Default_ReconnectDelayIs5Seconds()
    {
        var config = new HubConfig();
        Assert.Equal(TimeSpan.FromSeconds(5), config.ReconnectDelay);
    }

    [Fact]
    public void Ctor_Default_MaxReconnectAttemptsIs10()
    {
        var config = new HubConfig();
        Assert.Equal(10, config.MaxReconnectAttempts);
    }

    [Fact]
    public void Url_SetAndGet_Works()
    {
        var config = new HubConfig { Url = "https://hub.example.com/update" };
        Assert.Equal("https://hub.example.com/update", config.Url);
    }

    [Fact]
    public void ReconnectDelay_SetToZero_Works()
    {
        var config = new HubConfig { ReconnectDelay = TimeSpan.Zero };
        Assert.Equal(TimeSpan.Zero, config.ReconnectDelay);
    }

    [Fact]
    public void ReconnectDelay_SetToMaxValue_Works()
    {
        var config = new HubConfig { ReconnectDelay = TimeSpan.MaxValue };
        Assert.Equal(TimeSpan.MaxValue, config.ReconnectDelay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(-1)]
    public void MaxReconnectAttempts_SetVarious_Works(int attempts)
    {
        var config = new HubConfig { MaxReconnectAttempts = attempts };
        Assert.Equal(attempts, config.MaxReconnectAttempts);
    }

    [Fact]
    public void MaxReconnectAttempts_IntMinValue_Works()
    {
        var config = new HubConfig { MaxReconnectAttempts = int.MinValue };
        Assert.Equal(int.MinValue, config.MaxReconnectAttempts);
    }
}
