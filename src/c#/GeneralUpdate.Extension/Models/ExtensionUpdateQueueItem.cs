using System;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents an item in the extension update queue.
    /// </summary>
    public class ExtensionUpdateQueueItem
    {
        /// <summary>
        /// Unique identifier for this queue item.
        /// </summary>
        public string QueueId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Extension to be updated.
        /// </summary>
        public RemoteExtension Extension { get; set; } = new RemoteExtension();

        /// <summary>
        /// Current status of the update.
        /// </summary>
        public ExtensionUpdateStatus Status { get; set; } = ExtensionUpdateStatus.Queued;

        /// <summary>
        /// Download progress percentage (0-100).
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Time when the item was added to the queue.
        /// </summary>
        public DateTime QueuedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Time when the update started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Time when the update completed or failed.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Error message if the update failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Whether to trigger rollback on installation failure.
        /// </summary>
        public bool EnableRollback { get; set; } = true;
    }
}
