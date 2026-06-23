namespace GeneralUpdate.Drivelution.Abstractions.Models;

/// <summary>
/// Result of a batch driver update operation, containing per-driver outcomes.
/// </summary>
public class BatchUpdateResult
{
    /// <summary>
    /// Overall batch success (true only when all drivers updated successfully).
    /// </summary>
    public bool AllSucceeded { get; set; }

    /// <summary>
    /// Number of drivers that succeeded.
    /// </summary>
    public int SucceededCount { get; set; }

    /// <summary>
    /// Number of drivers that failed.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Individual results per driver, in the same order as the input list.
    /// </summary>
    public List<DriverUpdateEntry> Results { get; set; } = new();

    /// <summary>
    /// Total wall-clock duration of the batch operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Returns a summary of the batch operation.
    /// </summary>
    public override string ToString()
        => $"BatchComplete: {SucceededCount} succeeded, {FailedCount} failed, Duration={Duration.TotalSeconds:F1}s";
}

/// <summary>
/// Entry in a batch update result — links a driver to its individual update outcome.
/// </summary>
public class DriverUpdateEntry
{
    /// <summary>
    /// Driver information for this entry.
    /// </summary>
    public DriverInfo DriverInfo { get; init; } = default!;

    /// <summary>
    /// Whether this driver's update succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The full update result from the pipeline.
    /// </summary>
    public UpdateResult? Result { get; init; }
}

/// <summary>
/// Execution mode for batch driver updates.
/// </summary>
public enum BatchMode
{
    /// <summary>Process drivers one at a time in order.</summary>
    Sequential,

    /// <summary>Process drivers concurrently using Task.WhenAll.</summary>
    Parallel
}
