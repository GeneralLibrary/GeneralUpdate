using System;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Events
{
    /// <summary>
    /// Base event args for extension-related events.
    /// </summary>
    public class ExtensionEventArgs : EventArgs
    {
        /// <summary>
        /// The extension ID associated with this event.
        /// </summary>
        public string ExtensionId { get; set; } = string.Empty;

        /// <summary>
        /// The extension name associated with this event.
        /// </summary>
        public string ExtensionName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event args for extension update status changes.
    /// </summary>
    public class ExtensionUpdateStatusChangedEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// The queue item associated with this update.
        /// </summary>
        public ExtensionUpdateQueueItem QueueItem { get; set; } = new ExtensionUpdateQueueItem();

        /// <summary>
        /// The old status before the change.
        /// </summary>
        public ExtensionUpdateStatus OldStatus { get; set; }

        /// <summary>
        /// The new status after the change.
        /// </summary>
        public ExtensionUpdateStatus NewStatus { get; set; }
    }

    /// <summary>
    /// Event args for extension download progress updates.
    /// </summary>
    public class ExtensionDownloadProgressEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Current download progress percentage (0-100).
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Total bytes to download.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Bytes downloaded so far.
        /// </summary>
        public long ReceivedBytes { get; set; }

        /// <summary>
        /// Download speed formatted as string.
        /// </summary>
        public string? Speed { get; set; }

        /// <summary>
        /// Estimated remaining time.
        /// </summary>
        public TimeSpan RemainingTime { get; set; }
    }

    /// <summary>
    /// Event args for extension installation events.
    /// </summary>
    public class ExtensionInstallEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Whether the installation was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Installation path.
        /// </summary>
        public string? InstallPath { get; set; }

        /// <summary>
        /// Error message if installation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Event args for extension rollback events.
    /// </summary>
    public class ExtensionRollbackEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Whether the rollback was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if rollback failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
