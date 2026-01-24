using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Checks version compatibility between client and extensions.
    /// </summary>
    public class VersionCompatibilityChecker
    {
        private readonly Version _clientVersion;

        /// <summary>
        /// Initializes a new instance of the VersionCompatibilityChecker.
        /// </summary>
        /// <param name="clientVersion">The current client version.</param>
        public VersionCompatibilityChecker(Version clientVersion)
        {
            _clientVersion = clientVersion ?? throw new ArgumentNullException(nameof(clientVersion));
        }

        /// <summary>
        /// Checks if an extension is compatible with the client version.
        /// </summary>
        /// <param name="metadata">Extension metadata to check.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsCompatible(ExtensionMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            return metadata.Compatibility.IsCompatible(_clientVersion);
        }

        /// <summary>
        /// Filters a list of remote extensions to only include compatible ones.
        /// </summary>
        /// <param name="extensions">List of remote extensions.</param>
        /// <returns>List of compatible extensions.</returns>
        public List<RemoteExtension> FilterCompatibleExtensions(List<RemoteExtension> extensions)
        {
            if (extensions == null)
                return new List<RemoteExtension>();

            return extensions
                .Where(ext => IsCompatible(ext.Metadata))
                .ToList();
        }

        /// <summary>
        /// Finds the latest compatible version of an extension from a list of versions.
        /// </summary>
        /// <param name="extensions">List of extension versions (same extension ID, different versions).</param>
        /// <returns>The latest compatible version or null if none are compatible.</returns>
        public RemoteExtension? FindLatestCompatibleVersion(List<RemoteExtension> extensions)
        {
            if (extensions == null || !extensions.Any())
                return null;

            return extensions
                .Where(ext => IsCompatible(ext.Metadata))
                .OrderByDescending(ext => ext.Metadata.GetVersion())
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the minimum supported extension version among the latest compatible versions.
        /// This is useful when the client requests an upgrade and needs the minimum version
        /// that still works with the current client version.
        /// </summary>
        /// <param name="extensions">List of extension versions.</param>
        /// <returns>The minimum compatible version among the latest versions, or null if none are compatible.</returns>
        public RemoteExtension? FindMinimumSupportedLatestVersion(List<RemoteExtension> extensions)
        {
            if (extensions == null || !extensions.Any())
                return null;

            // First, filter to only compatible extensions
            var compatibleExtensions = extensions
                .Where(ext => IsCompatible(ext.Metadata))
                .ToList();

            if (!compatibleExtensions.Any())
                return null;

            // Find the maximum version among all compatible extensions
            var maxVersion = compatibleExtensions
                .Select(ext => ext.Metadata.GetVersion())
                .Where(v => v != null)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (maxVersion == null)
                return null;

            // Return the extension with that maximum version
            return compatibleExtensions
                .FirstOrDefault(ext => ext.Metadata.GetVersion() == maxVersion);
        }

        /// <summary>
        /// Checks if an update is available and compatible for a local extension.
        /// </summary>
        /// <param name="localExtension">The local extension.</param>
        /// <param name="remoteVersions">Available remote versions of the extension.</param>
        /// <returns>The compatible update if available, or null if none.</returns>
        public RemoteExtension? GetCompatibleUpdate(LocalExtension localExtension, List<RemoteExtension> remoteVersions)
        {
            if (localExtension == null || remoteVersions == null || !remoteVersions.Any())
                return null;

            var localVersion = localExtension.Metadata.GetVersion();
            if (localVersion == null)
                return null;

            // Find the latest compatible version that is newer than the local version
            return remoteVersions
                .Where(ext => IsCompatible(ext.Metadata))
                .Where(ext =>
                {
                    var remoteVersion = ext.Metadata.GetVersion();
                    return remoteVersion != null && remoteVersion > localVersion;
                })
                .OrderByDescending(ext => ext.Metadata.GetVersion())
                .FirstOrDefault();
        }
    }
}
