namespace GeneralUpdate.SystemService.PersistenceObjects
{
    internal enum ProcessStatus 
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

    internal class ProcessPersistence
    {
        public required string Name { get; set; }

        public required string Path { get; set; }

        public required string BackupPath { get; set; }

        public ProcessStatus Status { get; set; }

        public DateTime CreateTime { get; set; }
    }
}
