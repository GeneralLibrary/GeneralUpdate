using System;

namespace GeneralUpdate.Extension.EventHandlers
{
    /// <summary>
    /// Base class for all extension-related event arguments.
    /// </summary>
    public class ExtensionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the unique identifier of the extension associated with this event.
        /// </summary>
        public string ExtensionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the extension associated with this event.
        /// </summary>
        public string ExtensionName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Provides data for events that occur when an extension update state changes.
    /// </summary>
    public class UpdateStateChangedEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Gets or sets the update operation associated with this state change.
        /// </summary>
        public Download.UpdateOperation Operation { get; set; } = new Download.UpdateOperation();

        /// <summary>
        /// Gets or sets the previous state before the change.
        /// </summary>
        public Download.UpdateState PreviousState { get; set; }

        /// <summary>
        /// Gets or sets the new state after the change.
        /// </summary>
        public Download.UpdateState CurrentState { get; set; }
    }

    /// <summary>
    /// Provides data for download progress update events.
    /// </summary>
    public class DownloadProgressEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Gets or sets the current download progress percentage (0-100).
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Gets or sets the total number of bytes to download.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes downloaded so far.
        /// </summary>
        public long ReceivedBytes { get; set; }

        /// <summary>
        /// Gets or sets the formatted download speed string (e.g., "1.5 MB/s").
        /// </summary>
        public string? Speed { get; set; }

        /// <summary>
        /// Gets or sets the estimated remaining time for the download.
        /// </summary>
        public TimeSpan RemainingTime { get; set; }
    }

    /// <summary>
    /// Provides data for extension installation completion events.
    /// </summary>
    public class InstallationCompletedEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Gets or sets a value indicating whether the installation completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the file system path where the extension was installed.
        /// Null if installation failed.
        /// </summary>
        public string? InstallPath { get; set; }

        /// <summary>
        /// Gets or sets the error message if installation failed.
        /// Null if installation succeeded.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Provides data for extension rollback completion events.
    /// </summary>
    public class RollbackCompletedEventArgs : ExtensionEventArgs
    {
        /// <summary>
        /// Gets or sets a value indicating whether the rollback completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if rollback failed.
        /// Null if rollback succeeded.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
