namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents the current status of a plugin.
    /// </summary>
    public enum PluginStatus
    {
        /// <summary>
        /// Plugin is not installed.
        /// </summary>
        NotInstalled,

        /// <summary>
        /// Plugin is installed and operational.
        /// </summary>
        Installed,

        /// <summary>
        /// Plugin is disabled by user or system.
        /// </summary>
        Disabled,

        /// <summary>
        /// Plugin encountered an error.
        /// </summary>
        Error,

        /// <summary>
        /// Plugin is being uninstalled.
        /// </summary>
        Uninstalling
    }

    /// <summary>
    /// Represents the update status of a plugin during the update lifecycle.
    /// </summary>
    public enum UpdateStatus
    {
        /// <summary>
        /// No update is in progress.
        /// </summary>
        Idle,

        /// <summary>
        /// Checking for available updates.
        /// </summary>
        CheckingForUpdates,

        /// <summary>
        /// Update is queued for download.
        /// </summary>
        Queued,

        /// <summary>
        /// Plugin is being downloaded.
        /// </summary>
        Downloading,

        /// <summary>
        /// Download completed, preparing for installation.
        /// </summary>
        Downloaded,

        /// <summary>
        /// Plugin update is being installed.
        /// </summary>
        Installing,

        /// <summary>
        /// Update completed successfully.
        /// </summary>
        UpdateSucceeded,

        /// <summary>
        /// Update failed.
        /// </summary>
        UpdateFailed,

        /// <summary>
        /// Update was cancelled by user.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Rolling back to previous version.
        /// </summary>
        RollingBack
    }
}
