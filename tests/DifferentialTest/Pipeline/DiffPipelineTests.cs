using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Models;
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
            var differ = new BsdiffDiffer();
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

        [Fact]
        public async Task DirtyAsync_DeleteList_RemovesByRelativePath_KeepsDuplicateContentElsewhere()
        {
            // Regression: the delete manifest used to be applied by CONTENT HASH alone,
            // so duplicated content (same bytes in two directories) was deleted everywhere
            // even when the manifest only asked to remove one copy. Deletion must follow
            // the manifest's RelativePath; a same-content file outside the listed paths
            // must survive.
            // Arrange ¡ª "lib.dll" content exists in two places; manifest removes one.
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(Path.Combine(appDir, "plugins"));
            Directory.CreateDirectory(patchDir);

            var content = "identical payload";
            var listedFile = Path.Combine(appDir, "plugins", "lib.dll");
            var duplicateFile = Path.Combine(appDir, "lib.dll");
            File.WriteAllText(listedFile, content);
            File.WriteAllText(duplicateFile, content);

            var deleteNodes = new List<GeneralUpdate.Core.FileSystem.FileNode>
            {
                new() { Name = "lib.dll", FullName = listedFile, RelativePath = "plugins/lib.dll", Hash = "ignored" }
            };
            GeneralUpdate.Core.FileSystem.StorageManager.CreateJson(
                Path.Combine(patchDir, "generalupdate.delete.json"),
                deleteNodes,
                GeneralUpdate.Core.JsonContext.FileNodesJsonContext.Default.ListFileNode);

            var pipeline = new DiffPipeline();

            // Act
            await pipeline.DirtyAsync(appDir, patchDir);

            // Assert ¡ª the listed copy is gone; the duplicate survives.
            Assert.False(File.Exists(listedFile), "listed file should have been deleted");
            Assert.True(File.Exists(duplicateFile), "same-content file outside the manifest must be kept");
        }

        [Fact]
        public async Task DirtyAsync_DeleteList_WithoutRelativePaths_FallsBackToHash()
        {
            // Legacy manifest compatibility: entries without RelativePath still delete by
            // content hash (the original behavior), keeping old patch packages working.
            // Arrange
            var appDir = Path.Combine(_testDir, "app2");
            var patchDir = Path.Combine(_testDir, "patch2");
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            var target = Path.Combine(appDir, "obsolete.dll");
            File.WriteAllText(target, "to be deleted");

            var hash = new GeneralUpdate.Core.HashAlgorithms.Sha256HashAlgorithm().ComputeHash(target);
            var deleteNodes = new List<GeneralUpdate.Core.FileSystem.FileNode>
            {
                new() { Name = "obsolete.dll", FullName = target, RelativePath = null, Hash = hash }
            };
            GeneralUpdate.Core.FileSystem.StorageManager.CreateJson(
                Path.Combine(patchDir, "generalupdate.delete.json"),
                deleteNodes,
                GeneralUpdate.Core.JsonContext.FileNodesJsonContext.Default.ListFileNode);

            var pipeline = new DiffPipeline();

            // Act
            await pipeline.DirtyAsync(appDir, patchDir);

            // Assert
            Assert.False(File.Exists(target), "legacy hash-only manifest should still delete the file");
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
