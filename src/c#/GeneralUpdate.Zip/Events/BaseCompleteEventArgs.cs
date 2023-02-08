using System;

namespace GeneralUpdate.Zip.Events
{
    public class BaseCompleteEventArgs : EventArgs
    {
        public bool IsCompleted { get; set; }

        public BaseCompleteEventArgs(bool isCompleted) => IsCompleted = isCompleted;
    }
}