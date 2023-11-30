namespace GeneralUpdate.SystemService.PersistenceObjects
{
    internal class WillMessagePersistence<T> where T : class
    {
        public required List<T> Messages { get; set; }
    }
}
