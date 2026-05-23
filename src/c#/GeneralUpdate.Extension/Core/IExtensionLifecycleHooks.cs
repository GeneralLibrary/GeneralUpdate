using GeneralUpdate.Extension.Common.Models;

using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Provides lifecycle hooks for extension installation, activation, deactivation, and uninstallation.
/// Implement this interface to run custom logic during extension lifecycle events.
/// </summary>
public interface IExtensionLifecycleHooks
{
    /// <summary>
    /// Called before an extension is installed. Return false to cancel the installation.
    /// </summary>
    /// <param name="extension">Extension metadata.</param>
    /// <param name="packagePath">Path to the downloaded extension package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if installation should proceed; false to cancel.</returns>
    Task<bool> OnBeforeInstallAsync(ExtensionMetadata extension, string? packagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an extension has been successfully installed.
    /// </summary>
    /// <param name="extension">Extension metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnAfterInstallAsync(ExtensionMetadata extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before an extension is activated (loaded into the host).
    /// </summary>
    /// <param name="extensionId">Extension identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnBeforeActivateAsync(string extensionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an extension has been activated.
    /// </summary>
    /// <param name="extensionId">Extension identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnAfterActivateAsync(string extensionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before an extension is deactivated (unloaded from the host).
    /// </summary>
    /// <param name="extensionId">Extension identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnBeforeDeactivateAsync(string extensionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an extension has been deactivated.
    /// </summary>
    /// <param name="extensionId">Extension identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnAfterDeactivateAsync(string extensionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before an extension is uninstalled.
    /// </summary>
    /// <param name="extension">Extension metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if uninstallation should proceed; false to cancel.</returns>
    Task<bool> OnBeforeUninstallAsync(ExtensionMetadata extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an extension has been successfully uninstalled.
    /// </summary>
    /// <param name="extensionId">Extension identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnAfterUninstallAsync(string extensionId, CancellationToken cancellationToken = default);
}
