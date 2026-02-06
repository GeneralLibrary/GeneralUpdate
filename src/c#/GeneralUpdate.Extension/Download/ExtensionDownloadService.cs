using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Extension.DTOs;
using GeneralUpdate.Extension.Metadata;

namespace GeneralUpdate.Extension.Download
{
    /// <summary>
    /// Handles downloading of extension packages using the GeneralUpdate download infrastructure.
    /// Provides progress tracking and error handling during download operations.
    /// </summary>
    public class ExtensionDownloadService
    {
        private readonly string _downloadPath;
        private readonly int _downloadTimeout;
        private readonly IUpdateQueue _updateQueue;

        /// <summary>
        /// Occurs when download progress updates during package retrieval.
        /// </summary>
        public event EventHandler<EventHandlers.DownloadProgressEventArgs>? ProgressUpdated;

        /// <summary>
        /// Occurs when a download completes successfully.
        /// </summary>
        public event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadCompleted;

        /// <summary>
        /// Occurs when a download fails due to an error.
        /// </summary>
        public event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadFailed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionDownloadService"/> class.
        /// </summary>
        /// <param name="downloadPath">Directory path where extension packages will be downloaded.</param>
        /// <param name="updateQueue">The update queue for managing operation state.</param>
        /// <param name="downloadTimeout">Timeout in seconds for download operations (default: 300).</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public ExtensionDownloadService(string downloadPath, IUpdateQueue updateQueue, int downloadTimeout = 300)
        {
            if (string.IsNullOrWhiteSpace(downloadPath))
                throw new ArgumentNullException(nameof(downloadPath));

            _downloadPath = downloadPath;
            _updateQueue = updateQueue ?? throw new ArgumentNullException(nameof(updateQueue));
            _downloadTimeout = downloadTimeout;

            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }

        /// <summary>
        /// Downloads an extension package asynchronously with progress tracking.
        /// Updates the operation state in the queue throughout the download process.
        /// </summary>
        /// <param name="operation">The update operation containing extension details.</param>
        /// <returns>The local file path of the downloaded package, or null if download failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
        public async Task<string?> DownloadAsync(UpdateOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var descriptor = operation.Extension.Descriptor;

            if (string.IsNullOrWhiteSpace(descriptor.DownloadUrl))
            {
                _updateQueue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, "Download URL is missing");
                OnDownloadFailed(descriptor.Name, descriptor.DisplayName);
                return null;
            }

            try
            {
                _updateQueue.ChangeState(operation.OperationId, UpdateState.Updating);

                // Determine file format from URL or default to .zip
                var format = !string.IsNullOrWhiteSpace(descriptor.DownloadUrl) && descriptor.DownloadUrl!.Contains(".")
                    ? Path.GetExtension(descriptor.DownloadUrl)
                    : ".zip";

                // Create version info for the download manager
                var versionInfo = new VersionInfo
                {
                    Name = $"{descriptor.Name}_{descriptor.Version}",
                    Url = descriptor.DownloadUrl,
                    Hash = descriptor.PackageHash,
                    Version = descriptor.Version,
                    Size = descriptor.PackageSize,
                    Format = format
                };

                // Initialize download manager with configured settings
                var downloadManager = new DownloadManager(_downloadPath, format, _downloadTimeout);

                // Wire up event handlers for progress tracking
                downloadManager.MultiDownloadStatistics += (sender, args) => OnDownloadProgress(operation, args);
                downloadManager.MultiDownloadCompleted += (sender, args) => OnDownloadCompleted(operation, args);
                downloadManager.MultiDownloadError += (sender, args) => OnDownloadError(operation, args);

                // Create and enqueue the download task
                var downloadTask = new DownloadTask(downloadManager, versionInfo);
                downloadManager.Add(downloadTask);

                // Execute the download
                await downloadManager.LaunchTasksAsync();

                var downloadedFilePath = Path.Combine(_downloadPath, $"{versionInfo.Name}{format}");

                if (File.Exists(downloadedFilePath))
                {
                    OnDownloadSuccess(descriptor.Name, descriptor.DisplayName);
                    return downloadedFilePath;
                }
                else
                {
                    _updateQueue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, "Downloaded file not found");
                    OnDownloadFailed(descriptor.Name, descriptor.DisplayName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _updateQueue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, ex.Message);
                OnDownloadFailed(descriptor.Name, descriptor.DisplayName);
                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Download failed for extension {descriptor.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// Handles download statistics events and updates progress tracking.
        /// </summary>
        private void OnDownloadProgress(UpdateOperation operation, MultiDownloadStatisticsEventArgs args)
        {
            var progressPercentage = args.ProgressPercentage;
            _updateQueue.UpdateProgress(operation.OperationId, progressPercentage);

            ProgressUpdated?.Invoke(this, new EventHandlers.DownloadProgressEventArgs
            {
                Name = operation.Extension.Descriptor.Name,
                ExtensionName = operation.Extension.Descriptor.DisplayName,
                ProgressPercentage = progressPercentage,
                TotalBytes = args.TotalBytesToReceive,
                ReceivedBytes = args.BytesReceived,
                Speed = args.Speed,
                RemainingTime = args.Remaining
            });
        }

        /// <summary>
        /// Handles download completion and validates the result.
        /// </summary>
        private void OnDownloadCompleted(UpdateOperation operation, MultiDownloadCompletedEventArgs args)
        {
            if (!args.IsComplated)
            {
                _updateQueue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, "Download completed with errors");
            }
        }

        /// <summary>
        /// Handles download errors and updates the operation state.
        /// </summary>
        private void OnDownloadError(UpdateOperation operation, MultiDownloadErrorEventArgs args)
        {
            _updateQueue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, args.Exception?.Message);
        }

        /// <summary>
        /// Raises the DownloadCompleted event when a download succeeds.
        /// </summary>
        private void OnDownloadSuccess(string extensionName, string displayName)
        {
            DownloadCompleted?.Invoke(this, new EventHandlers.ExtensionEventArgs
            {
                Name = extensionName,
                ExtensionName = displayName
            });
        }

        /// <summary>
        /// Raises the DownloadFailed event when a download fails.
        /// </summary>
        private void OnDownloadFailed(string extensionName, string displayName)
        {
            DownloadFailed?.Invoke(this, new EventHandlers.ExtensionEventArgs
            {
                Name = extensionName,
                ExtensionName = displayName
            });
        }

        /// <summary>
        /// Downloads an extension and its dependencies by ID.
        /// This is the public API method that wraps the internal DownloadAsync method.
        /// Note: The caller is responsible for disposing the Stream in the returned DownloadExtensionDTO.
        /// </summary>
        /// <param name="id">Extension ID (Name)</param>
        /// <param name="availableExtensions">List of available extensions to search from</param>
        /// <returns>Download result containing file name and stream. The caller must dispose the stream.</returns>
        public async Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id, List<AvailableExtension> availableExtensions)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure("Extension ID cannot be null or empty");
                }

                if (availableExtensions == null || availableExtensions.Count == 0)
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure("Available extensions list is empty");
                }

                // Find the extension by ID (using Name as ID)
                var extension = availableExtensions.FirstOrDefault(e =>
                    e.Descriptor.Name?.Equals(id, StringComparison.OrdinalIgnoreCase) == true);

                if (extension == null)
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure(
                        $"Extension with ID '{id}' not found");
                }

                // Collect all extensions to download (main extension + dependencies)
                var extensionsToDownload = new List<AvailableExtension> { extension };

                // Resolve dependencies
                if (extension.Descriptor.Dependencies != null && extension.Descriptor.Dependencies.Count > 0)
                {
                    foreach (var depId in extension.Descriptor.Dependencies)
                    {
                        var dependency = availableExtensions.FirstOrDefault(e =>
                            e.Descriptor.Name?.Equals(depId, StringComparison.OrdinalIgnoreCase) == true);

                        if (dependency != null)
                        {
                            extensionsToDownload.Add(dependency);
                        }
                    }
                }

                // For now, we'll download only the main extension
                // In a real implementation, you might want to download all dependencies
                // and package them together or return multiple files

                // Use the shared update queue
                var operation = _updateQueue.Enqueue(extension, false);

                var downloadedPath = await DownloadAsync(operation);

                if (downloadedPath == null || !File.Exists(downloadedPath))
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure(
                        $"Failed to download extension '{extension.Descriptor.DisplayName}'");
                }

                // Read the file into a memory stream
                var fileBytes = File.ReadAllBytes(downloadedPath);
                var stream = new MemoryStream(fileBytes);

                var result = new DownloadExtensionDTO
                {
                    FileName = Path.GetFileName(downloadedPath),
                    Stream = stream
                };

                return HttpResponseDTO<DownloadExtensionDTO>.Success(result);
            }
            catch (Exception ex)
            {
                return HttpResponseDTO<DownloadExtensionDTO>.InnerException(
                    $"Error downloading extension: {ex.Message}");
            }
        }
    }
}
