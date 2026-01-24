using System;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Extension.Platform
{
    /// <summary>
    /// Provides platform detection and compatibility checking for plugins.
    /// Aligns with VS Code extension platform identifiers.
    /// </summary>
    public class PlatformResolver
    {
        private static readonly Lazy<PlatformResolver> _instance = new Lazy<PlatformResolver>(() => new PlatformResolver());

        /// <summary>
        /// Gets the singleton instance of PlatformResolver.
        /// </summary>
        public static PlatformResolver Instance => _instance.Value;

        private PlatformResolver()
        {
        }

        /// <summary>
        /// Gets the current platform identifier (e.g., "win32-x64", "darwin-arm64", "linux-x64").
        /// </summary>
        /// <returns>Platform identifier string.</returns>
        public string GetCurrentPlatform()
        {
            var os = GetOperatingSystem();
            var arch = GetArchitecture();
            return $"{os}-{arch}";
        }

        /// <summary>
        /// Gets the current operating system identifier.
        /// </summary>
        /// <returns>Operating system string ("win32", "darwin", or "linux").</returns>
        public string GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win32";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "darwin";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            return "unknown";
        }

        /// <summary>
        /// Gets the current CPU architecture identifier.
        /// </summary>
        /// <returns>Architecture string ("x64", "arm64", "x86", or "arm").</returns>
        public string GetArchitecture()
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            switch (arch)
            {
                case Architecture.X64:
                    return "x64";
                case Architecture.Arm64:
                    return "arm64";
                case Architecture.X86:
                    return "x86";
                case Architecture.Arm:
                    return "arm";
                default:
                    return "unknown";
            }
        }

        /// <summary>
        /// Checks if a plugin is compatible with the current platform.
        /// </summary>
        /// <param name="pluginPlatform">Target platform of the plugin (e.g., "win32-x64", "any").</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsCompatible(string pluginPlatform)
        {
            if (string.IsNullOrWhiteSpace(pluginPlatform))
                return false;

            // "any" or "universal" platform is compatible with all systems
            if (pluginPlatform.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                pluginPlatform.Equals("universal", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var currentPlatform = GetCurrentPlatform();
            return pluginPlatform.Equals(currentPlatform, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a plugin is compatible with the current platform based on separate OS and architecture.
        /// </summary>
        /// <param name="pluginOs">Target operating system of the plugin.</param>
        /// <param name="pluginArch">Target architecture of the plugin.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsCompatible(string pluginOs, string pluginArch)
        {
            if (string.IsNullOrWhiteSpace(pluginOs) || string.IsNullOrWhiteSpace(pluginArch))
                return false;

            // Check for "any" compatibility
            var osCompatible = pluginOs.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                               pluginOs.Equals(GetOperatingSystem(), StringComparison.OrdinalIgnoreCase);
            var archCompatible = pluginArch.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                                 pluginArch.Equals(GetArchitecture(), StringComparison.OrdinalIgnoreCase);

            return osCompatible && archCompatible;
        }

        /// <summary>
        /// Gets a display-friendly name for the current platform.
        /// </summary>
        /// <returns>Display name string.</returns>
        public string GetPlatformDisplayName()
        {
            var os = GetOperatingSystem();
            var arch = GetArchitecture();

            var osName = os switch
            {
                "win32" => "Windows",
                "darwin" => "macOS",
                "linux" => "Linux",
                _ => "Unknown OS"
            };

            var archName = arch switch
            {
                "x64" => "64-bit",
                "x86" => "32-bit",
                "arm64" => "ARM64",
                "arm" => "ARM",
                _ => "Unknown Architecture"
            };

            return $"{osName} {archName}";
        }
    }
}
