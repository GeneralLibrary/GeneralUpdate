using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;

namespace GeneralUpdate.Extension.Dependencies;

/// <summary>
/// Dependency resolver for extensions
/// </summary>
public class DependencyResolver : IDependencyResolver
{
    private readonly IExtensionCatalog _catalog;

    /// <summary>
    /// Initialize dependency resolver
    /// </summary>
    /// <param name="catalog">Extension catalog</param>
    public DependencyResolver(IExtensionCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public List<string> ResolveDependencies(ExtensionMetadata extension)
    {
        var resolved = new List<string>();
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        ResolveDependenciesRecursive(extension.Id, resolved, visiting, visited);

        return resolved;
    }

    /// <inheritdoc/>
    public List<string> GetMissingDependencies(ExtensionMetadata extension)
    {
        var allDependencies = ResolveDependencies(extension);
        var missing = new List<string>();

        foreach (var depId in allDependencies)
        {
            var installed = _catalog.GetInstalledExtensionById(depId);
            if (installed == null)
            {
                missing.Add(depId);
            }
        }

        return missing;
    }

    private void ResolveDependenciesRecursive(
        string extensionId,
        List<string> resolved,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(extensionId))
        {
            return;
        }

        if (visiting.Contains(extensionId))
        {
            throw new InvalidOperationException($"Circular dependency detected: {extensionId}");
        }

        visiting.Add(extensionId);

        var extension = _catalog.GetInstalledExtensionById(extensionId);
        if (extension?.Dependencies != null && !string.IsNullOrWhiteSpace(extension.Dependencies))
        {
            var dependencies = extension.Dependencies.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var dep in dependencies)
            {
                var depId = dep.Trim();
                ResolveDependenciesRecursive(depId, resolved, visiting, visited);
            }
        }

        visiting.Remove(extensionId);
        visited.Add(extensionId);

        if (!resolved.Contains(extensionId))
        {
            resolved.Add(extensionId);
        }
    }
}
