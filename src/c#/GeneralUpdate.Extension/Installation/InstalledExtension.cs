using System;

namespace GeneralUpdate.Extension.Installation
{
    /// <summary>
    /// Represents a locally installed extension with its installation state and configuration.
    /// </summary>
    public class InstalledExtension
    {
        /// <summary>
        /// Gets or sets the extension metadata descriptor.
        /// </summary>
        public Metadata.ExtensionDescriptor Descriptor { get; set; } = new Metadata.ExtensionDescriptor();

        /// <summary>
        /// Gets or sets the local file system path where the extension is installed.
        /// </summary>
        public string InstallPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time when the extension was first installed.
        /// </summary>
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic updates are enabled for this extension.
        /// </summary>
        public bool AutoUpdateEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the extension is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the date and time of the most recent update.
        /// Null if the extension has never been updated.
        /// </summary>
        public DateTime? LastUpdateDate { get; set; }
    }
}
