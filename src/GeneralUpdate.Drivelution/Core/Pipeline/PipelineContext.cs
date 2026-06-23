using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Core.Pipeline;

/// <summary>
/// Mutable context object that flows through the driver update pipeline.
/// Carries driver information, strategy configuration, and the accumulating result.
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Initializes a new pipeline context.
    /// </summary>
    /// <param name="driverInfo">Driver information for the update.</param>
    /// <param name="strategy">Update strategy configuration.</param>
    /// <param name="result">Accumulating update result (mutated by each step).</param>
    public PipelineContext(DriverInfo driverInfo, UpdateStrategy strategy, UpdateResult result)
    {
        DriverInfo = driverInfo ?? throw new ArgumentNullException(nameof(driverInfo));
        Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Driver information for the update.
    /// </summary>
    public DriverInfo DriverInfo { get; }

    /// <summary>
    /// Update strategy configuration.
    /// </summary>
    public UpdateStrategy Strategy { get; }

    /// <summary>
    /// Accumulating update result (mutated by each pipeline step).
    /// </summary>
    public UpdateResult Result { get; }

    /// <summary>
    /// A mutable bag of key-value pairs for steps to share intermediate data.
    /// Example: the backup step stores the backup path here, the rollback step reads it.
    /// </summary>
    public Dictionary<string, object?> Bag { get; } = new();
}
