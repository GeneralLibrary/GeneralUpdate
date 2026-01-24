using System;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Interfaces
{
    /// <summary>
    /// Manages the queue of plugin downloads with status tracking and event notifications.
    /// Integrates with GeneralUpdate.Common.Download.DownloadManager for actual download operations.
    /// </summary>
    public interface IDownloadQueue
    {
        /// <summary>
        /// Enqueues a plugin for download.
        /// </summary>
        /// <param name="plugin">Plugin to download.</param>
        /// <returns>True if successfully queued, false if already in queue.</returns>
        Task<bool> EnqueueAsync(PluginInfo plugin);

        /// <summary>
        /// Removes a plugin from the download queue.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>True if successfully removed, false if not in queue.</returns>
        Task<bool> DequeueAsync(string pluginId);

        /// <summary>
        /// Gets the current download status for a plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>Current update status.</returns>
        UpdateStatus GetStatus(string pluginId);

        /// <summary>
        /// Gets the number of plugins currently in the queue.
        /// </summary>
        /// <returns>Queue size.</returns>
        int GetQueueSize();

        /// <summary>
        /// Clears all plugins from the queue.
        /// </summary>
        Task ClearQueueAsync();

        /// <summary>
        /// Starts processing the download queue.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops processing the download queue.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Event triggered when a plugin enters the Queued status.
        /// </summary>
        event EventHandler<PluginUpdateEvent> PluginQueued;

        /// <summary>
        /// Event triggered when a plugin enters the Updating status.
        /// </summary>
        event EventHandler<PluginUpdateEvent> PluginUpdating;

        /// <summary>
        /// Event triggered when a plugin update succeeds.
        /// </summary>
        event EventHandler<PluginUpdateEvent> PluginUpdateSucceeded;

        /// <summary>
        /// Event triggered when a plugin update fails.
        /// </summary>
        event EventHandler<PluginUpdateEvent> PluginUpdateFailed;

        /// <summary>
        /// Event triggered to report download/update progress.
        /// </summary>
        event EventHandler<PluginUpdateEvent> PluginUpdateProgress;
    }
}
