using System;

namespace GeneralUpdate.Core.Domain.PO
{
    public class TestamentPO
    {
        /// <summary>
        /// The tracking id of this update request
        /// </summary>
        public string TrackID { get; set; }

        /// <summary>
        /// dump files everywhere after an exception occurs.
        /// </summary>
        public string DumpPath { get; set; }

        /// <summary>
        /// Backup the file path that needs to be updated before updating.
        /// </summary>
        public string BackupPath { get; set; }

        /// <summary>
        /// Exception information that occurs when updating fails.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
