using System;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents an update event with plugin information and status details.
    /// </summary>
    public class PluginUpdateEvent
    {
        /// <summary>
        /// The plugin associated with this event.
        /// </summary>
        public PluginInfo Plugin { get; set; }

        /// <summary>
        /// Current update status.
        /// </summary>
        public UpdateStatus Status { get; set; }

        /// <summary>
        /// Progress percentage (0-100) for download or installation.
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Optional message describing the event.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Optional exception if an error occurred.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Timestamp of when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Previous version before update (if applicable).
        /// </summary>
        public string PreviousVersion { get; set; }

        /// <summary>
        /// New version after update (if applicable).
        /// </summary>
        public string NewVersion { get; set; }
    }
}
