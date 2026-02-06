using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Extension.DTOs;

namespace GeneralUpdate.Extension
{
    /// <summary>
    /// Defines the main contract for the extension host system.
    /// Orchestrates extension discovery, compatibility checking, updates, and lifecycle management.
    /// </summary>
    public interface IExtensionHost
    {
        #region Properties

        /// <summary>
        /// Gets the current host application version.
        /// </summary>
        Version HostVersion { get; }

        /// <summary>
        /// Gets the target platform for extension filtering.
        /// </summary>
        Metadata.TargetPlatform TargetPlatform { get; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic updates are globally enabled.
        /// </summary>
        bool GlobalAutoUpdateEnabled { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when an update operation changes state.
        /// </summary>
        event EventHandler<EventHandlers.UpdateStateChangedEventArgs>? UpdateStateChanged;

        /// <summary>
        /// Occurs when download progress updates.
        /// </summary>
        event EventHandler<EventHandlers.DownloadProgressEventArgs>? DownloadProgress;

        /// <summary>
        /// Occurs when a download completes successfully.
        /// </summary>
        event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadCompleted;

        /// <summary>
        /// Occurs when a download fails.
        /// </summary>
        event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadFailed;

        /// <summary>
        /// Occurs when an installation completes.
        /// </summary>
        event EventHandler<EventHandlers.InstallationCompletedEventArgs>? InstallationCompleted;

        /// <summary>
        /// Occurs when a rollback completes.
        /// </summary>
        event EventHandler<EventHandlers.RollbackCompletedEventArgs>? RollbackCompleted;

        #endregion

        #region Extension Catalog

        /// <summary>
        /// Loads all locally installed extensions from the file system.
        /// </summary>
        void LoadInstalledExtensions();

        /// <summary>
        /// Gets all locally installed extensions.
        /// </summary>
        /// <returns>A list of installed extensions.</returns>
        List<Installation.InstalledExtension> GetInstalledExtensions();

        /// <summary>
        /// Gets installed extensions compatible with the current platform.
        /// </summary>
        /// <returns>A list of platform-compatible installed extensions.</returns>
        List<Installation.InstalledExtension> GetInstalledExtensionsForCurrentPlatform();

        /// <summary>
        /// Gets an installed extension by its identifier.
        /// </summary>
        /// <param name="extensionId">The extension identifier.</param>
        /// <returns>The installed extension if found; otherwise, null.</returns>
        Installation.InstalledExtension? GetInstalledExtensionById(string extensionId);

        /// <summary>
        /// Parses available extensions from JSON data.
        /// </summary>
        /// <param name="json">JSON string containing extension metadata.</param>
        /// <returns>A list of available extensions.</returns>
        List<Metadata.AvailableExtension> ParseAvailableExtensions(string json);

        /// <summary>
        /// Gets available extensions compatible with the current host and platform.
        /// </summary>
        /// <param name="availableExtensions">List of available extensions from the server.</param>
        /// <returns>A filtered list of compatible extensions.</returns>
        List<Metadata.AvailableExtension> GetCompatibleExtensions(List<Metadata.AvailableExtension> availableExtensions);

        #endregion

        #region Update Configuration

        /// <summary>
        /// Sets the auto-update preference for a specific extension.
        /// </summary>
        /// <param name="extensionId">The extension identifier.</param>
        /// <param name="enabled">True to enable auto-updates; false to disable.</param>
        void SetAutoUpdate(string extensionId, bool enabled);

        /// <summary>
        /// Gets the auto-update preference for a specific extension.
        /// </summary>
        /// <param name="extensionId">The extension identifier.</param>
        /// <returns>True if auto-updates are enabled; otherwise, false.</returns>
        bool GetAutoUpdate(string extensionId);

        #endregion

        #region Update Operations

        /// <summary>
        /// Queues an extension for update.
        /// </summary>
        /// <param name="extension">The extension to update.</param>
        /// <param name="enableRollback">Whether to enable rollback on failure.</param>
        /// <returns>The created update operation.</returns>
        Download.UpdateOperation QueueUpdate(Metadata.AvailableExtension extension, bool enableRollback = true);

        /// <summary>
        /// Automatically queues updates for all installed extensions with auto-update enabled.
        /// </summary>
        /// <param name="availableExtensions">List of available extensions to check for updates.</param>
        /// <returns>A list of update operations that were queued.</returns>
        List<Download.UpdateOperation> QueueAutoUpdates(List<Metadata.AvailableExtension> availableExtensions);

        /// <summary>
        /// Finds the best upgrade version for a specific extension.
        /// Matches the minimum supported version among the latest compatible versions.
        /// </summary>
        /// <param name="extensionId">The extension identifier.</param>
        /// <param name="availableExtensions">Available versions of the extension.</param>
        /// <returns>The best compatible version if found; otherwise, null.</returns>
        Metadata.AvailableExtension? FindBestUpgrade(string extensionId, List<Metadata.AvailableExtension> availableExtensions);

        /// <summary>
        /// Processes the next queued update operation.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result indicates success.</returns>
        Task<bool> ProcessNextUpdateAsync();

        /// <summary>
        /// Processes all queued update operations sequentially.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ProcessAllUpdatesAsync();

        /// <summary>
        /// Gets all update operations in the queue.
        /// </summary>
        /// <returns>A list of all update operations.</returns>
        List<Download.UpdateOperation> GetUpdateQueue();

        /// <summary>
        /// Gets update operations filtered by state.
        /// </summary>
        /// <param name="state">The state to filter by.</param>
        /// <returns>A list of operations with the specified state.</returns>
        List<Download.UpdateOperation> GetUpdatesByState(Download.UpdateState state);

        /// <summary>
        /// Clears completed update operations from the queue.
        /// </summary>
        void ClearCompletedUpdates();

        #endregion

        #region Compatibility

        /// <summary>
        /// Checks if an extension descriptor is compatible with the current host version.
        /// </summary>
        /// <param name="descriptor">The extension descriptor to validate.</param>
        /// <returns>True if compatible; otherwise, false.</returns>
        bool IsCompatible(Metadata.ExtensionDescriptor descriptor);

        #endregion

        #region Remote Queries

        Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryRemoteExtensions(ExtensionQueryDTO query);

        #endregion
    }
}
