using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides methods for managing extensions, including installation, uninstallation, and updates.
    /// </summary>
    public interface IExtensionManager
    {
        /// <summary>
        /// Installs an extension from a package file.
        /// </summary>
        /// <param name="packagePath">The path to the extension package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> InstallAsync(string packagePath);

        /// <summary>
        /// Uninstalls an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to uninstall.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> UninstallAsync(string extensionId);

        /// <summary>
        /// Enables an installed extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to enable.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> EnableAsync(string extensionId);

        /// <summary>
        /// Disables an enabled extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to disable.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> DisableAsync(string extensionId);

        /// <summary>
        /// Updates an extension to a new version.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to update.</param>
        /// <param name="targetVersion">The target version to update to.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> UpdateAsync(string extensionId, string targetVersion);

        /// <summary>
        /// Rolls back an extension to a previous version.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to roll back.</param>
        /// <param name="targetVersion">The target version to roll back to.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> RollbackAsync(string extensionId, string targetVersion);

        /// <summary>
        /// Gets all installed extensions.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation, containing a list of installed extension manifests.</returns>
        Task<List<ExtensionManifest>> GetInstalledExtensionsAsync();

        /// <summary>
        /// Gets the current state of an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing the extension state.</returns>
        Task<ExtensionState> GetExtensionStateAsync(string extensionId);
    }
}
