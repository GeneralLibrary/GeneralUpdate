using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Exceptions.CustomArgs
{
    [Serializable]
    public abstract class ExceptionArgs
    {
        public virtual string Message { get { return String.Empty; } }
    }
}
