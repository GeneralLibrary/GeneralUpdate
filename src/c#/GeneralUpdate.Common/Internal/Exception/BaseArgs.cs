using System;

namespace GeneralUpdate.Common.Internal
{
    [Serializable]
    public abstract class BaseArgs
    {
        public virtual string Message
        { get { return String.Empty; } }
    }
}