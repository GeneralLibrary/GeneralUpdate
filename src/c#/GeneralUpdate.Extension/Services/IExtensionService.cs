using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Extension.DTOs;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Interface for extension query and download operations
    /// </summary>
    public interface IExtensionService
    {
        /// <summary>
        /// Occurs when download progress updates during package retrieval.
        /// </summary>
        event EventHandler<EventHandlers.DownloadProgressEventArgs>? ProgressUpdated;

        /// <summary>
        /// Occurs when a download completes successfully.
        /// </summary>
        event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadCompleted;

        /// <summary>
        /// Occurs when a download fails due to an error.
        /// </summary>
        event EventHandler<EventHandlers.ExtensionEventArgs>? DownloadFailed;

        /// <summary>
        /// Updates the list of available extensions
        /// </summary>
        /// <param name="availableExtensions">New list of available extensions</param>
        void UpdateAvailableExtensions(List<Metadata.ExtensionMetadata> availableExtensions);

        /// <summary>
        /// Queries available extensions based on filter criteria
        /// </summary>
        /// <param name="query">Query parameters including pagination and filters</param>
        /// <returns>Paginated result of extensions matching the query</returns>
        Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> Query(ExtensionQueryDTO query);

        /// <summary>
        /// Downloads an extension and its dependencies by ID.
        /// Note: The caller is responsible for disposing the Stream in the returned DownloadExtensionDTO.
        /// </summary>
        /// <param name="id">Extension ID</param>
        /// <returns>Download result containing file name and stream. The caller must dispose the stream.</returns>
        Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id);

        /// <summary>
        /// Downloads an extension by ID with support for resumable downloads.
        /// Note: The caller is responsible for disposing the Stream in the returned DownloadExtensionDTO.
        /// </summary>
        /// <param name="id">Extension ID</param>
        /// <param name="startPosition">Starting byte position for resuming a download (0 for full download)</param>
        /// <returns>Download result containing file name and stream. The caller must dispose the stream.</returns>
        Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id, long startPosition);

        /// <summary>
        /// Downloads an extension package asynchronously with progress tracking.
        /// Updates the operation state in the queue throughout the download process.
        /// </summary>
        /// <param name="operation">The update operation containing extension details.</param>
        /// <returns>The local file path of the downloaded package, or null if download failed.</returns>
        Task<string?> DownloadAsync(Download.UpdateOperation operation);
    }
}
