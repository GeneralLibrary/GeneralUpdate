using System;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides methods for loading and managing extensions.
    /// </summary>
    public interface IExtensionLoader
    {
        /// <summary>
        /// Loads an extension from the specified path.
        /// </summary>
        /// <param name="extensionPath">The path to the extension package.</param>
        /// <returns>A task that represents the asynchronous operation, containing the loaded extension manifest.</returns>
        Task<ExtensionManifest> LoadAsync(string extensionPath);

        /// <summary>
        /// Unloads a previously loaded extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to unload.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> UnloadAsync(string extensionId);

        /// <summary>
        /// Activates a loaded extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to activate.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> ActivateAsync(string extensionId);

        /// <summary>
        /// Deactivates an active extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to deactivate.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> DeactivateAsync(string extensionId);

        /// <summary>
        /// Gets a value indicating whether an extension is currently loaded.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>True if the extension is loaded; otherwise, false.</returns>
        bool IsLoaded(string extensionId);

        /// <summary>
        /// Gets a value indicating whether an extension is currently active.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>True if the extension is active; otherwise, false.</returns>
        bool IsActive(string extensionId);
    }
}
