namespace GeneralUpdate.Zip.Events
{
    public class BaseCompleteEventArgs
    {
        public bool IsCompleted { get; set; }

        public BaseCompleteEventArgs(bool isCompleted) => IsCompleted = isCompleted;
    }
}