using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Platform-specific crash surveillance strategy.
/// Replaces the legacy <c>IStrategy</c> with a two-phase design (Prepare / PostProcess).
/// </summary>
internal interface IBowlStrategy
{
    /// <summary>
    /// Prepare the child process start info (procdump path and arguments).
    /// Returns <c>null</c> if the platform is not supported (caller should check before invoking).
    /// </summary>
    /// <param name="context">Immutable execution context.</param>
    /// <returns>Configured <see cref="ProcessStartInfo"/> or <c>null</c>.</returns>
    ProcessStartInfo? Prepare(in BowlContext context);

    /// <summary>
    /// Platform-specific post-processing after the child process exits.
    /// Called only when a dump file was captured (crash detected).
    /// </summary>
    /// <param name="context">Immutable execution context.</param>
    /// <param name="exitResult">Process exit details including stdout/stderr lines.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PostProcessAsync(in BowlContext context, ProcessExitResult exitResult,
        CancellationToken ct);
}
