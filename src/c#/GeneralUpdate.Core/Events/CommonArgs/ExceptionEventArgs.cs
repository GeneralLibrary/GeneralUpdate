using System;

namespace GeneralUpdate.Core.Events.CommonArgs
{
    public class ExceptionEventArgs : EventArgs
    {
        private readonly Exception _exception;

        public ExceptionEventArgs(Exception exception)
        {
            _exception = exception;
        }

        public Exception Exception => _exception;
    }
}
