using System;

namespace GeneralUpdate.Core.Exceptions.CustomArgs
{
    [Serializable]
    public abstract class ExceptionArgs
    {
        public virtual string Message
        { get { return String.Empty; } }
    }
}