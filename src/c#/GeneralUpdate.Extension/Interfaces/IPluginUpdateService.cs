using System;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Interfaces
{
    /// <summary>
    /// Provides plugin update orchestration, including manual updates, auto-update control,
    /// and coordination with download and installation components.
    /// </summary>
    public interface IPluginUpdateService
    {
        /// <summary>
        /// Gets or sets whether auto-update is enabled globally.
        /// </summary>
        bool GlobalAutoUpdateEnabled { get; set; }

        /// <summary>
        /// Manually triggers an update for a specific plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>True if update was queued successfully, false otherwise.</returns>
        Task<bool> UpdatePluginAsync(string pluginId);

        /// <summary>
        /// Triggers updates for all plugins that have updates available.
        /// </summary>
        /// <returns>Number of plugins queued for update.</returns>
        Task<int> UpdateAllPluginsAsync();

        /// <summary>
        /// Cancels an ongoing update for a specific plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        Task CancelUpdateAsync(string pluginId);

        /// <summary>
        /// Sets auto-update preference for a specific plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <param name="enabled">True to enable auto-update, false to disable.</param>
        Task SetPluginAutoUpdateAsync(string pluginId, bool enabled);

        /// <summary>
        /// Gets the current update status for a specific plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>Current update status.</returns>
        Task<UpdateStatus> GetUpdateStatusAsync(string pluginId);

        /// <summary>
        /// Checks for updates for all installed plugins and optionally auto-updates them.
        /// </summary>
        /// <param name="autoUpdate">Whether to automatically start updates for plugins with auto-update enabled.</param>
        /// <returns>Number of updates available.</returns>
        Task<int> CheckAndUpdateAsync(bool autoUpdate = false);

        /// <summary>
        /// Rolls back a plugin to its previous version if a backup is available.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>True if rollback was successful, false otherwise.</returns>
        Task<bool> RollbackPluginAsync(string pluginId);

        /// <summary>
        /// Event triggered when any plugin update status changes.
        /// </summary>
        event EventHandler<PluginUpdateEvent> PluginUpdateStatusChanged;
    }
}
