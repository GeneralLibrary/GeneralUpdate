using System;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Configuration options for <see cref="DiffPipeline"/>.
/// </summary>
public sealed class DiffPipelineOptions
{
    /// <summary>
    /// Gets or sets the maximum number of files to process concurrently.
    /// Default: <see cref="Environment.ProcessorCount"/>.
    /// Set to 1 for sequential processing.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether to stop processing on first error.
    /// Default: false (continue processing other files, report errors via progress).
    /// </summary>
    public bool StopOnFirstError { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to delete the patch directory after successful apply.
    /// Default: true.
    /// </summary>
    public bool DeletePatchAfterApply { get; set; } = true;
}
