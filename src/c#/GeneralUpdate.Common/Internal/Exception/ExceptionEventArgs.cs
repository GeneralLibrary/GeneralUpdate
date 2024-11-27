using System;

namespace GeneralUpdate.Common.Internal;

public class ExceptionEventArgs(Exception? exception = null, string? message = null) : EventArgs
{
    public Exception Exception { get; private set; } = exception;
    public string Message { get; private set; } = message;
}