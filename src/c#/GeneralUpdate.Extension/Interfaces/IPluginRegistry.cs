using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Interfaces
{
    /// <summary>
    /// Manages the registry of installed and available plugins.
    /// Provides methods to query, add, remove, and update plugin information.
    /// </summary>
    public interface IPluginRegistry
    {
        /// <summary>
        /// Gets all locally installed plugins.
        /// </summary>
        /// <returns>List of installed plugin information.</returns>
        Task<List<PluginInfo>> GetLocalPluginsAsync();

        /// <summary>
        /// Gets the list of plugins available on the server/marketplace.
        /// </summary>
        /// <returns>List of available plugin information.</returns>
        Task<List<PluginInfo>> GetServerPluginsAsync();

        /// <summary>
        /// Gets a specific plugin by its ID.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>Plugin information if found, null otherwise.</returns>
        Task<PluginInfo> GetPluginByIdAsync(string pluginId);

        /// <summary>
        /// Checks for updates for all installed plugins.
        /// </summary>
        /// <returns>List of plugins with available updates.</returns>
        Task<List<PluginInfo>> CheckForUpdatesAsync();

        /// <summary>
        /// Checks for updates for a specific plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>Plugin information with update status, or null if not found.</returns>
        Task<PluginInfo> CheckForUpdateAsync(string pluginId);

        /// <summary>
        /// Registers a newly installed plugin in the registry.
        /// </summary>
        /// <param name="plugin">Plugin information to register.</param>
        Task RegisterPluginAsync(PluginInfo plugin);

        /// <summary>
        /// Unregisters a plugin from the registry.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        Task UnregisterPluginAsync(string pluginId);

        /// <summary>
        /// Updates the metadata of an existing plugin.
        /// </summary>
        /// <param name="plugin">Updated plugin information.</param>
        Task UpdatePluginMetadataAsync(PluginInfo plugin);

        /// <summary>
        /// Enables a plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        Task EnablePluginAsync(string pluginId);

        /// <summary>
        /// Disables a plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        Task DisablePluginAsync(string pluginId);

        /// <summary>
        /// Gets plugins filtered by platform compatibility.
        /// </summary>
        /// <param name="platform">Target platform identifier.</param>
        /// <returns>List of compatible plugins.</returns>
        Task<List<PluginInfo>> GetPluginsByPlatformAsync(string platform);
    }
}
