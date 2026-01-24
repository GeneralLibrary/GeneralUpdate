using System;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents a locally installed extension.
    /// </summary>
    public class LocalExtension
    {
        /// <summary>
        /// Metadata of the extension.
        /// </summary>
        public ExtensionMetadata Metadata { get; set; } = new ExtensionMetadata();

        /// <summary>
        /// Local installation path of the extension.
        /// </summary>
        public string InstallPath { get; set; } = string.Empty;

        /// <summary>
        /// Date when the extension was installed.
        /// </summary>
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// Whether auto-update is enabled for this extension.
        /// </summary>
        public bool AutoUpdateEnabled { get; set; } = true;

        /// <summary>
        /// Whether the extension is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Date when the extension was last updated.
        /// </summary>
        public DateTime? LastUpdateDate { get; set; }
    }
}
