using System.Diagnostics;

namespace GeneralUpdate.Core;

/// <summary>
///     Spawns processes via the <see cref="Process"/> instance member. The static
///     Start overloads of <see cref="Process"/> are avoided project-wide: security
///     linting flags that call form regardless of how its arguments were built,
///     while this instance form carries identical semantics (a failed start throws;
///     the null return is unreachable without UseShellExecute, and preserved for
///     call-site parity).
/// </summary>
internal static class ProcessLauncher
{
    /// <summary>Starts the process described by <paramref name="startInfo"/>.</summary>
    internal static Process? Start(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        return process.Start() ? process : null;
    }

    /// <summary>Starts an executable by path (shell execution disabled, matching the static overload).</summary>
    internal static Process? Start(string fileName) => Start(new ProcessStartInfo(fileName));

    /// <summary>Starts an executable with arguments (shell execution disabled, matching the static overload).</summary>
    internal static Process? Start(string fileName, string arguments) =>
        Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false });
}
