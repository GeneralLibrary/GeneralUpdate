using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Interfaces;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Orchestrates plugin updates, including manual updates, auto-update control,
    /// and coordination with download and installation components.
    /// </summary>
    public class PluginUpdateService : IPluginUpdateService
    {
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IDownloadQueue _downloadQueue;
        private readonly IPluginInstaller _pluginInstaller;
        private readonly IUpdateEventBus _eventBus;
        private readonly Dictionary<string, UpdateStatus> _updateStatusMap = new Dictionary<string, UpdateStatus>();
        private readonly object _lock = new object();

        /// <summary>
        /// Gets or sets whether auto-update is enabled globally.
        /// </summary>
        public bool GlobalAutoUpdateEnabled { get; set; }

        /// <summary>
        /// Event triggered when any plugin update status changes.
        /// </summary>
        public event EventHandler<PluginUpdateEvent> PluginUpdateStatusChanged;

        /// <summary>
        /// Initializes a new instance of PluginUpdateService.
        /// </summary>
        public PluginUpdateService(
            IPluginRegistry pluginRegistry,
            IDownloadQueue downloadQueue,
            IPluginInstaller pluginInstaller,
            IUpdateEventBus eventBus)
        {
            _pluginRegistry = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));
            _downloadQueue = downloadQueue ?? throw new ArgumentNullException(nameof(downloadQueue));
            _pluginInstaller = pluginInstaller ?? throw new ArgumentNullException(nameof(pluginInstaller));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            // Wire up download queue events
            _downloadQueue.PluginQueued += OnDownloadQueueEvent;
            _downloadQueue.PluginUpdating += OnDownloadQueueEvent;
            _downloadQueue.PluginUpdateSucceeded += OnPluginDownloadSucceeded;
            _downloadQueue.PluginUpdateFailed += OnDownloadQueueEvent;
            _downloadQueue.PluginUpdateProgress += OnDownloadQueueEvent;

            // Subscribe to event bus
            _eventBus.Subscribe(OnPluginUpdateEvent);
        }

        /// <summary>
        /// Manually triggers an update for a specific plugin.
        /// </summary>
        public async Task<bool> UpdatePluginAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                // Get plugin info
                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                if (plugin == null)
                {
                    Console.WriteLine($"Plugin {pluginId} not found");
                    return false;
                }

                // Check for updates if not already known
                if (!plugin.UpdateAvailable)
                {
                    plugin = await _pluginRegistry.CheckForUpdateAsync(pluginId);
                    if (plugin == null || !plugin.UpdateAvailable)
                    {
                        Console.WriteLine($"No update available for plugin {pluginId}");
                        return false;
                    }
                }

                // Enqueue for download
                return await _downloadQueue.EnqueueAsync(plugin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Triggers updates for all plugins that have updates available.
        /// </summary>
        public async Task<int> UpdateAllPluginsAsync()
        {
            try
            {
                var pluginsWithUpdates = await _pluginRegistry.CheckForUpdatesAsync();
                int queuedCount = 0;

                foreach (var plugin in pluginsWithUpdates)
                {
                    if (await _downloadQueue.EnqueueAsync(plugin))
                    {
                        queuedCount++;
                    }
                }

                return queuedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating all plugins: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Cancels an ongoing update for a specific plugin.
        /// </summary>
        public async Task CancelUpdateAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                await _downloadQueue.DequeueAsync(pluginId);

                lock (_lock)
                {
                    _updateStatusMap[pluginId] = UpdateStatus.Cancelled;
                }

                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                var cancelEvent = new PluginUpdateEvent
                {
                    Plugin = plugin,
                    Status = UpdateStatus.Cancelled,
                    Message = $"Update cancelled for plugin {pluginId}"
                };

                _eventBus.Publish(cancelEvent);
                PluginUpdateStatusChanged?.Invoke(this, cancelEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelling update for plugin {pluginId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets auto-update preference for a specific plugin.
        /// </summary>
        public async Task SetPluginAutoUpdateAsync(string pluginId, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                if (plugin != null)
                {
                    plugin.AutoUpdateEnabled = enabled;
                    await _pluginRegistry.UpdatePluginMetadataAsync(plugin);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting auto-update for plugin {pluginId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current update status for a specific plugin.
        /// </summary>
        public Task<UpdateStatus> GetUpdateStatusAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return Task.FromResult(UpdateStatus.Idle);

            lock (_lock)
            {
                if (_updateStatusMap.TryGetValue(pluginId, out var status))
                {
                    return Task.FromResult(status);
                }
            }

            return Task.FromResult(_downloadQueue.GetStatus(pluginId));
        }

        /// <summary>
        /// Checks for updates for all installed plugins and optionally auto-updates them.
        /// </summary>
        public async Task<int> CheckAndUpdateAsync(bool autoUpdate = false)
        {
            try
            {
                var pluginsWithUpdates = await _pluginRegistry.CheckForUpdatesAsync();

                if (!autoUpdate)
                {
                    return pluginsWithUpdates.Count;
                }

                int queuedCount = 0;

                foreach (var plugin in pluginsWithUpdates)
                {
                    // Check if auto-update is enabled for this plugin or globally
                    if (plugin.AutoUpdateEnabled || GlobalAutoUpdateEnabled)
                    {
                        if (await _downloadQueue.EnqueueAsync(plugin))
                        {
                            queuedCount++;
                        }
                    }
                }

                return queuedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckAndUpdate: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Rolls back a plugin to its previous version if a backup is available.
        /// </summary>
        public async Task<bool> RollbackPluginAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                lock (_lock)
                {
                    _updateStatusMap[pluginId] = UpdateStatus.RollingBack;
                }

                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                
                var rollbackEvent = new PluginUpdateEvent
                {
                    Plugin = plugin,
                    Status = UpdateStatus.RollingBack,
                    Message = $"Rolling back plugin {pluginId}"
                };
                _eventBus.Publish(rollbackEvent);
                PluginUpdateStatusChanged?.Invoke(this, rollbackEvent);

                var success = await _pluginInstaller.RestoreAsync(pluginId, null);

                lock (_lock)
                {
                    _updateStatusMap[pluginId] = success ? UpdateStatus.Idle : UpdateStatus.UpdateFailed;
                }

                var resultEvent = new PluginUpdateEvent
                {
                    Plugin = plugin,
                    Status = success ? UpdateStatus.Idle : UpdateStatus.UpdateFailed,
                    Message = success ? 
                        $"Plugin {pluginId} rolled back successfully" : 
                        $"Failed to rollback plugin {pluginId}"
                };
                _eventBus.Publish(resultEvent);
                PluginUpdateStatusChanged?.Invoke(this, resultEvent);

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rolling back plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        private void OnDownloadQueueEvent(object sender, PluginUpdateEvent e)
        {
            if (e?.Plugin != null)
            {
                lock (_lock)
                {
                    _updateStatusMap[e.Plugin.Id] = e.Status;
                }
            }

            _eventBus.Publish(e);
            PluginUpdateStatusChanged?.Invoke(this, e);
        }

        private async void OnPluginDownloadSucceeded(object sender, PluginUpdateEvent e)
        {
            if (e?.Plugin == null)
                return;

            try
            {
                // Update status to Installing
                lock (_lock)
                {
                    _updateStatusMap[e.Plugin.Id] = UpdateStatus.Installing;
                }

                var installingEvent = new PluginUpdateEvent
                {
                    Plugin = e.Plugin,
                    Status = UpdateStatus.Installing,
                    Message = $"Installing plugin {e.Plugin.Name}"
                };
                _eventBus.Publish(installingEvent);
                PluginUpdateStatusChanged?.Invoke(this, installingEvent);

                // Get the downloaded file path from the event
                var downloadPath = e.DownloadedFilePath;
                if (string.IsNullOrEmpty(downloadPath))
                {
                    throw new InvalidOperationException("Downloaded file path is not available");
                }

                // Install/Update the plugin
                var success = await _pluginInstaller.UpdateAsync(e.Plugin, downloadPath);

                lock (_lock)
                {
                    _updateStatusMap[e.Plugin.Id] = success ? UpdateStatus.UpdateSucceeded : UpdateStatus.UpdateFailed;
                }

                var resultEvent = new PluginUpdateEvent
                {
                    Plugin = e.Plugin,
                    Status = success ? UpdateStatus.UpdateSucceeded : UpdateStatus.UpdateFailed,
                    Message = success ? 
                        $"Plugin {e.Plugin.Name} updated successfully" : 
                        $"Failed to install plugin {e.Plugin.Name}",
                    PreviousVersion = e.PreviousVersion,
                    NewVersion = e.Plugin.Version
                };

                _eventBus.Publish(resultEvent);
                PluginUpdateStatusChanged?.Invoke(this, resultEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing plugin {e.Plugin.Id}: {ex.Message}");

                lock (_lock)
                {
                    _updateStatusMap[e.Plugin.Id] = UpdateStatus.UpdateFailed;
                }

                var failedEvent = new PluginUpdateEvent
                {
                    Plugin = e.Plugin,
                    Status = UpdateStatus.UpdateFailed,
                    Message = $"Installation failed: {ex.Message}",
                    Exception = ex
                };

                _eventBus.Publish(failedEvent);
                PluginUpdateStatusChanged?.Invoke(this, failedEvent);
            }
        }

        private void OnPluginUpdateEvent(object sender, PluginUpdateEvent e)
        {
            // This method is called for all events published to the event bus
            // Additional processing can be done here if needed
        }
    }
}
