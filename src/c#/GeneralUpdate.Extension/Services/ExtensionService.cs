using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Extension.DTOs;
using GeneralUpdate.Extension.Metadata;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Implementation of extension query and download operations
    /// </summary>
    public class ExtensionService : IExtensionService
    {
        private List<AvailableExtension> _availableExtensions;
        private readonly Version? _hostVersion;
        private readonly Compatibility.ICompatibilityValidator? _validator;
        private readonly Download.ExtensionDownloadService? _downloadService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionService"/> class.
        /// </summary>
        /// <param name="availableExtensions">List of available extensions</param>
        /// <param name="hostVersion">Optional host version for compatibility checking</param>
        /// <param name="validator">Optional compatibility validator</param>
        /// <param name="downloadService">Optional download service</param>
        public ExtensionService(
            List<AvailableExtension> availableExtensions,
            Version? hostVersion = null,
            Compatibility.ICompatibilityValidator? validator = null,
            Download.ExtensionDownloadService? downloadService = null)
        {
            _availableExtensions = availableExtensions ?? throw new ArgumentNullException(nameof(availableExtensions));
            _hostVersion = hostVersion;
            _validator = validator;
            _downloadService = downloadService;
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
            if (_downloadService == null)
            {
                return HttpResponseDTO<DownloadExtensionDTO>.Failure(
                    "Download service is not configured");
            }

            // Delegate to ExtensionDownloadService which now contains the unified download implementation
            return await _downloadService.Download(id, _availableExtensions);
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
