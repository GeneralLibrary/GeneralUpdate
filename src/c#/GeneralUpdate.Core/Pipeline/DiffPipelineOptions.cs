using System;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Configuration options for <see cref="DiffPipeline"/>.
/// </summary>
public sealed class DiffPipelineOptions
{
    /// <summary>
    /// Gets or sets the maximum number of files to process concurrently.
    /// Default: 2.
    /// Set to 1 for sequential processing.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether to stop processing on first error.
    /// Default: false (continue processing other files, report errors via progress).
    /// </summary>
    public bool StopOnFirstError { get; set; } = false;
}
