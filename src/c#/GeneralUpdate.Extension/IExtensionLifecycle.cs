using System;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Defines the lifecycle methods for an extension.
    /// </summary>
    public interface IExtensionLifecycle
    {
        /// <summary>
        /// Called when the extension is installed.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnInstallAsync();

        /// <summary>
        /// Called when the extension is activated or enabled.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnActivateAsync();

        /// <summary>
        /// Called when the extension is deactivated or disabled.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnDeactivateAsync();

        /// <summary>
        /// Called when the extension is uninstalled.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnUninstallAsync();

        /// <summary>
        /// Called when the extension is updated to a new version.
        /// </summary>
        /// <param name="oldVersion">The previous version.</param>
        /// <param name="newVersion">The new version.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnUpdateAsync(string oldVersion, string newVersion);
    }
}
