using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides methods for querying and retrieving extensions from a repository.
    /// </summary>
    public interface IExtensionRepository
    {
        /// <summary>
        /// Searches for extensions matching the specified query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <returns>A task that represents the asynchronous operation, containing a list of matching extension manifests.</returns>
        Task<List<ExtensionManifest>> SearchAsync(string query);

        /// <summary>
        /// Gets the details of a specific extension by its identifier.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing the extension manifest.</returns>
        Task<ExtensionManifest> GetExtensionAsync(string extensionId);

        /// <summary>
        /// Gets the list of available versions for a specific extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing a list of version strings.</returns>
        Task<List<string>> GetVersionsAsync(string extensionId);

        /// <summary>
        /// Downloads an extension package.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="version">The version to download.</param>
        /// <param name="destinationPath">The path where the package should be saved.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> DownloadPackageAsync(string extensionId, string version, string destinationPath, IProgress<double> progress = null);

        /// <summary>
        /// Validates the metadata of an extension package.
        /// </summary>
        /// <param name="packagePath">The path to the extension package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the metadata is valid.</returns>
        Task<bool> ValidateMetadataAsync(string packagePath);

        /// <summary>
        /// Gets all extensions in a specific category.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>A task that represents the asynchronous operation, containing a list of extension manifests.</returns>
        Task<List<ExtensionManifest>> GetExtensionsByCategoryAsync(string category);

        /// <summary>
        /// Gets the most popular extensions.
        /// </summary>
        /// <param name="count">The number of extensions to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation, containing a list of extension manifests.</returns>
        Task<List<ExtensionManifest>> GetPopularExtensionsAsync(int count);
    }
}
