namespace GeneralUpdate.Drivelution.Core.Execution;

/// <summary>
/// Structured result of a command execution.
/// </summary>
public class CommandResult
{
    /// <summary>
    /// Process exit code.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output text.
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Standard error text.
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// Whether the command succeeded (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// Returns a summary of the result for logging.
    /// </summary>
    public override string ToString() =>
        Success
            ? $"ExitCode={ExitCode}, Output={StandardOutput.Trim()}"
            : $"ExitCode={ExitCode}, Error={StandardError.Trim()}";
}
