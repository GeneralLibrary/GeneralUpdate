namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Configures runtime options for <see cref="DiffPipeline"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DiffPipelineOptions"/> defines the runtime behavior parameters of the differential pipeline.
/// These options can be configured via the fluent API of <see cref="DiffPipelineBuilder"/> or passed directly
/// to the <see cref="DiffPipeline"/> constructor.
/// </para>
/// <para>
/// Default values:
/// <list type="bullet">
///   <item><description><see cref="MaxDegreeOfParallelism"/> = 2 — Processes 2 files concurrently.</description></item>
///   <item><description><see cref="StopOnFirstError"/> = <c>false</c> — Continues processing other files on error.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class DiffPipelineOptions
{
    /// <summary>
    /// Gets or sets the maximum number of files that can be processed concurrently.
    /// </summary>
    /// <value>
    /// The maximum number of files to process in parallel. The default value is 2.
    /// Set to 1 to force serial processing.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property controls the initial count of the <see cref="System.Threading.SemaphoreSlim"/>
    /// used internally by <see cref="DiffPipeline"/> to limit the number of concurrent file processing tasks.
    /// </para>
    /// <para>
    /// Tuning recommendations:
    /// <list type="bullet">
    ///   <item><description>1: Fully serial execution, minimum memory usage, suitable for resource-constrained environments.</description></item>
    ///   <item><description>2 (default): Minimum parallelism, provides good performance gains in most environments.</description></item>
    ///   <item><description>4: Suitable for modern multi-core CPUs and SSD storage.</description></item>
    ///   <item><description>8+: Suitable for high-performance servers; be mindful of I/O saturation.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int MaxDegreeOfParallelism { get; set; } = 2;

    /// <summary>
    /// Gets or sets a value indicating whether the pipeline should stop immediately when a file processing error occurs.
    /// </summary>
    /// <value>
    /// If <c>true</c>, the first file error causes the pipeline to terminate immediately and throw an exception;
    /// if <c>false</c> (default), errors are logged and passed through progress reporting, and the pipeline
    /// continues processing remaining files.
    /// </value>
    /// <remarks>
    /// <para>
    /// When this property is <c>false</c>, a processing failure for an individual file does not affect other files.
    /// Failure information is passed to the progress reporter via the <see cref="DiffProgress.ErrorMessage"/> property.
    /// After processing completes, callers can inspect the results for each file.
    /// </para>
    /// <para>
    /// When this property is <c>true</c>, any file failure cancels all ongoing tasks and throws an exception
    /// to the caller. Suitable for update scenarios with high data consistency requirements.
    /// </para>
    /// </remarks>
    public bool StopOnFirstError { get; set; } = false;
}
