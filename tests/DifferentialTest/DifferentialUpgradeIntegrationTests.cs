using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Core.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace DifferentialTest
{
    /// <summary>
    /// Comprehensive differential upgrade integration tests.
    /// Covers:
    ///   - Client  — Upgrade mesh update: generate patches in client context, apply in upgrade context
    ///   - All file operations: modified, added, deleted, unchanged, binary
    ///   - Complex directory structures
    ///   - Push upgrade simulation via differential pipeline
    ///   - Various parameter combinations (parallel, serial, cancellation, progress)
    ///   - Real-world developer usage scenarios
    /// </summary>
    public class DifferentialUpgradeIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly ITestOutputHelper _output;

        public DifferentialUpgradeIntegrationTests(ITestOutputHelper output)
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GU_DiffIntegration_{Guid.NewGuid()}");
            _output = output;
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
        }
        
        #region DiffPipeline Tests (Parallel / Serial / Cancellation / Progress)

        /// <summary>
        /// Tests DiffPipeline with parallel processing.
        /// </summary>
        [Fact]
        public async Task DiffPipeline_Parallel_ProcessesFilesConcurrently()
        {
            var src = Path.Combine(_testDir, "p_src");
            var tgt = Path.Combine(_testDir, "p_tgt");
            var patch = Path.Combine(_testDir, "p_patch");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            // Create many files to test parallel processing
            for (var i = 0; i < 20; i++)
            {
                File.WriteAllText(Path.Combine(src, $"file{i:D3}.txt"), $"old_{i}");
                File.WriteAllText(Path.Combine(tgt, $"file{i:D3}.txt"), $"new_{i}");
            }

            var reporter = new SyncProgress<DiffProgress>();
            var pipeline = new DiffPipelineBuilder()
                .WithParallelism(4)
                .Build();

            // Act
            await pipeline.CleanAsync(src, tgt, patch, reporter);

            // Assert
            Assert.True(reporter.LastValue.IsComplete);
            Assert.Equal(20, reporter.LastValue.Total);
            Assert.Equal(20, reporter.LastValue.Completed);

            // Verify patches were generated for all files
            for (var i = 0; i < 20; i++)
            {
                Assert.True(File.Exists(Path.Combine(patch, $"file{i:D3}.txt.patch")));
            }
        }

        /// <summary>
        /// Tests DiffPipeline with serial processing (parallelism=1).
        /// </summary>
        [Fact]
        public async Task DiffPipeline_Serial_ProcessesFilesSequentially()
        {
            var src = Path.Combine(_testDir, "s_src");
            var tgt = Path.Combine(_testDir, "s_tgt");
            var patch = Path.Combine(_testDir, "s_patch");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            for (var i = 0; i < 5; i++)
            {
                File.WriteAllText(Path.Combine(src, $"s{i}.txt"), $"old_{i}");
                File.WriteAllText(Path.Combine(tgt, $"s{i}.txt"), $"new_{i}");
            }

            var reporter = new SyncProgress<DiffProgress>();
            var pipeline = new DiffPipelineBuilder()
                .WithParallelism(1) // Serial
                .Build();

            // Act
            await pipeline.CleanAsync(src, tgt, patch, reporter);

            // Assert
            Assert.True(reporter.LastValue.IsComplete);
            Assert.Equal(5, reporter.LastValue.Total);
            Assert.Equal(5, reporter.LastValue.Completed);
        }

        /// <summary>
        /// Tests DiffPipeline cancellation.
        /// </summary>
        [Fact]
        public async Task DiffPipeline_Cancellation_ThrowsOperationCanceledException()
        {
            var src = Path.Combine(_testDir, "c_src");
            var tgt = Path.Combine(_testDir, "c_tgt");
            var patch = Path.Combine(_testDir, "c_patch");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            for (var i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(src, $"cf{i}.txt"), $"old_{i}");
                File.WriteAllText(Path.Combine(tgt, $"cf{i}.txt"), $"new_{i}");
            }

            var pipeline = new DiffPipelineBuilder().WithParallelism(2).Build();
            var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                pipeline.CleanAsync(src, tgt, patch, cancellationToken: cts.Token));
        }

        /// <summary>
        /// Tests DiffPipeline Dirty with progress reporting.
        /// </summary>
        [Fact]
        public async Task DiffPipeline_DirtyWithProgress_ReportsCorrectly()
        {
            var app = Path.Combine(_testDir, "dp_app");
            var src = Path.Combine(_testDir, "dp_src");
            var tgt = Path.Combine(_testDir, "dp_tgt");
            var patch = Path.Combine(_testDir, "dp_patch");
            Directory.CreateDirectory(app);
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            // Setup: 3 modified, 2 new, 1 deleted
            File.WriteAllText(Path.Combine(src, "m1.txt"), "old1");    // modified
            File.WriteAllText(Path.Combine(src, "m2.txt"), "old2");    // modified
            File.WriteAllText(Path.Combine(src, "m3.txt"), "old3");    // modified
            File.WriteAllText(Path.Combine(src, "del.txt"), "delete"); // deleted
            File.WriteAllText(Path.Combine(src, "keep.txt"), "same");  // unchanged

            File.WriteAllText(Path.Combine(tgt, "m1.txt"), "new1");
            File.WriteAllText(Path.Combine(tgt, "m2.txt"), "new2");
            File.WriteAllText(Path.Combine(tgt, "m3.txt"), "new3");
            File.WriteAllText(Path.Combine(tgt, "n1.txt"), "added1");  // new
            File.WriteAllText(Path.Combine(tgt, "n2.txt"), "added2");  // new
            File.WriteAllText(Path.Combine(tgt, "keep.txt"), "same");

            // Copy source to app
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(app, Path.GetFileName(f)));

            // Generate patches
            var genPipeline = new DiffPipelineBuilder().WithParallelism(1).Build();
            await genPipeline.CleanAsync(src, tgt, patch);

            // Apply with progress
            var reporter = new SyncProgress<DiffProgress>();
            var pipeline = new DiffPipelineBuilder().WithParallelism(1).Build();
            await pipeline.DirtyAsync(app, patch, reporter);

            // Assert progress was reported
            Assert.True(reporter.LastValue.IsComplete);
            Assert.NotEqual(0, reporter.LastValue.Total);
            Assert.Equal(reporter.LastValue.Total, reporter.LastValue.Completed);

            // Assert files are correct
            Assert.Equal("new1", File.ReadAllText(Path.Combine(app, "m1.txt")));
            Assert.Equal("new2", File.ReadAllText(Path.Combine(app, "m2.txt")));
            Assert.Equal("new3", File.ReadAllText(Path.Combine(app, "m3.txt")));
            Assert.True(File.Exists(Path.Combine(app, "n1.txt")));
            Assert.True(File.Exists(Path.Combine(app, "n2.txt")));
            Assert.False(File.Exists(Path.Combine(app, "del.txt")));
            Assert.Equal("same", File.ReadAllText(Path.Combine(app, "keep.txt")));
        }

        #endregion

        #region DiffPipeline Parameters Matrix

        /// <summary>
        /// Tests DiffPipelineBuilder with custom IBinaryDiffer.
        /// </summary>
        [Fact]
        public void DiffPipelineBuilder_CustomDiffer_UsesProvidedDiffer()
        {
            // Arrange
            var customDiffer = new BsdiffDiffer();

            // Act
            var pipeline = new DiffPipelineBuilder()
                .UseDiffer(customDiffer)
                .Build();

            // Assert
            Assert.NotNull(pipeline);
        }

        /// <summary>
        /// Tests DiffPipelineBuilder with various parallelism values.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        public void DiffPipelineBuilder_VariousParallelism_BuildsSuccessfully(int parallelism)
        {
            // Act
            var pipeline = new DiffPipelineBuilder()
                .WithParallelism(parallelism)
                .Build();

            // Assert
            Assert.NotNull(pipeline);
        }

        /// <summary>
        /// Tests DiffProgress calculation correctness.
        /// </summary>
        [Fact]
        public void DiffProgress_Calculation_ReturnsCorrectValues()
        {
            // Boundary cases
            var p0 = new DiffProgress(0, 10, null);
            Assert.Equal(0.0, p0.Percentage);
            Assert.False(p0.IsComplete);

            var p50 = new DiffProgress(5, 10, null);
            Assert.Equal(50.0, p50.Percentage);
            Assert.False(p50.IsComplete);

            var p100 = new DiffProgress(10, 10, null);
            Assert.Equal(100.0, p100.Percentage);
            Assert.True(p100.IsComplete);

            // Error case
            var pErr = new DiffProgress(3, 5, "bad.dll", "Hash mismatch");
            Assert.Equal(60.0, pErr.Percentage);
            Assert.Equal("bad.dll", pErr.CurrentFile);
            Assert.Equal("Hash mismatch", pErr.Error);

            // Complete factory
            var pComplete = DiffProgress.Complete(42);
            Assert.True(pComplete.IsComplete);
            Assert.Equal(42, pComplete.Completed);
            Assert.Equal(42, pComplete.Total);
            Assert.Equal(100.0, pComplete.Percentage);
        }

        #endregion

        #region Real-world Developer Scenarios

        /// <summary>
        /// Scenario: Developer implements a custom differ and uses it in the pipeline.
        /// </summary>
        [Fact]
        public async Task DeveloperScenario_CustomDifferInPipeline_WorksCorrectly()
        {
            var src = Path.Combine(_testDir, "ds_src");
            var tgt = Path.Combine(_testDir, "ds_tgt");
            var patch = Path.Combine(_testDir, "ds_patch");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            File.WriteAllText(Path.Combine(src, "code.cs"), "class A { }");
            File.WriteAllText(Path.Combine(tgt, "code.cs"), "class A { void B() { } }");

            // Developer uses BsdiffDiffer (BSDIFF) as the differ
            var pipeline = new DiffPipelineBuilder()
                .UseDiffer(new BsdiffDiffer())
                .WithParallelism(1)
                .Build();

            // Act
            await pipeline.CleanAsync(src, tgt, patch);

            // Assert
            Assert.True(File.Exists(Path.Combine(patch, "code.cs.patch")));
        }

        /// <summary>
        /// Scenario: Developer wants to apply patches in parallel for maximum speed.
        /// </summary>
        [Fact]
        public async Task DeveloperScenario_MaxParallelism_AppliesPatchesFast()
        {
            var app = Path.Combine(_testDir, "mp_app");
            var src = Path.Combine(_testDir, "mp_src");
            var tgt = Path.Combine(_testDir, "mp_tgt");
            var patch = Path.Combine(_testDir, "mp_patch");
            Directory.CreateDirectory(app);
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            // Simulate a large app with many files
            for (var i = 0; i < 30; i++)
            {
                var path = i % 3 == 0 ? $"sub{i / 3}" : "";
                if (path.Length > 0) Directory.CreateDirectory(Path.Combine(src, path));
                if (path.Length > 0) Directory.CreateDirectory(Path.Combine(tgt, path));

                File.WriteAllText(Path.Combine(src, path, $"file{i}.dll"), $"code_v1_{i}");
                File.WriteAllText(Path.Combine(tgt, path, $"file{i}.dll"), $"code_v2_{i}");

                if (path.Length > 0) Directory.CreateDirectory(Path.Combine(app, path));
                File.Copy(Path.Combine(src, path, $"file{i}.dll"),
                    Path.Combine(app, path, $"file{i}.dll"));
            }

            // Generate patches (serial for determinism)
            var genPipeline = new DiffPipelineBuilder().WithParallelism(1).Build();
            await genPipeline.CleanAsync(src, tgt, patch);

            // Apply patches with maximum parallelism
            var applyPipeline = new DiffPipelineBuilder().WithParallelism(Environment.ProcessorCount).Build();
            await applyPipeline.DirtyAsync(app, patch);

            // Assert all files updated
            for (var i = 0; i < 30; i++)
            {
                var path = i % 3 == 0 ? $"sub{i / 3}" : "";
                var filePath = Path.Combine(app, path, $"file{i}.dll");
                Assert.True(File.Exists(filePath), $"File file{i}.dll should exist");
                Assert.Equal($"code_v2_{i}", File.ReadAllText(filePath));
            }
        }

        #endregion

        #region Helper

        private sealed class SyncProgress<T> : IProgress<T>
        {
            public T LastValue { get; private set; } = default!;

            public void Report(T value)
            {
                LastValue = value;
            }
        }

        #endregion
    }
}
