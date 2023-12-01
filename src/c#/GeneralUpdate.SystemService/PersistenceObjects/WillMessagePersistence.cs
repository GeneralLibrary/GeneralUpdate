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

    internal class WillMessagePersistence<T> where T : class
    {
        public List<T> Messages { get; set; }

        public DateTime CreateTime { get; set; }

        public ProcessStatus Status { get; set; }
    }
}
