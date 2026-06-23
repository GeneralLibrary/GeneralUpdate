using System;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Fluent builder for <see cref="DiffPipeline"/>, providing a chained configuration API.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DiffPipelineBuilder"/> offers a declarative way to configure and create
/// <see cref="DiffPipeline"/> instances. All configuration methods return the builder instance
/// itself, supporting method chaining.
/// </para>
/// <para>
/// Default configuration:
/// <list type="bullet">
///   <item><description>Binary differ: <see cref="StreamingHdiffDiffer"/></description></item>
///   <item><description>Clean matcher: <see cref="DefaultCleanMatcher"/></description></item>
///   <item><description>Dirty matcher: <see cref="DefaultDirtyMatcher"/></description></item>
///   <item><description>Maximum parallelism: 2</description></item>
///   <item><description>Stop on first error: <c>false</c> (continue processing other files)</description></item>
/// </list>
/// </para>
/// <para>
/// Usage example:
/// <code>
/// var pipeline = new DiffPipelineBuilder()
///     .UseDiffer(new StreamingHdiffDiffer())
///     .UseCleanMatcher(new DefaultCleanMatcher())
///     .UseDirtyMatcher(new DefaultDirtyMatcher())
///     .WithParallelism(4)
///     .WithStopOnFirstError(true)
///     .WithProgress(new Progress&lt;DiffProgress&gt;(p =&gt; Console.WriteLine($"{p.Completed}/{p.Total}")))
///     .Build();
///
/// // Generate patches
/// await pipeline.CleanAsync(oldVersionDir, newVersionDir, patchOutputDir);
///
/// // Apply patches
/// await pipeline.DirtyAsync(appDir, patchDir);
/// </code>
/// </para>
/// </remarks>
public class DiffPipelineBuilder
{
    private IBinaryDiffer? _differ;
    private ICleanMatcher? _cleanMatcher;
    private IDirtyMatcher? _dirtyMatcher;
    private int _maxParallelism = 2;
    private bool _stopOnFirstError;
    private IProgress<DiffProgress>? _progress;

    /// <summary>
    /// Sets the binary differ used to generate and apply binary differential patches.
    /// </summary>
    /// <param name="differ">The binary differ instance implementing <see cref="IBinaryDiffer"/>. Must not be <c>null</c>.</param>
    /// <returns>The current <see cref="DiffPipelineBuilder"/> instance, enabling chained calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="differ"/> is <c>null</c>.</exception>
    /// <remarks>
    /// If this method is not called, <see cref="StreamingHdiffDiffer"/> is used by default.
    /// <see cref="StreamingHdiffDiffer"/> is based on the HDiffPatch algorithm and offers good compression
    /// ratios and performance. Custom binary differ algorithms can be used by implementing the
    /// <see cref="IBinaryDiffer"/> interface.
    /// </remarks>
    public DiffPipelineBuilder UseDiffer(IBinaryDiffer differ)
    {
        _differ = differ ?? throw new ArgumentNullException(nameof(differ));
        return this;
    }

    /// <summary>
    /// Sets the file matcher used during the Clean phase (<see cref="DiffPipeline.CleanAsync"/>),
    /// for directory comparison and file matching during patch generation.
    /// </summary>
    /// <param name="matcher">The matcher instance implementing <see cref="ICleanMatcher"/>. Must not be <c>null</c>.</param>
    /// <returns>The current <see cref="DiffPipelineBuilder"/> instance, enabling chained calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matcher"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// <see cref="ICleanMatcher"/> is responsible for two key operations:
    /// <list type="bullet">
    ///   <item><description><c>Compare</c> — Compares the old and new directories, identifying changed, new, and deleted files.</description></item>
    ///   <item><description><c>Match</c> — During patch generation, matches files from the new version with corresponding files in the old version.</description></item>
    ///   <item><description><c>Except</c> — Identifies files present in the old version but deleted from the new version.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If this method is not called, <see cref="DefaultCleanMatcher"/> is used by default.
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder UseCleanMatcher(ICleanMatcher matcher)
    {
        _cleanMatcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        return this;
    }

    /// <summary>
    /// Sets the file matcher used during the Dirty phase (<see cref="DiffPipeline.DirtyAsync"/>),
    /// for matching patch files to their corresponding old version files in the application directory.
    /// </summary>
    /// <param name="matcher">The matcher instance implementing <see cref="IDirtyMatcher"/>. Must not be <c>null</c>.</param>
    /// <returns>The current <see cref="DiffPipelineBuilder"/> instance, enabling chained calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matcher"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// During the Dirty phase, each patch file in the patch directory needs to be paired with its corresponding
    /// original file in the application directory. The <see cref="IDirtyMatcher.Match"/> method receives an old
    /// file object and the list of patch files, and returns the matching patch file.
    /// </para>
    /// <para>
    /// The default matcher <see cref="DefaultDirtyMatcher"/> performs matching based on the relative path
    /// of file paths. Custom matchers can implement matching logic based on file names, hash values, or
    /// other strategies.
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder UseDirtyMatcher(IDirtyMatcher matcher)
    {
        _dirtyMatcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        return this;
    }

    /// <summary>
    /// Sets the maximum degree of parallelism for file processing.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The maximum number of files to process concurrently. Must be greater than 0.</param>
    /// <returns>The current <see cref="DiffPipelineBuilder"/> instance, enabling chained calls.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxDegreeOfParallelism"/> is less than 1.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This value controls the number of files processed simultaneously in
    /// <see cref="DiffPipeline.CleanAsync"/> and <see cref="DiffPipeline.DirtyAsync"/>.
    /// Higher values can improve processing speed on multi-core systems but also increase memory and I/O
    /// resource consumption.
    /// </para>
    /// <para>
    /// Recommendations:
    /// <list type="bullet">
    ///   <item><description>2 (default): Suitable for most scenarios, balancing speed and resource consumption.</description></item>
    ///   <item><description>1: Fully serial processing, suitable for I/O-bound or resource-sensitive environments.</description></item>
    ///   <item><description>4-8: Suitable for high-performance environments with multi-core CPUs and fast SSDs.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder WithParallelism(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        _maxParallelism = maxDegreeOfParallelism;
        return this;
    }

    /// <summary>
    /// Sets whether the entire pipeline should stop immediately when the first file processing error occurs.
    /// </summary>
    /// <param name="stopOnFirstError">
    /// If <c>true</c>, any file processing failure causes the entire operation to terminate immediately
    /// and throw an exception. If <c>false</c> (default), failed files are skipped, processing continues,
    /// and error details are passed through progress reporting.
    /// </param>
    /// <returns>The current <see cref="DiffPipelineBuilder"/> instance, enabling chained calls.</returns>
    /// <remarks>
    /// <para>
    /// When <paramref name="stopOnFirstError"/> is <c>false</c> (default):
    /// Failure of an individual file does not affect the processing of other files. Failure details are
    /// communicated through <see cref="DiffProgress.ErrorMessage"/>, and callers can inspect the processing
    /// status of each file through the progress reporting mechanism. This is particularly useful when
    /// processing large batches of files, minimizing the impact of failures.
    /// </para>
    /// <para>
    /// When <c>true</c>:
    /// Any file failure immediately cancels all ongoing processing tasks and throws an exception.
    /// Suitable for scenarios with high data integrity requirements.
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder WithStopOnFirstError(bool stopOnFirstError = true)
    {
        _stopOnFirstError = stopOnFirstError;
        return this;
    }

    /// <summary>
    /// Attaches a progress reporter to receive real-time file-level progress updates.
    /// </summary>
    /// <param name="progress">
    /// The progress reporter instance implementing <see cref="IProgress{DiffProgress}"/>. Must not be <c>null</c>.
    /// </param>
    /// <returns>The current <see cref="DiffPipelineBuilder"/> instance, enabling chained calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="progress"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The <see cref="IProgress{DiffProgress}"/> will be notified when each file finishes processing,
    /// providing the number of completed files, total files, current file name, and optional error message.
    /// Use <see cref="Progress{DiffProgress}"/> or a custom implementation.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var progress = new Progress&lt;DiffProgress&gt;(p =&gt;
    /// {
    ///     Console.WriteLine($"[{p.Completed}/{p.Total}] {p.FileName}");
    ///     if (!string.IsNullOrEmpty(p.ErrorMessage))
    ///         Console.WriteLine($"    Error: {p.ErrorMessage}");
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder WithProgress(IProgress<DiffProgress> progress)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        return this;
    }

    /// <summary>
    /// Builds a <see cref="DiffPipeline"/> instance using the current configuration.
    /// </summary>
    /// <returns>A fully configured <see cref="DiffPipeline"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// This method packages all configured parameters into a <see cref="DiffPipelineOptions"/> instance,
    /// fills in default values for any parameters not explicitly set (e.g., the binary differ defaults to
    /// <see cref="StreamingHdiffDiffer"/>), creates a new <see cref="DiffPipeline"/> instance, and returns it.
    /// </para>
    /// <para>
    /// The built pipeline can be used for generating patches (<see cref="DiffPipeline.CleanAsync"/>) or
    /// applying patches (<see cref="DiffPipeline.DirtyAsync"/>). Pipeline instances are thread-safe and
    /// can be reused.
    /// </para>
    /// </remarks>
    public DiffPipeline Build()
    {
        var options = new DiffPipelineOptions
        {
            MaxDegreeOfParallelism = _maxParallelism,
            StopOnFirstError = _stopOnFirstError
        };

        var differ = _differ ?? new StreamingHdiffDiffer();
        return new DiffPipeline(options, differ, _cleanMatcher, _dirtyMatcher, _progress);
    }
}
