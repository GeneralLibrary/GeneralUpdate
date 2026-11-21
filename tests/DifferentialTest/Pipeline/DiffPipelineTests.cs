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

        [Fact]
        public async Task DirtyAsync_DeleteList_UriEscapedRelativePath_MatchesLiteralFile()
        {
            // Regression: StorageManager-relative paths are URI-escaped (Uri.MakeRelativeUri,
            // e.g. "lib#1.dll" stored as "lib%231.dll") while paths computed from the file
            // system are literal. Because relative paths were present, hash fallback was
            // disabled and the obsolete file silently stayed installed.
            // Arrange
            var appDir = Path.Combine(_testDir, "app-escaped");
            var patchDir = Path.Combine(_testDir, "patch-escaped");
            Directory.CreateDirectory(Path.Combine(appDir, "plugins"));
            Directory.CreateDirectory(patchDir);

            var target = Path.Combine(appDir, "plugins", "lib#1.dll");
            File.WriteAllText(target, "obsolete plugin");

            var deleteNodes = new List<GeneralUpdate.Core.FileSystem.FileNode>
            {
                new() { Name = "lib%231.dll", FullName = target, RelativePath = "plugins/lib%231.dll" }
            };
            GeneralUpdate.Core.FileSystem.StorageManager.CreateJson(
                Path.Combine(patchDir, "generalupdate.delete.json"),
                deleteNodes,
                GeneralUpdate.Core.JsonContext.FileNodesJsonContext.Default.ListFileNode);

            var pipeline = new DiffPipeline();

            // Act
            await pipeline.DirtyAsync(appDir, patchDir);

            // Assert
            Assert.False(File.Exists(target), "URI-escaped manifest path should match the literal file name");
        }

        [Fact]
        public async Task DirtyAsync_NonCanonicalAppPath_StripsPrefixBeforeMatching()
        {
            // Regression: the app path arrives as given (may be relative or contain "."
            // segments) while enumerated FullName values are absolute ¡ª a non-canonical base
            // failed the prefix test and reduced every path to a bare file name, which never
            // matched the deletion manifest.
            // Arrange
            var appDir = Path.Combine(_testDir, "app-dot");
            Directory.CreateDirectory(Path.Combine(appDir, "sub"));
            var patchDir = Path.Combine(_testDir, "patch-dot");
            Directory.CreateDirectory(patchDir);

            var target = Path.Combine(appDir, "sub", "gone.txt");
            File.WriteAllText(target, "obsolete");

            var nonCanonicalApp = Path.Combine(_testDir, ".", "app-dot");

            var deleteNodes = new List<GeneralUpdate.Core.FileSystem.FileNode>
            {
                new() { Name = "gone.txt", FullName = target, RelativePath = "sub/gone.txt" }
            };
            GeneralUpdate.Core.FileSystem.StorageManager.CreateJson(
                Path.Combine(patchDir, "generalupdate.delete.json"),
                deleteNodes,
                GeneralUpdate.Core.JsonContext.FileNodesJsonContext.Default.ListFileNode);

            var pipeline = new DiffPipeline();

            // Act
            await pipeline.DirtyAsync(nonCanonicalApp, patchDir);

            // Assert
            Assert.False(File.Exists(target), "non-canonical app path should still strip the prefix correctly");
        }

        [Fact]
        public async Task CleanAsync_CustomCleanMatcher_ReturnsNullMatch_PackagesFileWhole()
        {
            // Regression: the relative-path index shortcut bypassed the injected matcher, so
            // a custom ICleanMatcher whose Match deliberately returns null (package the file
            // whole / select a different baseline) got a patch against the indexed file
            // anyway. Only the EXACT default matcher may use the index shortcut.
            // Arrange
            var src = Path.Combine(_testDir, "src-custom");
            var tgt = Path.Combine(_testDir, "tgt-custom");
            var patch = Path.Combine(_testDir, "patch-custom");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            Directory.CreateDirectory(patch);

            File.WriteAllText(Path.Combine(src, "a.txt"), "old");
            File.WriteAllText(Path.Combine(tgt, "a.txt"), "new");

            var pipeline = new DiffPipeline(
                new DiffPipelineOptions(),
                new StreamingHdiffDiffer(),
                cleanMatcher: new NullCleanMatcher());

            // Act
            await pipeline.CleanAsync(src, tgt, patch);

            // Assert ¡ª Match() returned null ¡ú the new file is copied WHOLE, no patch emitted.
            Assert.True(File.Exists(Path.Combine(patch, "a.txt")), "custom matcher's null match must package the file whole");
            Assert.False(File.Exists(Path.Combine(patch, "a.txt.patch")), "custom matcher must not be bypassed by the relative-path index");
        }

        /// <summary>
        /// A custom matcher that deliberately never matches ¡ª the pipeline must respect its
        /// null verdicts (package files whole), not bypass it with the relative-path index.
        /// </summary>
        private sealed class NullCleanMatcher : GeneralUpdate.Core.Differential.ICleanMatcher
        {
            private readonly GeneralUpdate.Core.FileSystem.StorageManager _storage = new();

            public GeneralUpdate.Core.FileSystem.ComparisonResult Compare(string sourcePath, string targetPath)
                => _storage.Compare(sourcePath, targetPath);

            public IEnumerable<GeneralUpdate.Core.FileSystem.FileNode>? Except(string sourcePath, string targetPath)
                => _storage.Except(sourcePath, targetPath);

            public GeneralUpdate.Core.FileSystem.FileNode? Match(
                GeneralUpdate.Core.FileSystem.FileNode newFile,
                IEnumerable<GeneralUpdate.Core.FileSystem.FileNode> leftNodes)
                => null;
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
