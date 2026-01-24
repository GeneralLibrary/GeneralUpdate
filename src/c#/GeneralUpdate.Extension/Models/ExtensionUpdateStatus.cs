namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents the status of an extension update.
    /// </summary>
    public enum ExtensionUpdateStatus
    {
        /// <summary>
        /// Update has been queued but not started.
        /// </summary>
        Queued = 0,

        /// <summary>
        /// Update is currently in progress (downloading or installing).
        /// </summary>
        Updating = 1,

        /// <summary>
        /// Update completed successfully.
        /// </summary>
        UpdateSuccessful = 2,

        /// <summary>
        /// Update failed.
        /// </summary>
        UpdateFailed = 3,

        /// <summary>
        /// Update was cancelled.
        /// </summary>
        Cancelled = 4
    }
}
