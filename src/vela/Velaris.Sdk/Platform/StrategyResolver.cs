using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Velaris.Sdk.Platform;

/// <summary>
/// Resolves the correct platform strategy at runtime based on OS detection.
/// </summary>
public static class StrategyResolver
{
    /// <summary>
    /// Resolve the platform strategy for the current runtime.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating strategy instances.</param>
    /// <returns>The platform-appropriate strategy.</returns>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current platform has no strategy and no fallback is available.
    /// </exception>
    public static IPlatformStrategy Resolve(ILoggerFactory loggerFactory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxStrategy(loggerFactory.CreateLogger<LinuxStrategy>());
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsStrategy(loggerFactory.CreateLogger<WindowsStrategy>());
        }

        throw new PlatformNotSupportedException(
            $"Vela OTA does not support the current platform: {RuntimeInformation.OSDescription}. " +
            "Supported platforms: Linux (A/B slots), Windows IoT (P2).");
    }

    /// <summary>
    /// Resolve a strategy for a specific platform (bypass OS detection).
    /// Useful for testing and cross-compilation scenarios.
    /// </summary>
    public static IPlatformStrategy Resolve(VelaPlatform platform, ILoggerFactory loggerFactory)
    {
        return platform switch
        {
            VelaPlatform.Linux => new LinuxStrategy(loggerFactory.CreateLogger<LinuxStrategy>()),
            VelaPlatform.WindowsIoT => new WindowsStrategy(loggerFactory.CreateLogger<WindowsStrategy>()),
            VelaPlatform.Android => throw new PlatformNotSupportedException("Android strategy not yet implemented."),
            VelaPlatform.FreeRTOS => throw new PlatformNotSupportedException("FreeRTOS strategy not yet implemented."),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
        };
    }
}
