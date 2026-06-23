namespace GeneralUpdate.Drivelution.Core.Pipeline;

/// <summary>
/// Represents the outcome of a single pipeline step execution.
/// </summary>
public class PipelineResult
{
    /// <summary>
    /// Whether the step executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the step failed (null when successful).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Optional exception captured during step execution.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PipelineResult Ok() => new() { Success = true };

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    /// <param name="errorMessage">Description of the failure.</param>
    /// <param name="exception">Optional exception that caused the failure.</param>
    public static PipelineResult Fail(string errorMessage, Exception? exception = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}
