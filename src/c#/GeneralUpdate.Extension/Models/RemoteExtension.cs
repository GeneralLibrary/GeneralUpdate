namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents an extension available on the server.
    /// </summary>
    public class RemoteExtension
    {
        /// <summary>
        /// Metadata of the extension.
        /// </summary>
        public ExtensionMetadata Metadata { get; set; } = new ExtensionMetadata();

        /// <summary>
        /// Whether this is a pre-release version.
        /// </summary>
        public bool IsPreRelease { get; set; }

        /// <summary>
        /// Minimum rating or popularity score (optional).
        /// </summary>
        public double? Rating { get; set; }

        /// <summary>
        /// Number of downloads (optional).
        /// </summary>
        public long? DownloadCount { get; set; }
    }
}
