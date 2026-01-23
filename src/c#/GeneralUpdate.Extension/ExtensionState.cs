namespace MyApp.Extensions
{
    /// <summary>
    /// Represents the current state of an extension in the system.
    /// </summary>
    public enum ExtensionState
    {
        /// <summary>
        /// The extension is installed but not yet enabled.
        /// </summary>
        Installed,

        /// <summary>
        /// The extension is installed and currently enabled.
        /// </summary>
        Enabled,

        /// <summary>
        /// The extension is installed but disabled by the user or system.
        /// </summary>
        Disabled,

        /// <summary>
        /// An update is available for the extension.
        /// </summary>
        UpdateAvailable,

        /// <summary>
        /// The extension is incompatible with the current system or engine version.
        /// </summary>
        Incompatible,

        /// <summary>
        /// The extension is in a broken state and cannot be loaded.
        /// </summary>
        Broken
    }
}
