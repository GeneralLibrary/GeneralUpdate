using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Events;
using GeneralUpdate.Extension.Models;
using GeneralUpdate.Extension.Queue;
using GeneralUpdate.Extension.Services;

namespace GeneralUpdate.Extension
{
    /// <summary>
    /// Main manager for the extension system.
    /// Orchestrates extension list management, updates, downloads, and installations.
    /// </summary>
    public class ExtensionManager
    {
        private readonly Version _clientVersion;
        private readonly ExtensionListManager _listManager;
        private readonly VersionCompatibilityChecker _compatibilityChecker;
        private readonly ExtensionUpdateQueue _updateQueue;
        private readonly ExtensionDownloader _downloader;
        private readonly ExtensionInstaller _installer;
        private readonly ExtensionPlatform _currentPlatform;
        private bool _globalAutoUpdateEnabled = true;

        #region Events

        /// <summary>
        /// Event fired when an update status changes.
        /// </summary>
        public event EventHandler<ExtensionUpdateStatusChangedEventArgs>? UpdateStatusChanged;

        /// <summary>
        /// Event fired when download progress updates.
        /// </summary>
        public event EventHandler<ExtensionDownloadProgressEventArgs>? DownloadProgress;

        /// <summary>
        /// Event fired when a download completes.
        /// </summary>
        public event EventHandler<ExtensionEventArgs>? DownloadCompleted;

        /// <summary>
        /// Event fired when a download fails.
        /// </summary>
        public event EventHandler<ExtensionEventArgs>? DownloadFailed;

        /// <summary>
        /// Event fired when installation completes.
        /// </summary>
        public event EventHandler<ExtensionInstallEventArgs>? InstallCompleted;

        /// <summary>
        /// Event fired when rollback completes.
        /// </summary>
        public event EventHandler<ExtensionRollbackEventArgs>? RollbackCompleted;

        #endregion

        /// <summary>
        /// Initializes a new instance of the ExtensionManager.
        /// </summary>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="installBasePath">Base path for extension installations.</param>
        /// <param name="downloadPath">Path for downloading extensions.</param>
        /// <param name="currentPlatform">Current platform (Windows/Linux/macOS).</param>
        /// <param name="downloadTimeout">Download timeout in seconds.</param>
        public ExtensionManager(
            Version clientVersion, 
            string installBasePath, 
            string downloadPath,
            ExtensionPlatform currentPlatform = ExtensionPlatform.Windows,
            int downloadTimeout = 300)
        {
            _clientVersion = clientVersion ?? throw new ArgumentNullException(nameof(clientVersion));
            _currentPlatform = currentPlatform;
            
            _listManager = new ExtensionListManager(installBasePath);
            _compatibilityChecker = new VersionCompatibilityChecker(clientVersion);
            _updateQueue = new ExtensionUpdateQueue();
            _downloader = new ExtensionDownloader(downloadPath, _updateQueue, downloadTimeout);
            _installer = new ExtensionInstaller(installBasePath);

            // Wire up events
            _updateQueue.StatusChanged += (sender, args) => UpdateStatusChanged?.Invoke(sender, args);
            _downloader.DownloadProgress += (sender, args) => DownloadProgress?.Invoke(sender, args);
            _downloader.DownloadCompleted += (sender, args) => DownloadCompleted?.Invoke(sender, args);
            _downloader.DownloadFailed += (sender, args) => DownloadFailed?.Invoke(sender, args);
            _installer.InstallCompleted += (sender, args) => InstallCompleted?.Invoke(sender, args);
            _installer.RollbackCompleted += (sender, args) => RollbackCompleted?.Invoke(sender, args);
        }

        #region Extension List Management

        /// <summary>
        /// Loads local extensions from the file system.
        /// </summary>
        public void LoadLocalExtensions()
        {
            _listManager.LoadLocalExtensions();
        }

        /// <summary>
        /// Gets all locally installed extensions.
        /// </summary>
        /// <returns>List of local extensions.</returns>
        public List<LocalExtension> GetLocalExtensions()
        {
            return _listManager.GetLocalExtensions();
        }

        /// <summary>
        /// Gets local extensions for the current platform.
        /// </summary>
        /// <returns>List of local extensions compatible with the current platform.</returns>
        public List<LocalExtension> GetLocalExtensionsForCurrentPlatform()
        {
            return _listManager.GetLocalExtensionsByPlatform(_currentPlatform);
        }

        /// <summary>
        /// Gets a local extension by ID.
        /// </summary>
        /// <param name="extensionId">The extension ID.</param>
        /// <returns>The local extension or null if not found.</returns>
        public LocalExtension? GetLocalExtensionById(string extensionId)
        {
            return _listManager.GetLocalExtensionById(extensionId);
        }

        /// <summary>
        /// Parses remote extensions from JSON.
        /// </summary>
        /// <param name="json">JSON string containing remote extensions.</param>
        /// <returns>List of remote extensions.</returns>
        public List<RemoteExtension> ParseRemoteExtensions(string json)
        {
            return _listManager.ParseRemoteExtensions(json);
        }

        /// <summary>
        /// Gets remote extensions compatible with the current platform and client version.
        /// </summary>
        /// <param name="remoteExtensions">List of remote extensions from server.</param>
        /// <returns>Filtered list of compatible remote extensions.</returns>
        public List<RemoteExtension> GetCompatibleRemoteExtensions(List<RemoteExtension> remoteExtensions)
        {
            // First filter by platform
            var platformFiltered = _listManager.FilterRemoteExtensionsByPlatform(remoteExtensions, _currentPlatform);
            
            // Then filter by version compatibility
            return _compatibilityChecker.FilterCompatibleExtensions(platformFiltered);
        }

        #endregion

        #region Auto-Update Settings

        /// <summary>
        /// Gets or sets the global auto-update setting.
        /// </summary>
        public bool GlobalAutoUpdateEnabled
        {
            get => _globalAutoUpdateEnabled;
            set => _globalAutoUpdateEnabled = value;
        }

        /// <summary>
        /// Sets auto-update for a specific extension.
        /// </summary>
        /// <param name="extensionId">The extension ID.</param>
        /// <param name="enabled">Whether to enable auto-update.</param>
        public void SetExtensionAutoUpdate(string extensionId, bool enabled)
        {
            var extension = _listManager.GetLocalExtensionById(extensionId);
            if (extension != null)
            {
                extension.AutoUpdateEnabled = enabled;
                _listManager.AddOrUpdateLocalExtension(extension);
            }
        }

        /// <summary>
        /// Gets the auto-update setting for a specific extension.
        /// </summary>
        /// <param name="extensionId">The extension ID.</param>
        /// <returns>True if auto-update is enabled, false otherwise.</returns>
        public bool GetExtensionAutoUpdate(string extensionId)
        {
            var extension = _listManager.GetLocalExtensionById(extensionId);
            return extension?.AutoUpdateEnabled ?? false;
        }

        #endregion

        #region Update Management

        /// <summary>
        /// Queues an extension for update.
        /// </summary>
        /// <param name="remoteExtension">The remote extension to update to.</param>
        /// <param name="enableRollback">Whether to enable rollback on failure.</param>
        /// <returns>The queue item created.</returns>
        public ExtensionUpdateQueueItem QueueExtensionUpdate(RemoteExtension remoteExtension, bool enableRollback = true)
        {
            if (remoteExtension == null)
                throw new ArgumentNullException(nameof(remoteExtension));

            // Verify compatibility
            if (!_compatibilityChecker.IsCompatible(remoteExtension.Metadata))
            {
                throw new InvalidOperationException($"Extension {remoteExtension.Metadata.Name} is not compatible with client version {_clientVersion}");
            }

            // Verify platform support
            if ((remoteExtension.Metadata.SupportedPlatforms & _currentPlatform) == 0)
            {
                throw new InvalidOperationException($"Extension {remoteExtension.Metadata.Name} does not support the current platform");
            }

            return _updateQueue.Enqueue(remoteExtension, enableRollback);
        }

        /// <summary>
        /// Finds and queues updates for all local extensions that have auto-update enabled.
        /// </summary>
        /// <param name="remoteExtensions">List of remote extensions available.</param>
        /// <returns>List of queue items created.</returns>
        public List<ExtensionUpdateQueueItem> QueueAutoUpdates(List<RemoteExtension> remoteExtensions)
        {
            if (!_globalAutoUpdateEnabled)
                return new List<ExtensionUpdateQueueItem>();

            var queuedItems = new List<ExtensionUpdateQueueItem>();
            var localExtensions = _listManager.GetLocalExtensions();

            foreach (var localExtension in localExtensions)
            {
                if (!localExtension.AutoUpdateEnabled)
                    continue;

                // Find available versions for this extension
                var availableVersions = remoteExtensions
                    .Where(re => re.Metadata.Id == localExtension.Metadata.Id)
                    .ToList();

                if (!availableVersions.Any())
                    continue;

                // Get the latest compatible update
                var update = _compatibilityChecker.GetCompatibleUpdate(localExtension, availableVersions);

                if (update != null)
                {
                    var queueItem = QueueExtensionUpdate(update, true);
                    queuedItems.Add(queueItem);
                }
            }

            return queuedItems;
        }

        /// <summary>
        /// Finds the latest compatible version of an extension for upgrade.
        /// Automatically matches the minimum supported extension version among the latest versions.
        /// </summary>
        /// <param name="extensionId">The extension ID.</param>
        /// <param name="remoteExtensions">List of remote extension versions.</param>
        /// <returns>The best compatible version for upgrade, or null if none found.</returns>
        public RemoteExtension? FindBestUpgradeVersion(string extensionId, List<RemoteExtension> remoteExtensions)
        {
            var versions = remoteExtensions.Where(re => re.Metadata.Id == extensionId).ToList();
            return _compatibilityChecker.FindMinimumSupportedLatestVersion(versions);
        }

        /// <summary>
        /// Processes the next queued update.
        /// </summary>
        /// <returns>True if an update was processed, false if queue is empty.</returns>
        public async Task<bool> ProcessNextUpdateAsync()
        {
            var queueItem = _updateQueue.GetNextQueued();
            if (queueItem == null)
                return false;

            try
            {
                // Download the extension
                var downloadedPath = await _downloader.DownloadExtensionAsync(queueItem);
                
                if (downloadedPath == null)
                    return false;

                // Install the extension
                var localExtension = await _installer.InstallExtensionAsync(
                    downloadedPath, 
                    queueItem.Extension.Metadata, 
                    queueItem.EnableRollback);

                if (localExtension != null)
                {
                    _listManager.AddOrUpdateLocalExtension(localExtension);
                    _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateSuccessful);
                    return true;
                }
                else
                {
                    _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, "Installation failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Processes all queued updates.
        /// </summary>
        public async Task ProcessAllUpdatesAsync()
        {
            while (await ProcessNextUpdateAsync())
            {
                // Continue processing until queue is empty
            }
        }

        /// <summary>
        /// Gets all items in the update queue.
        /// </summary>
        /// <returns>List of all queue items.</returns>
        public List<ExtensionUpdateQueueItem> GetUpdateQueue()
        {
            return _updateQueue.GetAllItems();
        }

        /// <summary>
        /// Gets queue items by status.
        /// </summary>
        /// <param name="status">The status to filter by.</param>
        /// <returns>List of queue items with the specified status.</returns>
        public List<ExtensionUpdateQueueItem> GetUpdateQueueByStatus(ExtensionUpdateStatus status)
        {
            return _updateQueue.GetItemsByStatus(status);
        }

        /// <summary>
        /// Clears completed or failed items from the queue.
        /// </summary>
        public void ClearCompletedUpdates()
        {
            _updateQueue.ClearCompletedItems();
        }

        #endregion

        #region Version Compatibility

        /// <summary>
        /// Checks if an extension is compatible with the client.
        /// </summary>
        /// <param name="metadata">Extension metadata to check.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsExtensionCompatible(ExtensionMetadata metadata)
        {
            return _compatibilityChecker.IsCompatible(metadata);
        }

        /// <summary>
        /// Gets the current client version.
        /// </summary>
        public Version ClientVersion => _clientVersion;

        /// <summary>
        /// Gets the current platform.
        /// </summary>
        public ExtensionPlatform CurrentPlatform => _currentPlatform;

        #endregion
    }
}
