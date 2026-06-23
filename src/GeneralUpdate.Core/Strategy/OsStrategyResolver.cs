using System;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Shared OS platform strategy resolver. Eliminates the duplicate
/// <c>ResolveOsStrategy()</c> method in <see cref="ClientStrategy"/>
/// and <see cref="UpdateStrategy"/>.
/// </summary>
internal static class OsStrategyResolver
{
    /// <summary>
    /// Resolves the platform-specific strategy for the current OS.
    /// </summary>
    /// <param name="customOsStrategy">
    /// An optional custom OS strategy. When non-null, returned as-is.
    /// </param>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current OS is not Windows, Linux, or macOS and no custom strategy was provided.
    /// </exception>
    internal static IStrategy Resolve(IStrategy? customOsStrategy = null)
    {
        if (customOsStrategy != null)
            return customOsStrategy;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacStrategy();

        throw new PlatformNotSupportedException("The current operating system is not supported!");
    }

    /// <summary>
    /// Resolves the platform type for the current OS.
    /// </summary>
    internal static Configuration.PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Configuration.PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Configuration.PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Configuration.PlatformType.MacOS;
        return Configuration.PlatformType.Unknown;
    }
}
