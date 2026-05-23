using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;
using GeneralUpdate.Differential.Models;

namespace GeneralUpdate.Differential.Pipeline
{
    /// <summary>
    /// Fluent builder for <see cref="DiffPipeline"/> instances.
    /// </summary>
    /// <remarks>
    /// Usage:
    /// <code>
    /// var pipeline = new DiffPipelineBuilder()
    ///     .UseDiffer(new StreamingHdiffDiffer())
    ///     .WithParallelism(Environment.ProcessorCount)
    ///     .WithProgress(progress => Console.WriteLine(progress))
    ///     .Build();
    /// await pipeline.CleanAsync(src, tgt, patch, cancellationToken);
    /// </code>
    /// </remarks>
    public class DiffPipelineBuilder
    {
        private IBinaryDiffer? _differ;
        private int _maxParallelism = Environment.ProcessorCount;
        private bool _stopOnFirstError;
        private IProgress<DiffProgress>? _progress;

        /// <summary>
        /// Sets the binary differ to use. Defaults to <see cref="Differ.StreamingHdiffDiffer"/> if not set.
        /// </summary>
        public DiffPipelineBuilder UseDiffer(IBinaryDiffer differ)
        {
            _differ = differ ?? throw new ArgumentNullException(nameof(differ));
            return this;
        }

        /// <summary>
        /// Sets the maximum degree of parallelism for file processing.
        /// Default: <see cref="Environment.ProcessorCount"/>.
        /// </summary>
        public DiffPipelineBuilder WithParallelism(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1)
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            _maxParallelism = maxDegreeOfParallelism;
            return this;
        }

        /// <summary>
        /// Sets whether to stop processing on the first file error.
        /// Default: false (continue processing, report errors via progress).
        /// </summary>
        public DiffPipelineBuilder WithStopOnFirstError(bool stopOnFirstError = true)
        {
            _stopOnFirstError = stopOnFirstError;
            return this;
        }

        /// <summary>
        /// Attaches a progress reporter for real-time file-level status updates.
        /// </summary>
        public DiffPipelineBuilder WithProgress(IProgress<DiffProgress> progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            return this;
        }

        /// <summary>
        /// Builds the configured <see cref="DiffPipeline"/>.
        /// </summary>
        public DiffPipeline Build()
        {
            var options = new DiffPipelineOptions
            {
                MaxDegreeOfParallelism = _maxParallelism,
                StopOnFirstError = _stopOnFirstError
            };

            var differ = _differ ?? new Differ.StreamingHdiffDiffer();
            return new DiffPipeline(options, differ, _progress);
        }
    }
}
