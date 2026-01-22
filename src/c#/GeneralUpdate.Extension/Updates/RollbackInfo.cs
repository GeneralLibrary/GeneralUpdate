using System;

namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Represents information about a rollback operation, including the target version and reason.
    /// </summary>
    public class RollbackInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the extension being rolled back.
        /// </summary>
        public string ExtensionId { get; set; }

        /// <summary>
        /// Gets or sets the current version before rollback.
        /// </summary>
        public string CurrentVersion { get; set; }

        /// <summary>
        /// Gets or sets the target version to roll back to.
        /// </summary>
        public string TargetVersion { get; set; }

        /// <summary>
        /// Gets or sets the reason for the rollback.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the rollback was initiated.
        /// </summary>
        public DateTime RollbackTimestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to preserve user data during rollback.
        /// </summary>
        public bool PreserveUserData { get; set; }

        /// <summary>
        /// Gets or sets the path to the rollback snapshot or backup.
        /// </summary>
        public string SnapshotPath { get; set; }
    }
}
