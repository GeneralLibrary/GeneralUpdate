using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Core
{
    /// <summary>
    /// Defines the contract for managing extension catalogs (local and remote).
    /// </summary>
    public interface IExtensionCatalog
    {
        /// <summary>
        /// Loads all locally installed extensions from the file system.
        /// </summary>
        void LoadInstalledExtensions();

        /// <summary>
        /// Gets all locally installed extensions.
        /// </summary>
        /// <returns>A list of installed extensions.</returns>
        List<Installation.InstalledExtension> GetInstalledExtensions();

        /// <summary>
        /// Gets installed extensions filtered by target platform.
        /// </summary>
        /// <param name="platform">The platform to filter by.</param>
        /// <returns>A list of installed extensions compatible with the specified platform.</returns>
        List<Installation.InstalledExtension> GetInstalledExtensionsByPlatform(Metadata.TargetPlatform platform);

        /// <summary>
        /// Gets an installed extension by its unique identifier.
        /// </summary>
        /// <param name="extensionId">The extension identifier.</param>
        /// <returns>The installed extension if found; otherwise, null.</returns>
        Installation.InstalledExtension? GetInstalledExtensionById(string extensionId);

        /// <summary>
        /// Adds or updates an installed extension in the catalog.
        /// </summary>
        /// <param name="extension">The extension to add or update.</param>
        void AddOrUpdateInstalledExtension(Installation.InstalledExtension extension);

        /// <summary>
        /// Removes an installed extension from the catalog.
        /// </summary>
        /// <param name="extensionId">The identifier of the extension to remove.</param>
        void RemoveInstalledExtension(string extensionId);

        /// <summary>
        /// Filters available extensions by target platform.
        /// </summary>
        /// <param name="extensions">The list of extensions to filter.</param>
        /// <param name="platform">The platform to filter by.</param>
        /// <returns>A filtered list of extensions compatible with the specified platform.</returns>
        List<Metadata.ExtensionMetadata> FilterByPlatform(List<Metadata.ExtensionMetadata> extensions, Metadata.TargetPlatform platform);
    }
}
