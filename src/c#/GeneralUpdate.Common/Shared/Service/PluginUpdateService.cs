using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Shared.Service
{
    /// <summary>
    /// Service for managing plugin updates and compatibility.
    /// </summary>
    public static class PluginUpdateService
    {
        /// <summary>
        /// Retrieves available plugin updates from the server.
        /// </summary>
        /// <param name="httpUrl">Plugin validation endpoint.</param>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="pluginId">Optional specific plugin ID to check.</param>
        /// <param name="appKey">Application secret key.</param>
        /// <param name="platform">Platform type.</param>
        /// <param name="productId">Product identifier.</param>
        /// <param name="scheme">Authentication scheme.</param>
        /// <param name="token">Authentication token.</param>
        /// <returns>List of available plugin updates.</returns>
        public static async Task<List<PluginInfo>> GetAvailablePlugins(
            string httpUrl,
            string clientVersion,
            string pluginId = null,
            string appKey = null,
            int platform = 0,
            string productId = null,
            string scheme = null,
            string token = null)
        {
            try
            {
                var response = await VersionService.ValidatePlugins(
                    httpUrl, clientVersion, pluginId, appKey, platform, productId, scheme, token);

                if (response?.Code == 200 && response.Body != null)
                {
                    // Filter only compatible plugins
                    var compatiblePlugins = PluginCompatibilityChecker.FilterCompatiblePlugins(
                        clientVersion, response.Body);
                    return compatiblePlugins;
                }

                return new List<PluginInfo>();
            }
            catch (Exception e)
            {
                GeneralTracer.Error("Failed to retrieve available plugins.", e);
                return new List<PluginInfo>();
            }
        }

        /// <summary>
        /// Gets the minimum supported plugin version for the given client version.
        /// This is automatically called when the client upgrades to match plugin compatibility.
        /// </summary>
        /// <param name="httpUrl">Plugin validation endpoint.</param>
        /// <param name="clientVersion">Target client version after upgrade.</param>
        /// <param name="pluginId">Plugin ID to check.</param>
        /// <param name="appKey">Application secret key.</param>
        /// <param name="platform">Platform type.</param>
        /// <param name="productId">Product identifier.</param>
        /// <param name="scheme">Authentication scheme.</param>
        /// <param name="token">Authentication token.</param>
        /// <returns>Minimum supported plugin version for the client.</returns>
        public static async Task<PluginInfo?> GetMinimumSupportedPluginForClientVersion(
            string httpUrl,
            string clientVersion,
            string pluginId,
            string appKey = null,
            int platform = 0,
            string productId = null,
            string scheme = null,
            string token = null)
        {
            try
            {
                var availablePlugins = await GetAvailablePlugins(
                    httpUrl, clientVersion, pluginId, appKey, platform, productId, scheme, token);

                if (!availablePlugins.Any())
                    return null;

                return PluginCompatibilityChecker.GetMinimumSupportedPlugin(clientVersion, availablePlugins);
            }
            catch (Exception e)
            {
                GeneralTracer.Error($"Failed to get minimum plugin version for client {clientVersion}.", e);
                return null;
            }
        }

        /// <summary>
        /// Checks if a specific plugin version is compatible with the client version.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="plugin">Plugin to validate.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public static bool IsPluginCompatible(string clientVersion, PluginInfo plugin)
        {
            return PluginCompatibilityChecker.IsCompatible(clientVersion, plugin);
        }

        /// <summary>
        /// Determines which plugins need to be upgraded based on current versions.
        /// </summary>
        /// <param name="currentPlugins">Dictionary of currently installed plugin versions (PluginId -> Version).</param>
        /// <param name="availablePlugins">List of available plugin updates.</param>
        /// <returns>List of plugins that should be upgraded.</returns>
        public static List<PluginInfo> DeterminePluginUpdates(
            Dictionary<string, string> currentPlugins,
            List<PluginInfo> availablePlugins)
        {
            var updates = new List<PluginInfo>();

            if (currentPlugins == null || !currentPlugins.Any() || availablePlugins == null || !availablePlugins.Any())
                return updates;

            foreach (var availablePlugin in availablePlugins)
            {
                if (string.IsNullOrEmpty(availablePlugin.PluginId) || string.IsNullOrEmpty(availablePlugin.Version))
                    continue;

                // Check if plugin is installed
                if (currentPlugins.TryGetValue(availablePlugin.PluginId, out var currentVersion))
                {
                    try
                    {
                        var current = new Version(currentVersion);
                        var available = new Version(availablePlugin.Version);

                        // Add to updates if available version is newer
                        if (available > current)
                        {
                            updates.Add(availablePlugin);
                        }
                    }
                    catch (Exception e)
                    {
                        GeneralTracer.Error($"Error comparing plugin versions for {availablePlugin.PluginId}.", e);
                    }
                }
                else
                {
                    // Plugin not installed, add to updates
                    updates.Add(availablePlugin);
                }
            }

            return updates;
        }

        /// <summary>
        /// Filters mandatory plugins from the available updates.
        /// </summary>
        /// <param name="plugins">List of plugins to filter.</param>
        /// <returns>List of mandatory plugins.</returns>
        public static List<PluginInfo> GetMandatoryPlugins(List<PluginInfo> plugins)
        {
            if (plugins == null || !plugins.Any())
                return new List<PluginInfo>();

            return plugins.Where(p => p.IsMandatory == true).ToList();
        }
    }
}
