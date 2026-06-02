using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Dependencies;

/// <summary>
/// Interface for dependency resolution
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// Resolve all dependencies for an extension
    /// </summary>
    /// <param name="extension">Extension metadata</param>
    /// <returns>List of dependency IDs in installation order</returns>
    List<string> ResolveDependencies(ExtensionMetadata extension);

    /// <summary>
    /// Get missing dependencies for an extension
    /// </summary>
    /// <param name="extension">Extension metadata</param>
    /// <returns>List of missing dependency IDs</returns>
    List<string> GetMissingDependencies(ExtensionMetadata extension);

    /// <summary>
    /// Resolves transitive dependencies from a raw list of dependency IDs.
    /// Handles both installed extensions (full transitive resolution) and
    /// uninstalled ones (simple ID pass-through).
    /// </summary>
    /// <param name="dependencyIds">List of dependency extension IDs</param>
    /// <returns>Dependency IDs in topological installation order</returns>
    List<string> GetTransitiveDependencies(List<string> dependencyIds);
}
