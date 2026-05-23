using System;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Creates the correct <see cref="IBowlStrategy"/> for the current operating system.
/// Replaces the inline platform detection in the old static Bowl class.
/// </summary>
internal static class StrategyFactory
{
    /// <summary>
    /// Creates a platform-appropriate strategy.
    /// Throws <see cref="PlatformNotSupportedException"/> if no strategy is available.
    /// </summary>
    public static IBowlStrategy Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsBowlStrategy();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxBowlStrategy();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacBowlStrategy();

        throw new PlatformNotSupportedException(
            $"Bowl does not support the current platform: {RuntimeInformation.OSDescription}");
    }
}
