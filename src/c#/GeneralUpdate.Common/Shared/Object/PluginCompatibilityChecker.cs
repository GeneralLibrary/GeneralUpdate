using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Provides plugin compatibility validation between client and plugin versions.
    /// </summary>
    public static class PluginCompatibilityChecker
    {
        /// <summary>
        /// Checks if a plugin is compatible with the current client version.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="plugin">Plugin information to validate.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public static bool IsCompatible(string clientVersion, PluginInfo plugin)
        {
            if (string.IsNullOrEmpty(clientVersion) || plugin == null)
                return false;

            if (string.IsNullOrEmpty(plugin.MinClientVersion) && string.IsNullOrEmpty(plugin.MaxClientVersion))
                return true;

            try
            {
                var currentVersion = new Version(clientVersion);

                if (!string.IsNullOrEmpty(plugin.MinClientVersion))
                {
                    var minVersion = new Version(plugin.MinClientVersion);
                    if (currentVersion < minVersion)
                        return false;
                }

                if (!string.IsNullOrEmpty(plugin.MaxClientVersion))
                {
                    var maxVersion = new Version(plugin.MaxClientVersion);
                    if (currentVersion > maxVersion)
                        return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Filters a list of plugins to only include compatible ones for the given client version.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="plugins">List of plugins to filter.</param>
        /// <returns>List of compatible plugins.</returns>
        public static List<PluginInfo> FilterCompatiblePlugins(string clientVersion, List<PluginInfo> plugins)
        {
            if (plugins == null || !plugins.Any())
                return new List<PluginInfo>();

            return plugins.Where(p => IsCompatible(clientVersion, p)).ToList();
        }

        /// <summary>
        /// Gets the minimum supported plugin version for a specific client version range.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="plugins">List of available plugin versions.</param>
        /// <returns>The minimum compatible plugin version, or null if none found.</returns>
        public static PluginInfo? GetMinimumSupportedPlugin(string clientVersion, List<PluginInfo> plugins)
        {
            var compatiblePlugins = FilterCompatiblePlugins(clientVersion, plugins);
            if (!compatiblePlugins.Any())
                return null;

            try
            {
                return compatiblePlugins
                    .Where(p => !string.IsNullOrEmpty(p.Version))
                    .OrderBy(p => new Version(p.Version))
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                // If any version is invalid, return null
                return null;
            }
        }

        /// <summary>
        /// Gets the latest compatible plugin version for the given client version.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="plugins">List of available plugin versions.</param>
        /// <returns>The latest compatible plugin version, or null if none found.</returns>
        public static PluginInfo? GetLatestCompatiblePlugin(string clientVersion, List<PluginInfo> plugins)
        {
            var compatiblePlugins = FilterCompatiblePlugins(clientVersion, plugins);
            if (!compatiblePlugins.Any())
                return null;

            try
            {
                return compatiblePlugins
                    .Where(p => !string.IsNullOrEmpty(p.Version))
                    .OrderByDescending(p => new Version(p.Version))
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                // If any version is invalid, return null
                return null;
            }
        }
    }
}
