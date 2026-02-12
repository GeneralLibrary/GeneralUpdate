namespace GeneralUpdate.Drivelution.Abstractions.Events;

/// <summary>
/// Event arguments for log messages from Drivelution operations
/// </summary>
public class LogEventArgs : EventArgs
{
    /// <summary>
    /// Log level
    /// </summary>
    public LogLevel Level { get; set; }
    
    /// <summary>
    /// Log message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Exception if any
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// Timestamp of the log
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional context data
    /// </summary>
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Log level enumeration
/// </summary>
public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}
