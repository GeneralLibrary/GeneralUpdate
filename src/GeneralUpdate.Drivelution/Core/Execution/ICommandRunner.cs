namespace GeneralUpdate.Drivelution.Core.Execution;

/// <summary>
/// Abstraction for safe, cross-platform command execution.
/// Passes arguments individually to avoid shell injection—no string concatenation into Arguments.
/// </summary>
public interface ICommandRunner
{
    /// <summary>
    /// Runs a command with the given arguments.
    /// </summary>
    /// <param name="command">Executable name or path.</param>
    /// <param name="arguments">Arguments passed individually (not shell-parsed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="CommandResult"/> containing exit code, stdout, and stderr.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started.</exception>
    Task<CommandResult> RunAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a command and throws if the exit code is non-zero.
    /// </summary>
    /// <param name="command">Executable name or path.</param>
    /// <param name="arguments">Arguments passed individually.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A successful <see cref="CommandResult"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process exits with a non-zero code.</exception>
    Task<CommandResult> RunOrThrowAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default);
}
