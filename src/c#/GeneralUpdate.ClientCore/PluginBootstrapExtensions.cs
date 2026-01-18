using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.ClientCore
{
    /// <summary>
    /// Extension methods for GeneralClientBootstrap to support plugin upgrades.
    /// </summary>
    public static class PluginBootstrapExtensions
    {
        /// <summary>
        /// Validates and retrieves available plugin updates for the current client version.
        /// </summary>
        /// <param name="configInfo">Global configuration information.</param>
        /// <param name="platform">Platform type.</param>
        /// <returns>List of available plugin updates compatible with the current client version.</returns>
        public static async Task<List<PluginInfo>> GetAvailablePluginUpdatesAsync(
            GlobalConfigInfo configInfo,
            int platform)
        {
            if (configInfo == null || string.IsNullOrEmpty(configInfo.PluginUpdateUrl))
            {
                return new List<PluginInfo>();
            }

            try
            {
                var availablePlugins = await PluginUpdateService.GetAvailablePlugins(
                    configInfo.PluginUpdateUrl,
                    configInfo.ClientVersion,
                    null, // Check all plugins
                    configInfo.AppSecretKey,
                    platform,
                    configInfo.ProductId,
                    configInfo.Scheme,
                    configInfo.Token);

                return availablePlugins;
            }
            catch (Exception)
            {
                return new List<PluginInfo>();
            }
        }

        /// <summary>
        /// Automatically determines minimum required plugin versions for a new client version.
        /// This is called when the client is being upgraded to ensure plugin compatibility.
        /// </summary>
        /// <param name="configInfo">Global configuration information.</param>
        /// <param name="targetClientVersion">Target client version after upgrade.</param>
        /// <param name="platform">Platform type.</param>
        /// <param name="currentPlugins">Dictionary of currently installed plugins (PluginId -> Version).</param>
        /// <returns>List of plugins that need to be upgraded.</returns>
        public static async Task<List<PluginInfo>> GetRequiredPluginUpdatesForClientUpgradeAsync(
            GlobalConfigInfo configInfo,
            string targetClientVersion,
            int platform,
            Dictionary<string, string> currentPlugins)
        {
            if (configInfo == null || string.IsNullOrEmpty(configInfo.PluginUpdateUrl))
            {
                return new List<PluginInfo>();
            }

            try
            {
                var requiredPlugins = new List<PluginInfo>();

                foreach (var currentPlugin in currentPlugins)
                {
                    var minPlugin = await PluginUpdateService.GetMinimumSupportedPluginForClientVersion(
                        configInfo.PluginUpdateUrl,
                        targetClientVersion,
                        currentPlugin.Key,
                        configInfo.AppSecretKey,
                        platform,
                        configInfo.ProductId,
                        configInfo.Scheme,
                        configInfo.Token);

                    if (minPlugin != null)
                    {
                        try
                        {
                            var currentVersion = new Version(currentPlugin.Value);
                            var minVersion = new Version(minPlugin.Version);

                            // Only add if current version is below minimum required
                            if (currentVersion < minVersion)
                            {
                                requiredPlugins.Add(minPlugin);
                            }
                        }
                        catch (Exception)
                        {
                            // Skip plugins with version comparison errors
                        }
                    }
                }

                return requiredPlugins;
            }
            catch (Exception)
            {
                return new List<PluginInfo>();
            }
        }

        /// <summary>
        /// Filters plugin updates based on user selection.
        /// Allows users to choose which plugins to upgrade, similar to VS Code.
        /// </summary>
        /// <param name="availablePlugins">List of available plugin updates.</param>
        /// <param name="selectedPluginIds">List of plugin IDs selected by user for upgrade.</param>
        /// <returns>List of plugins to upgrade based on user selection.</returns>
        public static List<PluginInfo> SelectPluginsForUpgrade(
            List<PluginInfo> availablePlugins,
            List<string> selectedPluginIds)
        {
            if (availablePlugins == null || !availablePlugins.Any())
                return new List<PluginInfo>();

            if (selectedPluginIds == null || !selectedPluginIds.Any())
                return new List<PluginInfo>();

            return availablePlugins
                .Where(p => !string.IsNullOrEmpty(p.PluginId) && selectedPluginIds.Contains(p.PluginId))
                .ToList();
        }

        /// <summary>
        /// Determines mandatory plugin updates that must be installed.
        /// </summary>
        /// <param name="availablePlugins">List of available plugin updates.</param>
        /// <returns>List of mandatory plugins.</returns>
        public static List<PluginInfo> GetMandatoryPluginUpdates(List<PluginInfo> availablePlugins)
        {
            return PluginUpdateService.GetMandatoryPlugins(availablePlugins);
        }

        /// <summary>
        /// Checks if any plugin updates are incompatible with the current client version.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="plugins">List of plugins to check.</param>
        /// <returns>True if all plugins are compatible, false otherwise.</returns>
        public static bool AreAllPluginsCompatible(string clientVersion, List<PluginInfo> plugins)
        {
            if (plugins == null || !plugins.Any())
                return true;

            return plugins.All(p => PluginUpdateService.IsPluginCompatible(clientVersion, p));
        }

        /// <summary>
        /// Validates that selected plugins are compatible with a target client version.
        /// Used before performing client upgrade to prevent compatibility issues.
        /// </summary>
        /// <param name="targetClientVersion">Target client version after upgrade.</param>
        /// <param name="selectedPlugins">Plugins selected for installation.</param>
        /// <returns>True if all selected plugins are compatible with target version.</returns>
        public static bool ValidatePluginCompatibilityForUpgrade(
            string targetClientVersion,
            List<PluginInfo> selectedPlugins)
        {
            if (string.IsNullOrEmpty(targetClientVersion))
                return false;

            if (selectedPlugins == null || !selectedPlugins.Any())
                return true;

            foreach (var plugin in selectedPlugins)
            {
                if (!PluginUpdateService.IsPluginCompatible(targetClientVersion, plugin))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
