using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Extension.DTOs;

namespace GeneralUpdate.Extension
{
    /// <summary>
    /// Main orchestrator for the extension system.
    /// Coordinates extension discovery, compatibility validation, updates, and lifecycle management.
    /// </summary>
    public class GeneralExtensionHost : IExtensionHost
    {
        private readonly Version _hostVersion;
        private readonly Metadata.TargetPlatform _targetPlatform;
        private readonly Core.IExtensionCatalog _catalog;
        private readonly Compatibility.ICompatibilityValidator _validator;
        private readonly Download.IUpdateQueue _updateQueue;
        private readonly Installation.ExtensionInstallService _installService;
        private readonly Services.IExtensionService _extensionService;
        private bool _globalAutoUpdateEnabled = true;

        #region Properties

        /// <summary>
        /// Gets the current host application version used for compatibility checking.
        /// </summary>
        public Version HostVersion => _hostVersion;

        /// <summary>
        /// Gets the target platform for extension filtering.
        /// </summary>
        public Metadata.TargetPlatform TargetPlatform => _targetPlatform;

        /// <summary>
        /// Gets or sets a value indicating whether automatic updates are globally enabled.
        /// When disabled, no extensions will be automatically updated.
        /// </summary>
        public bool GlobalAutoUpdateEnabled
        {
            get => _globalAutoUpdateEnabled;
            set => _globalAutoUpdateEnabled = value;
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when an update operation changes state.
        /// </summary>
        public event EventHandler<EventHandlers.UpdateStateChangedEventArgs>? UpdateStateChanged;

        /// <summary>
        /// Occurs when download progress updates.
        /// </summary>
        public event EventHandler<EventHandlers.DownloadProgressEventArgs>? DownloadProgress;

        /// <summary>
        /// Occurs when a download completes successfully.
        /// </summary>
        public event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadCompleted;

        /// <summary>
        /// Occurs when a download fails.
        /// </summary>
        public event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadFailed;

        /// <summary>
        /// Occurs when an installation completes.
        /// </summary>
        public event EventHandler<EventHandlers.InstallationCompletedEventArgs>? InstallationCompleted;

        /// <summary>
        /// Occurs when a rollback completes.
        /// </summary>
        public event EventHandler<EventHandlers.RollbackCompletedEventArgs>? RollbackCompleted;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralExtensionHost"/> class using a configuration object.
        /// </summary>
        /// <param name="config">Configuration settings for the extension host.</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null or required properties are missing.</exception>
        public GeneralExtensionHost(ExtensionHostConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            
            config.Validate();

            _hostVersion = config.HostVersion;
            _targetPlatform = config.TargetPlatform;

            // Initialize core services
            _catalog = new Core.ExtensionCatalog(config.InstallBasePath);
            _validator = new Compatibility.CompatibilityValidator(config.HostVersion);
            _updateQueue = new Download.UpdateQueue();
            _installService = new Installation.ExtensionInstallService(config.InstallBasePath);

            // Initialize extension service with empty list (will be updated via ParseAvailableExtensions)
            _extensionService = new Services.ExtensionService(
                new List<Metadata.AvailableExtension>(),
                config.DownloadPath,
                _updateQueue,
                config.ServerUrl,
                config.HostVersion,
                _validator,
                config.DownloadTimeout,
                config.AuthScheme,
                config.AuthToken);

            // Wire up event handlers
            _updateQueue.StateChanged += (sender, args) => UpdateStateChanged?.Invoke(sender, args);
            _extensionService.ProgressUpdated += (sender, args) => DownloadProgress?.Invoke(sender, args);
            _extensionService.DownloadCompleted += (sender, args) => DownloadCompleted?.Invoke(sender, args);
            _extensionService.DownloadFailed += (sender, args) => DownloadFailed?.Invoke(sender, args);
            _installService.InstallationCompleted += (sender, args) => InstallationCompleted?.Invoke(sender, args);
            _installService.RollbackCompleted += (sender, args) => RollbackCompleted?.Invoke(sender, args);
        }

        #region Extension Catalog

        /// <summary>
        /// Loads all locally installed extensions from the file system.
        /// This should be called during application startup to populate the catalog.
        /// </summary>
        public void LoadInstalledExtensions()
        {
            _catalog.LoadInstalledExtensions();
        }

        /// <summary>
        /// Gets all locally installed extensions currently in the catalog.
        /// </summary>
        /// <returns>A list of installed extensions.</returns>
        public List<Installation.InstalledExtension> GetInstalledExtensions()
        {
            return _catalog.GetInstalledExtensions();
        }

        /// <summary>
        /// Gets installed extensions compatible with the current target platform.
        /// </summary>
        /// <returns>A filtered list of platform-compatible extensions.</returns>
        public List<Installation.InstalledExtension> GetInstalledExtensionsForCurrentPlatform()
        {
            return _catalog.GetInstalledExtensionsByPlatform(_targetPlatform);
        }

        /// <summary>
        /// Retrieves a specific installed extension by its unique identifier.
        /// </summary>
        /// <param name="extensionName">The extension identifier to search for.</param>
        /// <returns>The matching extension if found; otherwise, null.</returns>
        public Installation.InstalledExtension? GetInstalledExtensionById(string extensionName)
        {
            return _catalog.GetInstalledExtensionById(extensionName);
        }

        /// <summary>
        /// Parses available extensions from JSON data received from the server.
        /// </summary>
        /// <param name="json">JSON string containing extension metadata.</param>
        /// <returns>A list of parsed available extensions.</returns>
        public List<Metadata.AvailableExtension> ParseAvailableExtensions(string json)
        {
            var extensions = _catalog.ParseAvailableExtensions(json);
            _extensionService.UpdateAvailableExtensions(extensions);
            return extensions;
        }

        /// <summary>
        /// Gets available extensions that are compatible with the current host version and platform.
        /// Applies both platform and version compatibility filters.
        /// </summary>
        /// <param name="availableExtensions">List of available extensions from the server.</param>
        /// <returns>A filtered list of compatible extensions.</returns>
        public List<Metadata.AvailableExtension> GetCompatibleExtensions(List<Metadata.AvailableExtension> availableExtensions)
        {
            // First filter by platform
            var platformFiltered = _catalog.FilterByPlatform(availableExtensions, _targetPlatform);

            // Then filter by version compatibility
            return _validator.FilterCompatible(platformFiltered);
        }

        #endregion

        #region Update Configuration

        /// <summary>
        /// Sets the auto-update preference for a specific extension.
        /// Changes are persisted in the extension's manifest file.
        /// </summary>
        /// <param name="extensionName">The extension identifier.</param>
        /// <param name="enabled">True to enable auto-updates; false to disable.</param>
        public void SetAutoUpdate(string extensionName, bool enabled)
        {
            var extension = _catalog.GetInstalledExtensionById(extensionName);
            if (extension != null)
            {
                extension.AutoUpdateEnabled = enabled;
                _catalog.AddOrUpdateInstalledExtension(extension);
            }
        }

        /// <summary>
        /// Gets the auto-update preference for a specific extension.
        /// </summary>
        /// <param name="extensionName">The extension identifier.</param>
        /// <returns>True if auto-updates are enabled; otherwise, false.</returns>
        public bool GetAutoUpdate(string extensionName)
        {
            var extension = _catalog.GetInstalledExtensionById(extensionName);
            return extension?.AutoUpdateEnabled ?? false;
        }

        #endregion

        #region Update Operations

        /// <summary>
        /// Queues an extension for update after validating compatibility and platform support.
        /// </summary>
        /// <param name="extension">The extension to update.</param>
        /// <param name="enableRollback">Whether to enable automatic rollback on installation failure.</param>
        /// <returns>The created update operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="extension"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the extension is incompatible.</exception>
        public Download.UpdateOperation QueueUpdate(Metadata.AvailableExtension extension, bool enableRollback = true)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            // Verify compatibility
            if (!_validator.IsCompatible(extension.Descriptor))
            {
                throw new InvalidOperationException(
                    $"Extension '{extension.Descriptor.DisplayName}' is not compatible with host version {_hostVersion}");
            }

            // Verify platform support
            if ((extension.Descriptor.SupportedPlatforms & _targetPlatform) == 0)
            {
                throw new InvalidOperationException(
                    $"Extension '{extension.Descriptor.DisplayName}' does not support the current platform");
            }

            return _updateQueue.Enqueue(extension, enableRollback);
        }

        /// <summary>
        /// Automatically discovers and queues updates for all installed extensions with auto-update enabled.
        /// Only considers extensions that have compatible updates available.
        /// </summary>
        /// <param name="availableExtensions">List of available extensions to check for updates.</param>
        /// <returns>A list of update operations that were queued.</returns>
        public List<Download.UpdateOperation> QueueAutoUpdates(List<Metadata.AvailableExtension> availableExtensions)
        {
            if (!_globalAutoUpdateEnabled)
                return new List<Download.UpdateOperation>();

            var queuedOperations = new List<Download.UpdateOperation>();
            var installedExtensions = _catalog.GetInstalledExtensions();

            foreach (var installed in installedExtensions)
            {
                if (!installed.AutoUpdateEnabled)
                    continue;

                // Find available versions for this extension
                var versions = availableExtensions
                    .Where(ext => ext.Descriptor.Name == installed.Descriptor.Name)
                    .ToList();

                if (!versions.Any())
                    continue;

                // Get the latest compatible update
                var update = _validator.GetCompatibleUpdate(installed, versions);

                if (update != null)
                {
                    try
                    {
                        var operation = QueueUpdate(update, true);
                        queuedOperations.Add(operation);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other extensions
                        GeneralUpdate.Common.Shared.GeneralTracer.Error(
                            $"Failed to queue auto-update for extension {installed.Descriptor.Name}", ex);
                    }
                }
            }

            return queuedOperations;
        }

        /// <summary>
        /// Finds the best upgrade version for a specific extension.
        /// Selects the minimum supported version among the latest compatible versions.
        /// </summary>
        /// <param name="extensionName">The extension identifier.</param>
        /// <param name="availableExtensions">Available versions of the extension.</param>
        /// <returns>The best compatible version if found; otherwise, null.</returns>
        public Metadata.AvailableExtension? FindBestUpgrade(string extensionName, List<Metadata.AvailableExtension> availableExtensions)
        {
            var versions = availableExtensions
                .Where(ext => ext.Descriptor.Name == extensionName)
                .ToList();

            return _validator.FindMinimumSupportedLatest(versions);
        }

        /// <summary>
        /// Processes the next queued update operation by downloading and installing the extension.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result indicates success.</returns>
        public async Task<bool> ProcessNextUpdateAsync()
        {
            var operation = _updateQueue.GetNextQueued();
            if (operation == null)
                return false;

            try
            {
                // Download the extension package
                var downloadedPath = await _extensionService.DownloadAsync(operation);

                if (downloadedPath == null)
                    return false;

                // Install the extension
                var installed = await _installService.InstallAsync(
                    downloadedPath,
                    operation.Extension.Descriptor,
                    operation.EnableRollback);

                if (installed != null)
                {
                    _catalog.AddOrUpdateInstalledExtension(installed);
                    _updateQueue.ChangeState(operation.OperationId, Download.UpdateState.UpdateSuccessful);
                    return true;
                }
                else
                {
                    _updateQueue.ChangeState(operation.OperationId, Download.UpdateState.UpdateFailed, "Installation failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _updateQueue.ChangeState(operation.OperationId, Download.UpdateState.UpdateFailed, ex.Message);
                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Failed to process update for operation {operation.OperationId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Processes all queued update operations sequentially.
        /// Continues processing until the queue is empty.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ProcessAllUpdatesAsync()
        {
            while (await ProcessNextUpdateAsync())
            {
                // Continue processing until queue is empty
            }
        }

        /// <summary>
        /// Gets all update operations currently in the queue.
        /// </summary>
        /// <returns>A list of all update operations.</returns>
        public List<Download.UpdateOperation> GetUpdateQueue()
        {
            return _updateQueue.GetAllOperations();
        }

        /// <summary>
        /// Gets update operations filtered by their current state.
        /// </summary>
        /// <param name="state">The state to filter by.</param>
        /// <returns>A list of operations with the specified state.</returns>
        public List<Download.UpdateOperation> GetUpdatesByState(Download.UpdateState state)
        {
            return _updateQueue.GetOperationsByState(state);
        }

        /// <summary>
        /// Clears completed update operations from the queue to prevent memory accumulation.
        /// Removes operations that are successful, failed, or cancelled.
        /// </summary>
        public void ClearCompletedUpdates()
        {
            _updateQueue.ClearCompleted();
        }

        #endregion

        #region Compatibility

        /// <summary>
        /// Checks if an extension descriptor is compatible with the current host version.
        /// </summary>
        /// <param name="descriptor">The extension descriptor to validate.</param>
        /// <returns>True if compatible; otherwise, false.</returns>
        public bool IsCompatible(Metadata.ExtensionDescriptor descriptor)
        {
            return _validator.IsCompatible(descriptor);
        }
        
        #endregion
        
        /// <summary>
        /// Queries remote extensions from the server with pagination and filtering.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryRemoteExtensions(ExtensionQueryDTO query)
        {
            return _extensionService.Query(query);
        }
    }
}
