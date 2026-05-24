using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Binary;
using GeneralUpdate.Differential.Pipeline;
using GeneralUpdate.Differential.Models;
using Xunit;

namespace DifferentialTest.Pipeline
{
    /// <summary>
    /// Test cases for DiffPipeline and DiffPipelineBuilder â€?parallel processing,
    /// progress reporting, IBinaryDiffer injection, and cancellation.
    /// </summary>
    public class DiffPipelineTests : IDisposable
    {
        private readonly string _testDir;

        public DiffPipelineTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"PipelineTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
        }

        [Fact]
        public void Builder_WithDefaults_ProducesValidPipeline()
        {
            var pipeline = new DiffPipelineBuilder().Build();
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void Builder_WithCustomDiffer_UsesProvidedDiffer()
        {
            var differ = new BinaryHandler();
            var pipeline = new DiffPipelineBuilder().UseDiffer(differ).Build();
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void Builder_WithParallelism_SetsCorrectly()
        {
            var pipeline = new DiffPipelineBuilder()
                .WithParallelism(2)
                .Build();
            Assert.NotNull(pipeline);
        }

        [Fact]
        public async Task CleanAsync_WithProgress_ReportsCompletion()
        {
            var src = Path.Combine(_testDir, "src");
            var tgt = Path.Combine(_testDir, "tgt");
            var patch = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            File.WriteAllText(Path.Combine(src, "a.txt"), "old");
            File.WriteAllText(Path.Combine(tgt, "a.txt"), "new");
            File.WriteAllText(Path.Combine(tgt, "b.txt"), "added");

            var pipeline = new DiffPipelineBuilder()
                .WithParallelism(1)
                .Build();

            var reporter = new SyncProgress<DiffProgress>();
            await pipeline.CleanAsync(src, tgt, patch, reporter);

            Assert.True(reporter.LastValue.IsComplete);
            Assert.True(reporter.LastValue.Total > 0);
            Assert.Equal(reporter.LastValue.Total, reporter.LastValue.Completed);
        }

        [Fact]
        public async Task DirtyAsync_WithProgress_ReportsCompletion()
        {
            var app = Path.Combine(_testDir, "app");
            var patch = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(app);
            Directory.CreateDirectory(patch);

            File.WriteAllText(Path.Combine(app, "x.txt"), "old data");

            var src = Path.Combine(_testDir, "src");
            var tgt = Path.Combine(_testDir, "tgt");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            File.WriteAllText(Path.Combine(src, "x.txt"), "old data");
            File.WriteAllText(Path.Combine(tgt, "x.txt"), "new data");

            var genPipeline = new DiffPipelineBuilder().WithParallelism(1).Build();
            await genPipeline.CleanAsync(src, tgt, patch);

            var pipeline = new DiffPipelineBuilder().WithParallelism(1).Build();
            var reporter = new SyncProgress<DiffProgress>();
            await pipeline.DirtyAsync(app, patch, reporter);

            Assert.True(reporter.LastValue.IsComplete);
            Assert.True(reporter.LastValue.Total > 0);
            Assert.Equal(reporter.LastValue.Total, reporter.LastValue.Completed);
            Assert.Equal("new data", File.ReadAllText(Path.Combine(app, "x.txt")));
        }

        [Fact]
        public async Task CleanAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            var src = Path.Combine(_testDir, "src");
            var tgt = Path.Combine(_testDir, "tgt");
            var patch = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            for (int i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(src, $"f{i}.txt"), $"old_{i}");
                File.WriteAllText(Path.Combine(tgt, $"f{i}.txt"), $"new_{i}");
            }

            var pipeline = new DiffPipelineBuilder().WithParallelism(2).Build();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                pipeline.CleanAsync(src, tgt, patch, cancellationToken: cts.Token));
        }

        [Fact]
        public void DiffProgress_Properties_CalculateCorrectly()
        {
            var p = new DiffProgress(5, 10, "file.txt");
            Assert.Equal(5, p.Completed);
            Assert.Equal(10, p.Total);
            Assert.Equal(50.0, p.Percentage);
            Assert.False(p.IsComplete);
            Assert.Equal("file.txt", p.CurrentFile);
            Assert.Null(p.Error);
        }

        [Fact]
        public void DiffProgress_Complete_IsComplete()
        {
            var p = DiffProgress.Complete(10);
            Assert.True(p.IsComplete);
            Assert.Equal(10, p.Completed);
        }

        [Fact]
        public void DiffProgress_WithError_ReportsError()
        {
            var p = new DiffProgress(3, 5, "bad.txt", "Access denied");
            Assert.Equal("Access denied", p.Error);
        }

        /// <summary>
        /// Synchronous IProgress implementation for deterministic test assertions.
        /// Unlike System.Progress, callbacks fire immediately during Report().
        /// </summary>
        private sealed class SyncProgress<T> : IProgress<T>
        {
            public T LastValue { get; private set; } = default!;

            public void Report(T value)
            {
                LastValue = value;
            }
        }
    }
}
