using System;

namespace GeneralUpdate.Extension.Download
{
    /// <summary>
    /// Represents a queued extension update operation with progress tracking.
    /// </summary>
    public class UpdateOperation
    {
        /// <summary>
        /// Gets or sets the unique identifier for this update operation.
        /// </summary>
        public string OperationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the extension to be updated.
        /// </summary>
        public Metadata.ExtensionMetadata Extension { get; set; } = new Metadata.ExtensionMetadata();

        /// <summary>
        /// Gets or sets the current state of the update operation.
        /// </summary>
        public UpdateState State { get; set; } = UpdateState.Queued;

        /// <summary>
        /// Gets or sets the download progress percentage (0-100).
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this operation was queued.
        /// </summary>
        public DateTime QueuedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the timestamp when the update started.
        /// Null if not yet started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the update completed or failed.
        /// Null if still in progress.
        /// </summary>
        public DateTime? CompletionTime { get; set; }

        /// <summary>
        /// Gets or sets the error message if the update failed.
        /// Null if no error occurred.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether rollback should be attempted on installation failure.
        /// </summary>
        public bool EnableRollback { get; set; } = true;
    }
}
