using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Extension.Compatibility
{
    /// <summary>
    /// Validates version compatibility between the host application and extensions.
    /// Ensures extensions only run on supported host versions.
    /// </summary>
    public class CompatibilityValidator : ICompatibilityValidator
    {
        private readonly Version _hostVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompatibilityValidator"/> class.
        /// </summary>
        /// <param name="hostVersion">The current version of the host application.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostVersion"/> is null.</exception>
        public CompatibilityValidator(Version hostVersion)
        {
            _hostVersion = hostVersion ?? throw new ArgumentNullException(nameof(hostVersion));
        }

        /// <summary>
        /// Checks if an extension descriptor meets the host version requirements.
        /// Evaluates both minimum and maximum version constraints.
        /// </summary>
        /// <param name="descriptor">The extension descriptor to validate.</param>
        /// <returns>True if the extension is compatible with the host version; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is null.</exception>
        public bool IsCompatible(Metadata.ExtensionDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            return descriptor.Compatibility.IsCompatibleWith(_hostVersion);
        }

        /// <summary>
        /// Filters a collection of available extensions to only include compatible versions.
        /// Extensions not meeting the host version requirements are excluded.
        /// </summary>
        /// <param name="extensions">The list of extensions to filter.</param>
        /// <returns>A filtered list containing only compatible extensions.</returns>
        public List<Metadata.AvailableExtension> FilterCompatible(List<Metadata.AvailableExtension> extensions)
        {
            if (extensions == null)
                return new List<Metadata.AvailableExtension>();

            return extensions
                .Where(ext => IsCompatible(ext.Descriptor))
                .ToList();
        }

        /// <summary>
        /// Finds the latest compatible version from a list of extension versions.
        /// Useful when multiple versions of the same extension are available.
        /// </summary>
        /// <param name="extensions">List of extension versions to evaluate.</param>
        /// <returns>The latest compatible version if found; otherwise, null.</returns>
        public Metadata.AvailableExtension? FindLatestCompatible(List<Metadata.AvailableExtension> extensions)
        {
            if (extensions == null || !extensions.Any())
                return null;

            return extensions
                .Where(ext => IsCompatible(ext.Descriptor))
                .OrderByDescending(ext => ext.Descriptor.GetVersionObject())
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the minimum supported version among the latest compatible versions.
        /// This is used for upgrade matching when the host requests a compatible update.
        /// </summary>
        /// <param name="extensions">List of extension versions to evaluate.</param>
        /// <returns>The minimum supported latest version if found; otherwise, null.</returns>
        public Metadata.AvailableExtension? FindMinimumSupportedLatest(List<Metadata.AvailableExtension> extensions)
        {
            if (extensions == null || !extensions.Any())
                return null;

            // First, filter to only compatible extensions
            var compatibleExtensions = extensions
                .Where(ext => IsCompatible(ext.Descriptor))
                .ToList();

            if (!compatibleExtensions.Any())
                return null;

            // Find the maximum version among all compatible extensions
            var maxVersion = compatibleExtensions
                .Select(ext => ext.Descriptor.GetVersionObject())
                .Where(v => v != null)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (maxVersion == null)
                return null;

            // Return the extension with that maximum version
            return compatibleExtensions
                .FirstOrDefault(ext => ext.Descriptor.GetVersionObject() == maxVersion);
        }

        /// <summary>
        /// Determines if a compatible update is available for an installed extension.
        /// Only considers versions newer than the currently installed version.
        /// </summary>
        /// <param name="installed">The currently installed extension.</param>
        /// <param name="availableVersions">Available versions of the extension from the remote source.</param>
        /// <returns>The latest compatible update if available; otherwise, null.</returns>
        public Metadata.AvailableExtension? GetCompatibleUpdate(Installation.InstalledExtension installed, List<Metadata.AvailableExtension> availableVersions)
        {
            if (installed == null || availableVersions == null || !availableVersions.Any())
                return null;

            var installedVersion = installed.Descriptor.GetVersionObject();
            if (installedVersion == null)
                return null;

            // Find the latest compatible version that is newer than the installed version
            return availableVersions
                .Where(ext => IsCompatible(ext.Descriptor))
                .Where(ext =>
                {
                    var availableVersion = ext.Descriptor.GetVersionObject();
                    return availableVersion != null && availableVersion > installedVersion;
                })
                .OrderByDescending(ext => ext.Descriptor.GetVersionObject())
                .FirstOrDefault();
        }
    }
}
