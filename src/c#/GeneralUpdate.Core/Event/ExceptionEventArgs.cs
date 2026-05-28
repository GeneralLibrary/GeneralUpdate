using System;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// Event arguments for exceptions that occur during the update flow,
/// encapsulating the exception object and a custom error message.
/// </summary>
/// <remarks>
/// <para>
/// When an exception occurs in any stage of the update process (e.g., download,
/// extraction, file operations), this event argument is dispatched through
/// <see cref="EventManager"/>.
/// </para>
/// <para>
/// Event receivers can obtain detailed exception information via the
/// <see cref="Exception"/> property and a human-readable error description
/// via the <see cref="Message"/> property.
/// </para>
/// </remarks>
public class ExceptionEventArgs(Exception? exception = null, string? message = null) : EventArgs
{
    /// <summary>
    /// Gets the exception object associated with the event.
    /// </summary>
    /// <value>May be <c>null</c> if the error information is conveyed only through a text message.</value>
    public Exception Exception { get; private set; } = exception;

    /// <summary>
    /// Gets the custom error description message.
    /// </summary>
    /// <value>May be <c>null</c> if no custom message was provided.</value>
    public string Message { get; private set; } = message;
}
