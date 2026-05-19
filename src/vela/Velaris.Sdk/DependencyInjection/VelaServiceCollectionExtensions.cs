using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.DependencyInjection;

/// <summary>
/// DI registration extensions for Velaris SDK.
/// All registrations are explicit (no reflection scanning) for Native AOT compatibility.
/// </summary>
public static class VelaServiceCollectionExtensions
{
    /// <summary>
    /// Register all Velaris SDK services with the DI container.
    /// </summary>
    public static IServiceCollection AddVela(this IServiceCollection services, VelaConfig config)
    {
        // 1. Platform strategy — compile-time factory, no reflection
        services.AddSingleton<IPlatformStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IPlatformStrategy>>();
            return StrategyResolver.Resolve(config.PreferredPlatform ?? VelaPlatform.Linux,
                sp.GetRequiredService<ILoggerFactory>());
        });

        // 2. Core engine wrapper — singleton
        services.AddSingleton<VelaCore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VelaCore>>();
            return new VelaCore(logger);
        });

        // 3. VelaConfig — singleton
        services.AddSingleton(config);

        return services;
    }

    /// <summary>
    /// Register Velaris SDK with configuration from an options delegate.
    /// </summary>
    public static IServiceCollection AddVela(this IServiceCollection services, Action<VelaConfig> configure)
    {
        var config = new VelaConfig();
        configure(config);
        return services.AddVela(config);
    }
}
