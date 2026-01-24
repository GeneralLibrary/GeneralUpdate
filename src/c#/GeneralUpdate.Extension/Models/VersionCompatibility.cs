using System;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents version compatibility information between client and extension.
    /// </summary>
    public class VersionCompatibility
    {
        /// <summary>
        /// Minimum client version required for this extension.
        /// </summary>
        public Version? MinClientVersion { get; set; }

        /// <summary>
        /// Maximum client version supported by this extension.
        /// </summary>
        public Version? MaxClientVersion { get; set; }

        /// <summary>
        /// Checks if a given client version is compatible with this extension.
        /// </summary>
        /// <param name="clientVersion">The client version to check.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsCompatible(Version clientVersion)
        {
            if (clientVersion == null)
                throw new ArgumentNullException(nameof(clientVersion));

            bool meetsMinimum = MinClientVersion == null || clientVersion >= MinClientVersion;
            bool meetsMaximum = MaxClientVersion == null || clientVersion <= MaxClientVersion;

            return meetsMinimum && meetsMaximum;
        }
    }
}
