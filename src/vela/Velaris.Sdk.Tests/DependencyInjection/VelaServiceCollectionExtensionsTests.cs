using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Velaris.Sdk.DependencyInjection;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests.DependencyInjection;

public class VelaServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVela_RegistersConfig()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVela(new VelaConfig { MockMode = true });

        var provider = services.BuildServiceProvider();
        var config = provider.GetService<VelaConfig>();
        Assert.NotNull(config);
        Assert.True(config.MockMode);
    }

    [Fact]
    public void AddVela_RegistersPlatformStrategy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVela(new VelaConfig { PreferredPlatform = VelaPlatform.Linux });

        var provider = services.BuildServiceProvider();
        var strategy = provider.GetService<IPlatformStrategy>();
        Assert.NotNull(strategy);
        Assert.IsType<LinuxStrategy>(strategy);
    }

    [Fact(Skip = "Requires Rust Vela Core native library (vela_ffi.dll/so)")]
    public void AddVela_RegistersVelaCore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVela(new VelaConfig());

        var provider = services.BuildServiceProvider();
        var core = provider.GetService<VelaCore>();
        Assert.NotNull(core);
    }

    [Fact]
    public void AddVela_WithDelegate_Works()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVela(cfg =>
        {
            cfg.HubBaseUrl = "https://custom-hub.example.com";
            cfg.PollIntervalSeconds = 60;
            cfg.PreferredPlatform = VelaPlatform.WindowsIoT;
        });

        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<VelaConfig>();
        Assert.Equal("https://custom-hub.example.com", config.HubBaseUrl);
        Assert.Equal(60, config.PollIntervalSeconds);
    }

    [Fact]
    public void AddVela_DefaultsAreSensible()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVela(new VelaConfig());

        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<VelaConfig>();
        Assert.Contains("vela-ota", config.HubBaseUrl);
        Assert.True(config.WatchdogEnabled);
    }
}
