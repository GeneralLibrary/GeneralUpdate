using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Main interface for extension host
/// </summary>
public interface IExtensionHost
{
    /// <summary>
    /// Get the extension catalog
    /// </summary>
    IExtensionCatalog ExtensionCatalog { get; }

    /// <summary>
    /// Query extensions from server
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <returns>Paged result of extensions</returns>
    Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryExtensionsAsync(ExtensionQueryDTO query);

    /// <summary>
    /// Download an extension by ID
    /// </summary>
    /// <param name="extensionId">Extension ID to download</param>
    /// <param name="savePath">Path to save the extension</param>
    /// <returns>True if download succeeded</returns>
    Task<bool> DownloadExtensionAsync(string extensionId, string savePath);

    /// <summary>
    /// Update an extension
    /// </summary>
    /// <param name="extensionId">Extension ID to update</param>
    /// <returns>True if update succeeded</returns>
    Task<bool> UpdateExtensionAsync(string extensionId);

    /// <summary>
    /// Install an extension
    /// </summary>
    /// <param name="extensionPath">Path to extension file</param>
    /// <param name="rollbackOnFailure">Whether to rollback on failure</param>
    /// <returns>True if installation succeeded</returns>
    Task<bool> InstallExtensionAsync(string extensionPath, bool rollbackOnFailure = true);

    /// <summary>
    /// Check if an extension is compatible with the host version
    /// </summary>
    /// <param name="extension">Extension to check</param>
    /// <returns>True if compatible</returns>
    bool IsExtensionCompatible(ExtensionMetadata extension);

    /// <summary>
    /// Set auto-update for an extension
    /// </summary>
    /// <param name="extensionId">Extension ID</param>
    /// <param name="autoUpdate">Enable/disable auto-update</param>
    void SetAutoUpdate(string extensionId, bool autoUpdate);

    /// <summary>
    /// Set global auto-update setting
    /// </summary>
    /// <param name="enabled">Enable/disable global auto-update</param>
    void SetGlobalAutoUpdate(bool enabled);

    /// <summary>
    /// Event fired when extension update status changes
    /// </summary>
    event EventHandler<ExtensionUpdateEventArgs>? ExtensionUpdateStatusChanged;
}

/// <summary>
/// Event arguments for extension update status changes
/// </summary>
public class ExtensionUpdateEventArgs : EventArgs
{
    /// <summary>
    /// Extension ID
    /// </summary>
    public string ExtensionId { get; set; } = string.Empty;

    /// <summary>
    /// Extension name
    /// </summary>
    public string? ExtensionName { get; set; }

    /// <summary>
    /// Update status
    /// </summary>
    public ExtensionUpdateStatus Status { get; set; }

    /// <summary>
    /// Error message if status is UpdateFailed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }
}
