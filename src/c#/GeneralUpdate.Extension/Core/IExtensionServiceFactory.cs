using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Download;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Factory for creating extension service instances
/// </summary>
public interface IExtensionServiceFactory
{
    /// <summary>
    /// Creates an extension HTTP client
    /// </summary>
    IExtensionHttpClient CreateHttpClient();

    /// <summary>
    /// Creates a version compatibility checker
    /// </summary>
    IVersionCompatibilityChecker CreateCompatibilityChecker();

    /// <summary>
    /// Creates a download queue manager
    /// </summary>
    IDownloadQueueManager CreateDownloadQueueManager();

    /// <summary>
    /// Creates a dependency resolver
    /// </summary>
    IDependencyResolver CreateDependencyResolver(IExtensionCatalog catalog);

    /// <summary>
    /// Creates a platform matcher
    /// </summary>
    IPlatformMatcher CreatePlatformMatcher();
}
