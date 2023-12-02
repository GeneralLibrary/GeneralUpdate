using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Domain.PO
{
    internal enum WillMessageStatus
    {
        /// <summary>
        /// Processing has not yet begun.
        /// </summary>
        NotStarted,
        /// <summary>
        /// Processing completed.
        /// </summary>
        Completed,
        /// <summary>
        /// Processing failure.
        /// </summary>
        Failed
    }

    internal class BackupPO 
    {
        public string Name { get; set; }

        public string InstallPath { get; set; }

        public string BackupPath { get; set; }

        public string Version { get; set; }

        public int AppType { get; set; }
    }

    internal class WillMessagePO
    {
        public Stack<List<BackupPO>> Message { get; set; }

        public WillMessageStatus Status { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime ChangeTime { get; set; }
    }
}
