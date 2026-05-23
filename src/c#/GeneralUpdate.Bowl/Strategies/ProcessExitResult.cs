using System.Collections.Generic;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Result of the child (procdump) process run.
/// </summary>
internal readonly record struct ProcessExitResult
{
    /// <summary>Process exit code.</summary>
    public int ExitCode { get; init; }

    /// <summary>Standard output lines captured from the child process.</summary>
    public IReadOnlyList<string> OutputLines { get; init; }
}
