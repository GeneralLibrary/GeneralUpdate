using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Extension.Compatibility
{
    /// <summary>
    /// Defines the contract for checking version compatibility between the host and extensions.
    /// </summary>
    public interface ICompatibilityValidator
    {
        /// <summary>
        /// Checks if an extension descriptor is compatible with the host version.
        /// </summary>
        /// <param name="descriptor">The extension descriptor to validate.</param>
        /// <returns>True if compatible; otherwise, false.</returns>
        bool IsCompatible(Metadata.ExtensionDescriptor descriptor);

        /// <summary>
        /// Filters a list of available extensions to only include compatible ones.
        /// </summary>
        /// <param name="extensions">The list of extensions to filter.</param>
        /// <returns>A filtered list containing only compatible extensions.</returns>
        List<Metadata.AvailableExtension> FilterCompatible(List<Metadata.AvailableExtension> extensions);

        /// <summary>
        /// Finds the latest compatible version of an extension from a list of versions.
        /// </summary>
        /// <param name="extensions">List of extension versions to evaluate.</param>
        /// <returns>The latest compatible version if found; otherwise, null.</returns>
        Metadata.AvailableExtension? FindLatestCompatible(List<Metadata.AvailableExtension> extensions);

        /// <summary>
        /// Finds the minimum supported version among the latest compatible versions.
        /// Used for upgrade request matching.
        /// </summary>
        /// <param name="extensions">List of extension versions to evaluate.</param>
        /// <returns>The minimum supported latest version if found; otherwise, null.</returns>
        Metadata.AvailableExtension? FindMinimumSupportedLatest(List<Metadata.AvailableExtension> extensions);

        /// <summary>
        /// Checks if an update is available for an installed extension.
        /// </summary>
        /// <param name="installed">The currently installed extension.</param>
        /// <param name="availableVersions">Available versions of the extension.</param>
        /// <returns>A compatible update if available; otherwise, null.</returns>
        Metadata.AvailableExtension? GetCompatibleUpdate(Installation.InstalledExtension installed, List<Metadata.AvailableExtension> availableVersions);
    }
}
