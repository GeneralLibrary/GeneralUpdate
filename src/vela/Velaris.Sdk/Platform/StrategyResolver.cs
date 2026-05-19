using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Velaris.Sdk.Platform;

public static class StrategyResolver
{
    public static IPlatformStrategy Resolve(ILoggerFactory loggerFactory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy(loggerFactory.CreateLogger<LinuxStrategy>());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy(loggerFactory.CreateLogger<WindowsStrategy>());
        throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
    }

    public static IPlatformStrategy Resolve(VelaPlatform platform, ILoggerFactory loggerFactory) => platform switch
    {
        VelaPlatform.Linux => new LinuxStrategy(loggerFactory.CreateLogger<LinuxStrategy>()),
        VelaPlatform.WindowsIoT => new WindowsStrategy(loggerFactory.CreateLogger<WindowsStrategy>()),
        _ => throw new PlatformNotSupportedException($"Platform {platform} not yet implemented."),
    };
}
