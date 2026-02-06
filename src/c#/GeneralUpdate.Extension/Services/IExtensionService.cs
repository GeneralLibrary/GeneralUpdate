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
        /// Updates the list of available extensions
        /// </summary>
        /// <param name="availableExtensions">New list of available extensions</param>
        void UpdateAvailableExtensions(List<Metadata.AvailableExtension> availableExtensions);

        /// <summary>
        /// Queries available extensions based on filter criteria
        /// </summary>
        /// <param name="query">Query parameters including pagination and filters</param>
        /// <returns>Paginated result of extensions matching the query</returns>
        Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> Query(ExtensionQueryDTO query);

        /// <summary>
        /// Downloads an extension and its dependencies by ID
        /// </summary>
        /// <param name="id">Extension ID</param>
        /// <returns>Download result containing file name and stream</returns>
        Task<HttpResponseDTO<DownloadExtensionDTO>> Download(string id);
    }
}
