using System;

namespace GeneralUpdate.Common.Exception
{
    [Serializable]
    public abstract class BaseArgs
    {
        public virtual string Message
        { get { return String.Empty; } }
    }
}