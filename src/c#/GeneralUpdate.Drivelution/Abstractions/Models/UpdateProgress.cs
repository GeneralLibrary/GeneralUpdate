using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Abstractions.Models;

/// <summary>
/// Progress information reported during a driver update operation.
/// Suitable for binding to UI progress bars and status displays.
/// </summary>
public class UpdateProgress
{
    /// <summary>
    /// Current update status in the pipeline.
    /// </summary>
    public UpdateStatus CurrentStatus { get; init; }

    /// <summary>
    /// Name of the currently executing pipeline step.
    /// </summary>
    public string StepName { get; init; } = string.Empty;

    /// <summary>
    /// Overall completion percentage (0-100).
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Human-readable progress message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Index of the current step (0-based).
    /// </summary>
    public int StepIndex { get; init; }

    /// <summary>
    /// Total number of steps in the pipeline.
    /// </summary>
    public int TotalSteps { get; init; }

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Percentage}%] {StepName} ({StepIndex + 1}/{TotalSteps}): {Message}";
}
