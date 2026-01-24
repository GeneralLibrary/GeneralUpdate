namespace GeneralUpdate.Extension.Download
{
    /// <summary>
    /// Defines the lifecycle states of an extension update operation.
    /// </summary>
    public enum UpdateState
    {
        /// <summary>
        /// Update has been queued but not yet started.
        /// </summary>
        Queued = 0,

        /// <summary>
        /// Update is currently downloading or installing.
        /// </summary>
        Updating = 1,

        /// <summary>
        /// Update completed successfully.
        /// </summary>
        UpdateSuccessful = 2,

        /// <summary>
        /// Update failed due to an error.
        /// </summary>
        UpdateFailed = 3,

        /// <summary>
        /// Update was cancelled by the user or system.
        /// </summary>
        Cancelled = 4
    }
}
