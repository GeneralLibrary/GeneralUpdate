using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Extension.DTOs;
using GeneralUpdate.Extension.Metadata;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Implementation of extension query and download operations.
    /// Handles downloading of extension packages using the GeneralUpdate download infrastructure.
    /// Provides progress tracking and error handling during download operations.
    /// </summary>
    public class ExtensionService : IExtensionService
    {
        private List<AvailableExtension> _availableExtensions;
        private readonly Version? _hostVersion;
        private readonly Compatibility.ICompatibilityValidator? _validator;
        private readonly string _downloadPath;
        private readonly int _downloadTimeout;
        private readonly Download.IUpdateQueue _updateQueue;
        private readonly string? _authScheme;
        private readonly string? _authToken;
        private readonly string _serverUrl;

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
        /// Initializes a new instance of the <see cref="ExtensionService"/> class.
        /// </summary>
        /// <param name="availableExtensions">List of available extensions</param>
        /// <param name="downloadPath">Directory path where extension packages will be downloaded</param>
        /// <param name="updateQueue">The update queue for managing operation state</param>
        /// <param name="serverUrl">Server base URL for extension queries and downloads</param>
        /// <param name="hostVersion">Optional host version for compatibility checking</param>
        /// <param name="validator">Optional compatibility validator</param>
        /// <param name="downloadTimeout">Timeout in seconds for download operations (default: 300)</param>
        /// <param name="authScheme">Optional HTTP authentication scheme (e.g., "Bearer", "Basic")</param>
        /// <param name="authToken">Optional HTTP authentication token</param>
        public ExtensionService(
            List<AvailableExtension> availableExtensions,
            string downloadPath,
            Download.IUpdateQueue updateQueue,
            string serverUrl,
            Version? hostVersion = null,
            Compatibility.ICompatibilityValidator? validator = null,
            int downloadTimeout = 300,
            string? authScheme = null,
            string? authToken = null)
        {
            _availableExtensions = availableExtensions ?? throw new ArgumentNullException(nameof(availableExtensions));
            
            if (string.IsNullOrWhiteSpace(downloadPath))
                throw new ArgumentNullException(nameof(downloadPath));
            
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentNullException(nameof(serverUrl));
            
            _downloadPath = downloadPath;
            _serverUrl = serverUrl.TrimEnd('/'); // Remove trailing slash for consistent URL construction
            _updateQueue = updateQueue ?? throw new ArgumentNullException(nameof(updateQueue));
            _downloadTimeout = downloadTimeout;
            _hostVersion = hostVersion;
            _validator = validator;
            _authScheme = authScheme;
            _authToken = authToken;

            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }

        /// <summary>
        /// Updates the list of available extensions
        /// </summary>
        /// <param name="availableExtensions">New list of available extensions</param>
        public void UpdateAvailableExtensions(List<AvailableExtension> availableExtensions)
        {
            _availableExtensions = availableExtensions ?? throw new ArgumentNullException(nameof(availableExtensions));
        }

        /// <summary>
        /// Queries available extensions based on filter criteria
        /// </summary>
        /// <param name="query">Query parameters including pagination and filters</param>
        /// <returns>Paginated result of extensions matching the query</returns>
        public Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> Query(ExtensionQueryDTO query)
        {
            try
            {
                if (query == null)
                {
                    return Task.FromResult(HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "Query parameter cannot be null"));
                }

                // Validate pagination parameters
                if (query.PageNumber < 1)
                {
                    return Task.FromResult(HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "PageNumber must be greater than 0"));
                }

                if (query.PageSize < 1)
                {
                    return Task.FromResult(HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "PageSize must be greater than 0"));
                }

                // Parse host version if provided
                Version? queryHostVersion = null;
                if (!string.IsNullOrWhiteSpace(query.HostVersion))
                {
                    if (!Version.TryParse(query.HostVersion, out queryHostVersion))
                    {
                        return Task.FromResult(HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                            $"Invalid host version format: {query.HostVersion}"));
                    }
                }

                // Use query host version if provided, otherwise use service host version
                var effectiveHostVersion = queryHostVersion ?? _hostVersion;

                // Start with all available extensions
                IEnumerable<AvailableExtension> filtered = _availableExtensions;

                // Apply filters
                if (!string.IsNullOrWhiteSpace(query.Name))
                {
                    filtered = filtered.Where(e =>
                        e.Descriptor.Name?.IndexOf(query.Name, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (!string.IsNullOrWhiteSpace(query.Publisher))
                {
                    filtered = filtered.Where(e =>
                        e.Descriptor.Publisher?.IndexOf(query.Publisher, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (!string.IsNullOrWhiteSpace(query.Category))
                {
                    filtered = filtered.Where(e =>
                        e.Descriptor.Categories?.Any(c =>
                            c.IndexOf(query.Category, StringComparison.OrdinalIgnoreCase) >= 0) == true);
                }

                if (query.TargetPlatform.HasValue && query.TargetPlatform.Value != TargetPlatform.None)
                {
                    filtered = filtered.Where(e =>
                        (e.Descriptor.SupportedPlatforms & query.TargetPlatform.Value) != 0);
                }

                if (!query.IncludePreRelease)
                {
                    filtered = filtered.Where(e => !e.IsPreRelease);
                }

                if (!string.IsNullOrWhiteSpace(query.SearchTerm))
                {
                    filtered = filtered.Where(e =>
                        (e.Descriptor.Name?.IndexOf(query.SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (e.Descriptor.DisplayName?.IndexOf(query.SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (e.Descriptor.Description?.IndexOf(query.SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0));
                }

                // Convert to list for pagination
                var filteredList = filtered.ToList();

                // Calculate pagination
                var totalCount = filteredList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

                // Apply pagination
                var items = filteredList
                    .Skip((query.PageNumber - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .Select(e => MapToExtensionDTO(e, effectiveHostVersion))
                    .ToList();

                var result = new PagedResultDTO<ExtensionDTO>
                {
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    Items = items
                };

                return Task.FromResult(HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Success(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.InnerException(
                    $"Error querying extensions: {ex.Message}"));
            }
        }

        /// <summary>
        /// Downloads an extension and its dependencies by ID.
        /// Note: The caller is responsible for disposing the Stream in the returned DownloadExtensionDTO.
        /// </summary>
        /// <param name="id">Extension ID (Name)</param>
        /// <returns>Download result containing file name and stream. The caller must dispose the stream.</returns>
        public async Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure("Extension ID cannot be null or empty");
                }

                if (_availableExtensions == null || _availableExtensions.Count == 0)
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure("Available extensions list is empty");
                }

                // Find the extension by ID (using Name as ID)
                var extension = _availableExtensions.FirstOrDefault(e =>
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
                        var dependency = _availableExtensions.FirstOrDefault(e =>
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

        /// <summary>
        /// Downloads an extension package asynchronously with progress tracking.
        /// Updates the operation state in the queue throughout the download process.
        /// </summary>
        /// <param name="operation">The update operation containing extension details.</param>
        /// <returns>The local file path of the downloaded package, or null if download failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
        public async Task<string?> DownloadAsync(Download.UpdateOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var descriptor = operation.Extension.Descriptor;

            // Construct download URL from server URL and extension ID
            var downloadUrl = $"{_serverUrl}/Download/{descriptor.Name}";

            try
            {
                _updateQueue.ChangeState(operation.OperationId, GeneralUpdate.Extension.Download.UpdateState.Updating);

                // Determine file format from descriptor or default to .zip
                var format = !string.IsNullOrWhiteSpace(descriptor.DownloadUrl) && descriptor.DownloadUrl!.Contains(".")
                    ? Path.GetExtension(descriptor.DownloadUrl)
                    : ".zip";

                // Create version info for the download manager
                var versionInfo = new VersionInfo
                {
                    Name = $"{descriptor.Name}_{descriptor.Version}",
                    Url = downloadUrl,
                    Hash = descriptor.PackageHash,
                    Version = descriptor.Version,
                    Size = descriptor.PackageSize,
                    Format = format,
                    AuthScheme = _authScheme,
                    AuthToken = _authToken
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
                    _updateQueue.ChangeState(operation.OperationId, GeneralUpdate.Extension.Download.UpdateState.UpdateFailed, "Downloaded file not found");
                    OnDownloadFailed(descriptor.Name, descriptor.DisplayName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _updateQueue.ChangeState(operation.OperationId, GeneralUpdate.Extension.Download.UpdateState.UpdateFailed, ex.Message);
                OnDownloadFailed(descriptor.Name, descriptor.DisplayName);
                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Download failed for extension {descriptor.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// Handles download statistics events and updates progress tracking.
        /// </summary>
        private void OnDownloadProgress(Download.UpdateOperation operation, MultiDownloadStatisticsEventArgs args)
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
        private void OnDownloadCompleted(Download.UpdateOperation operation, MultiDownloadCompletedEventArgs args)
        {
            if (!args.IsComplated)
            {
                _updateQueue.ChangeState(operation.OperationId, GeneralUpdate.Extension.Download.UpdateState.UpdateFailed, "Download completed with errors");
            }
        }

        /// <summary>
        /// Handles download errors and updates the operation state.
        /// </summary>
        private void OnDownloadError(Download.UpdateOperation operation, MultiDownloadErrorEventArgs args)
        {
            _updateQueue.ChangeState(operation.OperationId, GeneralUpdate.Extension.Download.UpdateState.UpdateFailed, args.Exception?.Message);
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
        /// Maps an AvailableExtension to an ExtensionDTO
        /// </summary>
        private ExtensionDTO MapToExtensionDTO(AvailableExtension extension, Version? hostVersion)
        {
            var descriptor = extension.Descriptor;

            // Determine compatibility if host version is provided
            bool? isCompatible = null;
            if (hostVersion != null && _validator != null)
            {
                isCompatible = _validator.IsCompatible(descriptor);
            }

            return new ExtensionDTO
            {
                Id = descriptor.Name ?? string.Empty,
                Name = descriptor.Name,
                DisplayName = descriptor.DisplayName,
                Version = descriptor.Version,
                FileSize = descriptor.PackageSize > 0 ? descriptor.PackageSize : (long?)null,
                UploadTime = descriptor.ReleaseDate,
                Status = true, // Assume enabled if it's in the available list
                Description = descriptor.Description,
                Format = GetFileFormat(descriptor.DownloadUrl),
                Hash = descriptor.PackageHash,
                Publisher = descriptor.Publisher,
                License = descriptor.License,
                Categories = descriptor.Categories,
                SupportedPlatforms = descriptor.SupportedPlatforms,
                MinHostVersion = descriptor.Compatibility?.MinHostVersion?.ToString(),
                MaxHostVersion = descriptor.Compatibility?.MaxHostVersion?.ToString(),
                ReleaseDate = descriptor.ReleaseDate,
                Dependencies = descriptor.Dependencies,
                IsPreRelease = extension.IsPreRelease,
                DownloadUrl = descriptor.DownloadUrl,
                CustomProperties = descriptor.CustomProperties,
                IsCompatible = isCompatible
            };
        }

        /// <summary>
        /// Extracts file format from download URL
        /// </summary>
        private string? GetFileFormat(string? downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                return null;

            try
            {
                var extension = Path.GetExtension(downloadUrl);
                return string.IsNullOrWhiteSpace(extension) ? null : extension;
            }
            catch
            {
                return null;
            }
        }
    }
}
