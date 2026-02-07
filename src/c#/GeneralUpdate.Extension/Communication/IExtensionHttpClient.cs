using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Communication;

/// <summary>
/// Interface for extension HTTP client operations
/// </summary>
public interface IExtensionHttpClient
{
    /// <summary>
    /// Query extensions from server
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response with paged results</returns>
    Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryExtensionsAsync(
        ExtensionQueryDTO query, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download extension by ID
    /// </summary>
    /// <param name="extensionId">Extension ID</param>
    /// <param name="savePath">Path to save file</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if download succeeded</returns>
    Task<bool> DownloadExtensionAsync(
        string extensionId,
        string savePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
