using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Core.Pipeline;

/// <summary>
/// Defines a single step in the driver update pipeline.
/// Each step encapsulates one stage of the update process (validate, backup, install, verify, etc.).
/// </summary>
public interface IPipelineStep
{
    /// <summary>
    /// Human-readable name of this step, used for logging and progress reporting.
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// Determines whether this step should be executed given the current context.
    /// Allows steps to conditionally skip based on strategy (e.g., backup step skipped when RequireBackup is false).
    /// </summary>
    /// <param name="context">Current pipeline context.</param>
    /// <returns><c>true</c> if the step should run; otherwise <c>false</c>.</returns>
    bool ShouldExecute(PipelineContext context);

    /// <summary>
    /// Executes the pipeline step asynchronously.
    /// </summary>
    /// <param name="context">Mutable pipeline context carrying driver info, strategy, and result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PipelineResult"/> indicating success or failure.</returns>
    Task<PipelineResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
}
