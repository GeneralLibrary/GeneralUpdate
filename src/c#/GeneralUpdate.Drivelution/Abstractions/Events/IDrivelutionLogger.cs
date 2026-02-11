namespace GeneralUpdate.Drivelution.Abstractions.Events;

/// <summary>
/// Logger interface for Drivelution operations
/// Provides event-based logging mechanism to replace Serilog
/// </summary>
public interface IDrivelutionLogger
{
    /// <summary>
    /// Event raised when a log message is generated
    /// </summary>
    event EventHandler<LogEventArgs>? LogMessage;
    
    /// <summary>
    /// Logs a debug message
    /// </summary>
    void Debug(string message, params object[] args);
    
    /// <summary>
    /// Logs an information message
    /// </summary>
    void Information(string message, params object[] args);
    
    /// <summary>
    /// Logs a warning message
    /// </summary>
    void Warning(string message, params object[] args);
    
    /// <summary>
    /// Logs an error message
    /// </summary>
    void Error(string message, Exception? exception = null, params object[] args);
    
    /// <summary>
    /// Logs a fatal message
    /// </summary>
    void Fatal(string message, Exception? exception = null, params object[] args);
}
