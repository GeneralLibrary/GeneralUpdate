namespace GeneralUpdate.Drivelution.Abstractions.Events;

/// <summary>
/// Default implementation of IDrivelutionLogger that raises events
/// </summary>
public class DrivelutionLogger : IDrivelutionLogger
{
    /// <inheritdoc />
    public event EventHandler<LogEventArgs>? LogMessage;
    
    /// <inheritdoc />
    public void Debug(string message, params object[] args)
    {
        RaiseLogEvent(LogLevel.Debug, message, null, args);
    }
    
    /// <inheritdoc />
    public void Information(string message, params object[] args)
    {
        RaiseLogEvent(LogLevel.Information, message, null, args);
    }
    
    /// <inheritdoc />
    public void Warning(string message, params object[] args)
    {
        RaiseLogEvent(LogLevel.Warning, message, null, args);
    }
    
    /// <inheritdoc />
    public void Error(string message, Exception? exception = null, params object[] args)
    {
        RaiseLogEvent(LogLevel.Error, message, exception, args);
    }
    
    /// <inheritdoc />
    public void Fatal(string message, Exception? exception = null, params object[] args)
    {
        RaiseLogEvent(LogLevel.Fatal, message, exception, args);
    }
    
    private void RaiseLogEvent(LogLevel level, string message, Exception? exception, params object[] args)
    {
        try
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            
            var eventArgs = new LogEventArgs
            {
                Level = level,
                Message = formattedMessage,
                Exception = exception,
                Timestamp = DateTime.UtcNow
            };
            
            LogMessage?.Invoke(this, eventArgs);
        }
        catch
        {
            // Silently ignore exceptions in logging to prevent cascading failures
        }
    }
}
