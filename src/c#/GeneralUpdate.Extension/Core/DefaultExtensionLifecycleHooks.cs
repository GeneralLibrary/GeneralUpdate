using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Default no-op implementation of lifecycle hooks.
/// Extend this class and override only the hooks you need.
/// </summary>
public class DefaultExtensionLifecycleHooks : IExtensionLifecycleHooks
{
    /// <inheritdoc/>
    public virtual Task<bool> OnBeforeInstallAsync(ExtensionMetadata extension, string? packagePath, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public virtual Task OnAfterInstallAsync(ExtensionMetadata extension, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnBeforeActivateAsync(string extensionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnAfterActivateAsync(string extensionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnBeforeDeactivateAsync(string extensionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnAfterDeactivateAsync(string extensionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task<bool> OnBeforeUninstallAsync(ExtensionMetadata extension, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public virtual Task OnAfterUninstallAsync(string extensionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
