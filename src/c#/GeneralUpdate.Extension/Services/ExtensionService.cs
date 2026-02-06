using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        private readonly HttpClient _httpClient;

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
            // Remove trailing slashes for consistent URL construction (only forward slashes expected in URLs)
            _serverUrl = serverUrl.TrimEnd('/');
            _updateQueue = updateQueue ?? throw new ArgumentNullException(nameof(updateQueue));
            _downloadTimeout = downloadTimeout;
            _hostVersion = hostVersion;
            _validator = validator;
            _authScheme = authScheme;
            _authToken = authToken;

            // Initialize HttpClient with timeout
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_downloadTimeout)
            };

            // Set authentication headers if provided
            if (!string.IsNullOrWhiteSpace(_authScheme) && !string.IsNullOrWhiteSpace(_authToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue(_authScheme, _authToken);
            }

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
        /// Queries available extensions based on filter criteria via HTTP request to the server
        /// </summary>
        /// <param name="query">Query parameters including pagination and filters</param>
        /// <returns>Paginated result of extensions matching the query</returns>
        public async Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> Query(ExtensionQueryDTO query)
        {
            try
            {
                if (query == null)
                {
                    return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "Query parameter cannot be null");
                }

                // Validate pagination parameters
                if (query.PageNumber < 1)
                {
                    return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "PageNumber must be greater than 0");
                }

                if (query.PageSize < 1)
                {
                    return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "PageSize must be greater than 0");
                }

                // Build query string from parameters
                var queryParams = new List<string>();
                queryParams.Add($"PageNumber={query.PageNumber}");
                queryParams.Add($"PageSize={query.PageSize}");
                
                if (!string.IsNullOrWhiteSpace(query.Name))
                    queryParams.Add($"Name={Uri.EscapeDataString(query.Name)}");
                
                if (!string.IsNullOrWhiteSpace(query.Publisher))
                    queryParams.Add($"Publisher={Uri.EscapeDataString(query.Publisher)}");
                
                if (!string.IsNullOrWhiteSpace(query.Category))
                    queryParams.Add($"Category={Uri.EscapeDataString(query.Category)}");
                
                if (query.TargetPlatform.HasValue)
                    queryParams.Add($"TargetPlatform={(int)query.TargetPlatform.Value}");
                
                if (!string.IsNullOrWhiteSpace(query.HostVersion))
                    queryParams.Add($"HostVersion={Uri.EscapeDataString(query.HostVersion)}");
                
                queryParams.Add($"IncludePreRelease={query.IncludePreRelease}");
                
                if (!string.IsNullOrWhiteSpace(query.SearchTerm))
                    queryParams.Add($"SearchTerm={Uri.EscapeDataString(query.SearchTerm)}");

                var queryString = string.Join("&", queryParams);
                var requestUrl = $"{_serverUrl}/Query?{queryString}";

                // Make HTTP GET request
                var response = await _httpClient.GetAsync(requestUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        $"Server returned error {response.StatusCode}: {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PagedResultDTO<ExtensionDTO>>(jsonContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Failure(
                        "Failed to deserialize server response");
                }

                // Update local cache with results
                if (result.Items != null && result.Items.Any())
                {
                    var availableExtensions = result.Items
                        .Select(dto => MapFromExtensionDTO(dto))
                        .Where(ext => ext != null)
                        .Cast<AvailableExtension>()
                        .ToList();
                    
                    // Merge with existing extensions
                    foreach (var ext in availableExtensions)
                    {
                        var existing = _availableExtensions.FirstOrDefault(e => 
                            e.Descriptor.Name?.Equals(ext.Descriptor.Name, StringComparison.OrdinalIgnoreCase) == true);
                        
                        if (existing == null)
                        {
                            _availableExtensions.Add(ext);
                        }
                    }
                }

                return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.Success(result);
            }
            catch (HttpRequestException ex)
            {
                return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.InnerException(
                    $"HTTP request error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.InnerException(
                    $"Request timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                return HttpResponseDTO<PagedResultDTO<ExtensionDTO>>.InnerException(
                    $"Error querying extensions: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads an extension package by ID via HTTP GET request to the server.
        /// Note: The caller is responsible for disposing the Stream in the returned DownloadExtensionDTO.
        /// </summary>
        /// <param name="id">Extension ID (Name)</param>
        /// <returns>Download result containing file name and stream. The caller must dispose the stream.</returns>
        public async Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id)
        {
            return await Download(id, 0);
        }

        /// <summary>
        /// Downloads an extension package by ID via HTTP GET request with support for resumable downloads.
        /// Note: The caller is responsible for disposing the Stream in the returned DownloadExtensionDTO.
        /// </summary>
        /// <param name="id">Extension ID (Name)</param>
        /// <param name="startPosition">Starting byte position for resuming a download (0 for full download)</param>
        /// <returns>Download result containing file name and stream. The caller must dispose the stream.</returns>
        public async Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id, long startPosition)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure("Extension ID cannot be null or empty");
                }

                if (startPosition < 0)
                {
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure("Start position cannot be negative");
                }

                // Construct download URL with encoded extension name
                var encodedExtensionName = Uri.EscapeDataString(id);
                var downloadUrl = $"{_serverUrl}/Download/{encodedExtensionName}";

                // Create request message to support Range header
                var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

                // Add Range header if resuming from a specific position
                if (startPosition > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startPosition, null);
                }

                // Make HTTP GET request to download the file
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Check for success status codes (200 for full content, 206 for partial content)
                if (response.StatusCode != System.Net.HttpStatusCode.OK && 
                    response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return HttpResponseDTO<DownloadExtensionDTO>.Failure(
                        $"Server returned error {response.StatusCode}: {errorContent}");
                }

                // Read the file content as stream
                var stream = await response.Content.ReadAsStreamAsync();
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Try to get filename from content-disposition header
                var fileName = $"{id}.zip";
                if (response.Content.Headers.ContentDisposition?.FileName != null)
                {
                    fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
                }
                // URL decode the filename if it was URL encoded
                fileName = System.Net.WebUtility.UrlDecode(fileName);

                var result = new DownloadExtensionDTO
                {
                    FileName = fileName,
                    Stream = memoryStream
                };

                return HttpResponseDTO<DownloadExtensionDTO>.Success(result);
            }
            catch (HttpRequestException ex)
            {
                return HttpResponseDTO<DownloadExtensionDTO>.InnerException(
                    $"HTTP request error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                return HttpResponseDTO<DownloadExtensionDTO>.InnerException(
                    $"Request timeout: {ex.Message}");
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

            // Construct download URL from server URL and extension ID (URL-encoded for safety)
            var encodedExtensionName = Uri.EscapeDataString(descriptor.Name);
            var downloadUrl = $"{_serverUrl}/Download/{encodedExtensionName}";

            try
            {
                _updateQueue.ChangeState(operation.OperationId, GeneralUpdate.Extension.Download.UpdateState.Updating);

                // Default to .zip format
                var format = ".zip";

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

            // Construct download URL from server URL (URL-encoded for safety)
            var encodedExtensionName = Uri.EscapeDataString(descriptor.Name ?? string.Empty);
            var downloadUrl = $"{_serverUrl}/Download/{encodedExtensionName}";

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
                Format = ".zip", // Default format
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
                DownloadUrl = downloadUrl, // Use constructed URL from server
                CustomProperties = descriptor.CustomProperties,
                IsCompatible = isCompatible
            };
        }

        /// <summary>
        /// Maps an ExtensionDTO to an AvailableExtension
        /// </summary>
        private AvailableExtension? MapFromExtensionDTO(ExtensionDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                return null;

            Version? minVersion = null;
            Version? maxVersion = null;

            if (!string.IsNullOrWhiteSpace(dto.MinHostVersion))
                Version.TryParse(dto.MinHostVersion, out minVersion);

            if (!string.IsNullOrWhiteSpace(dto.MaxHostVersion))
                Version.TryParse(dto.MaxHostVersion, out maxVersion);

            var descriptor = new ExtensionDescriptor
            {
                Name = dto.Name,
                DisplayName = dto.DisplayName ?? dto.Name,
                Version = dto.Version ?? "1.0.0",
                Description = dto.Description,
                Publisher = dto.Publisher,
                License = dto.License,
                Categories = dto.Categories,
                SupportedPlatforms = dto.SupportedPlatforms,
                Compatibility = new VersionCompatibility
                {
                    MinHostVersion = minVersion,
                    MaxHostVersion = maxVersion
                },
                DownloadUrl = dto.DownloadUrl,
                PackageHash = dto.Hash,
                PackageSize = dto.FileSize ?? 0,
                ReleaseDate = dto.ReleaseDate,
                Dependencies = dto.Dependencies,
                CustomProperties = dto.CustomProperties
            };

            return new AvailableExtension
            {
                Descriptor = descriptor,
                IsPreRelease = dto.IsPreRelease
            };
        }
    }
}
