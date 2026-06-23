using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Catalog;

/// <summary>
/// Interface for managing installed extensions catalog
/// </summary>
public interface IExtensionCatalog
{
    /// <summary>
    /// Load installed extensions from storage
    /// </summary>
    void LoadInstalledExtensions();

    /// <summary>
    /// Get all installed extensions
    /// </summary>
    /// <returns>List of installed extensions</returns>
    List<ExtensionMetadata> GetInstalledExtensions();

    /// <summary>
    /// Get installed extensions filtered by platform
    /// </summary>
    /// <param name="platform">The platform to filter by</param>
    /// <returns>List of installed extensions for the platform</returns>
    List<ExtensionMetadata> GetInstalledExtensionsByPlatform(TargetPlatform platform);

    /// <summary>
    /// Get installed extension by ID
    /// </summary>
    /// <param name="extensionId">The extension identifier</param>
    /// <returns>Extension metadata or null if not found</returns>
    ExtensionMetadata? GetInstalledExtensionById(string extensionId);

    /// <summary>
    /// Add or update an installed extension
    /// </summary>
    /// <param name="extension">The extension to add or update</param>
    void AddOrUpdateInstalledExtension(ExtensionMetadata extension);

    /// <summary>
    /// Remove an installed extension
    /// </summary>
    /// <param name="extensionId">The identifier of the extension to remove</param>
    void RemoveInstalledExtension(string extensionId);
}
