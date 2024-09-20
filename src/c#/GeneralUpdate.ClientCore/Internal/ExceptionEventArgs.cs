using System;

namespace GeneralUpdate.ClientCore.Internal;

public class ExceptionEventArgs : EventArgs
{
    public ExceptionEventArgs(Exception exception = null, string message = null)
    {
        Exception = exception ?? throw new Exception(nameof(exception));
        Message = message ?? exception.Message;
    }

    public Exception Exception { get; private set; }
    public string Message { get; private set; }
}