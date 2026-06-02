using System;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Download;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Default implementation of <see cref="IExtensionServiceFactory"/>.
/// Creates standard instances of all extension services except <see cref="CreateHttpClient"/>,
/// which requires a server URL and must be configured via <see cref="ExtensionHostBuilder"/>.
/// </summary>
public class ExtensionServiceFactory : IExtensionServiceFactory
{
    /// <summary>
    /// Creates an extension HTTP client.
    /// Requires server URL — use <see cref="ExtensionHostBuilder"/> to configure.
    /// </summary>
    public IExtensionHttpClient CreateHttpClient() =>
        throw new NotSupportedException(
            "IExtensionHttpClient requires server URL and authentication configuration. " +
            "Use ExtensionHostBuilder to configure these, or provide a custom implementation.");

    /// <inheritdoc />
    public IVersionCompatibilityChecker CreateCompatibilityChecker() =>
        new VersionCompatibilityChecker();

    /// <inheritdoc />
    public IDownloadQueueManager CreateDownloadQueueManager() =>
        new DownloadQueueManager();

    /// <inheritdoc />
    public IDependencyResolver CreateDependencyResolver(IExtensionCatalog catalog) =>
        new DependencyResolver(catalog ?? throw new ArgumentNullException(nameof(catalog)));

    /// <inheritdoc />
    public IPlatformMatcher CreatePlatformMatcher() =>
        new PlatformMatcher();
}
