namespace GeneralUpdate.Extension.Metadata
{
    /// <summary>
    /// Represents an extension available from the remote marketplace or update server.
    /// </summary>
    public class AvailableExtension
    {
        /// <summary>
        /// Gets or sets the extension metadata descriptor.
        /// </summary>
        public ExtensionDescriptor Descriptor { get; set; } = new ExtensionDescriptor();

        /// <summary>
        /// Gets or sets a value indicating whether this is a pre-release version.
        /// Pre-release versions are typically beta or alpha builds.
        /// </summary>
        public bool IsPreRelease { get; set; }

        /// <summary>
        /// Gets or sets the total number of times this extension has been downloaded.
        /// Null if download statistics are not available.
        /// </summary>
        public long? DownloadCount { get; set; }
    }
}
