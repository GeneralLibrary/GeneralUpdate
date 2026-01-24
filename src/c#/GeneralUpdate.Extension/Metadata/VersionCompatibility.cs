using System;

namespace GeneralUpdate.Extension.Metadata
{
    /// <summary>
    /// Defines version compatibility constraints between the host application and an extension.
    /// Ensures extensions only run on compatible host versions to prevent runtime errors.
    /// </summary>
    public class VersionCompatibility
    {
        /// <summary>
        /// Gets or sets the minimum host application version required by this extension.
        /// Null indicates no minimum version constraint.
        /// </summary>
        public Version? MinHostVersion { get; set; }

        /// <summary>
        /// Gets or sets the maximum host application version supported by this extension.
        /// Null indicates no maximum version constraint.
        /// </summary>
        public Version? MaxHostVersion { get; set; }

        /// <summary>
        /// Determines whether the extension is compatible with the specified host version.
        /// </summary>
        /// <param name="hostVersion">The host application version to validate against.</param>
        /// <returns>True if the extension is compatible with the host version; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostVersion"/> is null.</exception>
        public bool IsCompatibleWith(Version hostVersion)
        {
            if (hostVersion == null)
                throw new ArgumentNullException(nameof(hostVersion));

            bool meetsMinimum = MinHostVersion == null || hostVersion >= MinHostVersion;
            bool meetsMaximum = MaxHostVersion == null || hostVersion <= MaxHostVersion;

            return meetsMinimum && meetsMaximum;
        }
    }
}
