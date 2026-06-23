using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Generates crash reports from procdump output and surveillance parameters.
/// </summary>
internal interface ICrashReporter
{
    /// <summary>
    /// Generates a crash report JSON file in the fail directory.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="outputLines">Captured stdout/stderr lines from procdump.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full path to the generated report file.</returns>
    Task<string> GenerateReportAsync(
        BowlContext context,
        IReadOnlyList<string> outputLines,
        CancellationToken ct);
}
