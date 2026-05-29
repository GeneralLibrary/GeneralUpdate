using System.IO.Compression;
using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Core.Pipeline;

namespace DifferentialTest.Pipeline
{
    /// <summary>
    /// DiffPipeline 集成测试：端到端验证 Clean/Dirty 管线的文件级 patch 生成与应用。
    /// 每个测试创建临时目录，在 Dispose 中清理。
    /// </summary>
    public class DiffPipelineIntegrationTests : IDisposable
    {
        private readonly string _testDir;

        public DiffPipelineIntegrationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"DPI_IT_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // ---- helpers ----

        private string GetPath(string relative) => Path.Combine(_testDir, relative);

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        private static (IProgress<DiffProgress> Progress, List<DiffProgress> Captured) CreateProgressCapture()
        {
            var captured = new List<DiffProgress>();
            var progress = new Progress<DiffProgress>(p => captured.Add(p));
            return (progress, captured);
        }

        // ================================================================
        // CleanAsync 集成测试
        // ================================================================

        [Fact(DisplayName = "CleanAsync_单个修改文件_生成补丁文件")]
        public async Task CleanAsync_SingleModifiedFile_GeneratesPatch()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var oldData = new byte[200];
            new Random(1).NextBytes(oldData);
            var newData = new byte[200];
            oldData.CopyTo(newData, 0);
            newData[50] ^= 0xFF; // modify one byte

            File.WriteAllBytes(Path.Combine(sourceDir, "file.bin"), oldData);
            File.WriteAllBytes(Path.Combine(targetDir, "file.bin"), newData);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert
            var patchFile = Path.Combine(patchDir, "file.bin.patch");
            Assert.True(File.Exists(patchFile));
            Assert.True(new FileInfo(patchFile).Length > 0);
        }

        [Fact(DisplayName = "CleanAsync_多文件_生成所有补丁且不处理无变更文件")]
        public async Task CleanAsync_MultipleFiles_GeneratesAllPatches()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var rng = new Random(42);
            var shared = new byte[100];
            rng.NextBytes(shared);

            // 3 modified files
            byte[] mod1Old = new byte[100]; shared.CopyTo(mod1Old, 0);
            byte[] mod1New = new byte[100]; shared.CopyTo(mod1New, 0); mod1New[30] = 88;

            var mod2Old = new byte[200]; rng.NextBytes(mod2Old);
            var mod2New = new byte[200]; rng.NextBytes(mod2New);

            var mod3Old = new byte[50]; rng.NextBytes(mod3Old);
            var mod3New = new byte[50]; rng.NextBytes(mod3New);

            // 1 identical file
            var ident = new byte[150]; rng.NextBytes(ident);

            // 1 new file (only in target)
            var newFileData = new byte[80]; rng.NextBytes(newFileData);

            File.WriteAllBytes(Path.Combine(sourceDir, "mod1.bin"), mod1Old);
            File.WriteAllBytes(Path.Combine(sourceDir, "mod2.bin"), mod2Old);
            File.WriteAllBytes(Path.Combine(sourceDir, "mod3.bin"), mod3Old);
            File.WriteAllBytes(Path.Combine(sourceDir, "ident.bin"), ident);
            // "new1.bin" intentionally NOT written to source

            File.WriteAllBytes(Path.Combine(targetDir, "mod1.bin"), mod1New);
            File.WriteAllBytes(Path.Combine(targetDir, "mod2.bin"), mod2New);
            File.WriteAllBytes(Path.Combine(targetDir, "mod3.bin"), mod3New);
            File.WriteAllBytes(Path.Combine(targetDir, "ident.bin"), ident);
            File.WriteAllBytes(Path.Combine(targetDir, "new1.bin"), newFileData);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert
            // 3 modified → .patch files
            Assert.True(File.Exists(Path.Combine(patchDir, "mod1.bin.patch")));
            Assert.True(File.Exists(Path.Combine(patchDir, "mod2.bin.patch")));
            Assert.True(File.Exists(Path.Combine(patchDir, "mod3.bin.patch")));
            // 1 new → copied directly (not patched)
            Assert.True(File.Exists(Path.Combine(patchDir, "new1.bin")));
            // 1 identical → no output
            Assert.False(File.Exists(Path.Combine(patchDir, "ident.bin.patch")));
            Assert.False(File.Exists(Path.Combine(patchDir, "ident.bin")));
        }

        [Fact(DisplayName = "CleanAsync_目标有新文件_直接复制而非打补丁")]
        public async Task CleanAsync_NewFile_CopiedDirectly()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var data = new byte[120];
            new Random(7).NextBytes(data);

            // source has NO newfile.bin
            File.WriteAllBytes(Path.Combine(targetDir, "newfile.bin"), data);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert: copied, not patched
            var copied = Path.Combine(patchDir, "newfile.bin");
            var patched = Path.Combine(patchDir, "newfile.bin.patch");
            Assert.True(File.Exists(copied));
            Assert.False(File.Exists(patched));
            Assert.Equal(data, File.ReadAllBytes(copied));
        }

        [Fact(DisplayName = "CleanAsync_相同文件_不生成补丁也不复制")]
        public async Task CleanAsync_IdenticalFile_NoPatch()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var data = new byte[80];
            new Random(5).NextBytes(data);
            File.WriteAllBytes(Path.Combine(sourceDir, "ident.bin"), data);
            File.WriteAllBytes(Path.Combine(targetDir, "ident.bin"), data);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert: no .patch and no copy for unchanged file
            Assert.False(File.Exists(Path.Combine(patchDir, "ident.bin.patch")));
            Assert.False(File.Exists(Path.Combine(patchDir, "ident.bin")));
        }

        [Fact(DisplayName = "CleanAsync_源有目标无_写入删除列表JSON")]
        public async Task CleanAsync_DeletedFile_WritesDeleteJson()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var data = new byte[60];
            new Random(3).NextBytes(data);
            File.WriteAllBytes(Path.Combine(sourceDir, "delete_me.bin"), data);
            // target is intentionally empty (no delete_me.bin)

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert
            var deleteJson = Path.Combine(patchDir, "generalupdate.delete.json");
            Assert.True(File.Exists(deleteJson));
            var content = File.ReadAllText(deleteJson);
            Assert.NotEmpty(content);
        }

        [Fact(DisplayName = "CleanAsync_进度回调_最终Completed等于Total")]
        public async Task CleanAsync_WithProgress_ReportsProgress()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var rng = new Random(12);
            for (int i = 1; i <= 5; i++)
            {
                var oldData = new byte[100]; rng.NextBytes(oldData);
                var newData = new byte[100]; rng.NextBytes(newData);
                File.WriteAllBytes(Path.Combine(sourceDir, $"f{i}.bin"), oldData);
                File.WriteAllBytes(Path.Combine(targetDir, $"f{i}.bin"), newData);
            }

            var (progress, captured) = CreateProgressCapture();
            var pipeline = new DiffPipeline(
                new DiffPipelineOptions { MaxDegreeOfParallelism = 1 },
                new GeneralUpdate.Differential.Differ.StreamingHdiffDiffer(),
                progress: progress);

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert
            Assert.NotEmpty(captured);
            var last = captured[^1];
            Assert.True(last.IsComplete);
            Assert.Equal(5, last.Completed);
            Assert.Equal(5, last.Total);
        }

        [Fact(DisplayName = "CleanAsync_已取消令牌_抛出OperationCanceledException")]
        public async Task CleanAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(targetDir, "a.bin"), new byte[] { 4, 5, 6 });

            var pipeline = new DiffPipeline();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                pipeline.CleanAsync(sourceDir, targetDir, patchDir, cancellationToken: cts.Token));
        }

        [Fact(DisplayName = "CleanAsync_StopOnFirstError为false_一个文件出错其他继续")]
        public async Task CleanAsync_StopOnFirstError_False_ContinuesOnError()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var rng = new Random(99);
            var aOld = new byte[100]; rng.NextBytes(aOld);
            var aNew = new byte[100]; rng.NextBytes(aNew);
            var bOld = new byte[80]; rng.NextBytes(bOld);
            var bNew = new byte[80]; rng.NextBytes(bNew);

            File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), aOld);
            File.WriteAllBytes(Path.Combine(sourceDir, "b.bin"), bOld);
            File.WriteAllBytes(Path.Combine(targetDir, "a.bin"), aNew);
            File.WriteAllBytes(Path.Combine(targetDir, "b.bin"), bNew);

            // Mock differ: a.bin fails, b.bin succeeds
            var mockDiffer = new Mock<IBinaryDiffer>();
            mockDiffer
                .Setup(d => d.CleanAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string old, string _, string patch, CancellationToken ct) =>
                {
                    if (Path.GetFileName(old) == "a.bin")
                        return Task.FromException(new IOException("Simulated error for a.bin"));

                    if (!File.Exists(patch))
                        File.WriteAllBytes(patch, new byte[10]);
                    return Task.CompletedTask;
                });

            var (progress, captured) = CreateProgressCapture();
            var options = new DiffPipelineOptions { MaxDegreeOfParallelism = 1, StopOnFirstError = false };
            var pipeline = new DiffPipeline(options, mockDiffer.Object, progress: progress);

            // Act
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Assert
            // a.bin → error, no patch
            Assert.False(File.Exists(Path.Combine(patchDir, "a.bin.patch")));
            // b.bin → succeeded, patch exists
            Assert.True(File.Exists(Path.Combine(patchDir, "b.bin.patch")));

            // progress should include an error entry and complete
            var errors = captured.Where(p => p.Error != null).ToList();
            Assert.NotEmpty(errors);
        }

        // ================================================================
        // DirtyAsync 集成测试
        // ================================================================

        [Fact(DisplayName = "DirtyAsync_应用多个补丁_正确更新文件")]
        public async Task DirtyAsync_AppliesMultiplePatches_Correctly()
        {
            // Arrange — Phase 1: generate patches via Clean
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            var appDir = GetPath("app");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(appDir);

            var rng = new Random(42);
            var aOld = new byte[200]; rng.NextBytes(aOld);
            var aNew = new byte[200]; rng.NextBytes(aNew);
            var bOld = new byte[300]; rng.NextBytes(bOld);
            var bNew = new byte[300]; rng.NextBytes(bNew);

            File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), aOld);
            File.WriteAllBytes(Path.Combine(sourceDir, "b.bin"), bOld);
            File.WriteAllBytes(Path.Combine(targetDir, "a.bin"), aNew);
            File.WriteAllBytes(Path.Combine(targetDir, "b.bin"), bNew);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Verify patches were generated
            Assert.True(File.Exists(Path.Combine(patchDir, "a.bin.patch")));
            Assert.True(File.Exists(Path.Combine(patchDir, "b.bin.patch")));

            // Phase 2: set up app dir with old files, patch dir copy
            File.WriteAllBytes(Path.Combine(appDir, "a.bin"), aOld);
            File.WriteAllBytes(Path.Combine(appDir, "b.bin"), bOld);

            var patch2Dir = GetPath("patch2");
            CopyDirectory(patchDir, patch2Dir);

            // Act
            await pipeline.DirtyAsync(appDir, patch2Dir);

            // Assert: app files now match target versions
            Assert.Equal(aNew, File.ReadAllBytes(Path.Combine(appDir, "a.bin")));
            Assert.Equal(bNew, File.ReadAllBytes(Path.Combine(appDir, "b.bin")));
        }

        [Fact(DisplayName = "DirtyAsync_有删除列表_删除标记文件")]
        public async Task DirtyAsync_WithDeleteList_DeletesMarkedFiles()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            var appDir = GetPath("app");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(appDir);

            var keepData = new byte[60];
            var deleteData = new byte[80];
            new Random(1).NextBytes(keepData);
            new Random(2).NextBytes(deleteData);

            // Source has delete_me.bin, target does NOT
            File.WriteAllBytes(Path.Combine(sourceDir, "keep.bin"), keepData);
            File.WriteAllBytes(Path.Combine(sourceDir, "delete_me.bin"), deleteData);
            File.WriteAllBytes(Path.Combine(targetDir, "keep.bin"), keepData);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Phase 1: Clean generates delete JSON
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);
            Assert.True(File.Exists(Path.Combine(patchDir, "generalupdate.delete.json")));

            // Phase 2: app dir has both files
            File.WriteAllBytes(Path.Combine(appDir, "keep.bin"), keepData);
            File.WriteAllBytes(Path.Combine(appDir, "delete_me.bin"), deleteData);

            var patch2Dir = GetPath("patch2");
            CopyDirectory(patchDir, patch2Dir);

            // Act
            await pipeline.DirtyAsync(appDir, patch2Dir);

            // Assert: keep.bin stays, delete_me.bin is deleted
            Assert.True(File.Exists(Path.Combine(appDir, "keep.bin")));
            Assert.False(File.Exists(Path.Combine(appDir, "delete_me.bin")));
        }

        [Fact(DisplayName = "DirtyAsync_补丁目录有额外未知文件_复制到应用目录")]
        public async Task DirtyAsync_UnknownFiles_CopiedToAppDir()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            var appDir = GetPath("app");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(appDir);

            var data = new byte[50];
            new Random(3).NextBytes(data);
            File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), data);
            File.WriteAllBytes(Path.Combine(targetDir, "a.bin"), data);

            // Phase 1: Clean (no differences → empty patch dir)
            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Add an "unknown" file (not .patch) to patch dir
            var unknownData = new byte[] { 100, 101, 102, 103 };
            File.WriteAllBytes(Path.Combine(patchDir, "config.xml"), unknownData);

            // App dir has original (identical) file
            File.WriteAllBytes(Path.Combine(appDir, "a.bin"), data);

            // Act
            await pipeline.DirtyAsync(appDir, patchDir);

            // Assert: unknown file was copied to app dir
            var copied = Path.Combine(appDir, "config.xml");
            Assert.True(File.Exists(copied));
            Assert.Equal(unknownData, File.ReadAllBytes(copied));
        }

        [Fact(DisplayName = "DirtyAsync_进度回调_最终Completed等于Total")]
        public async Task DirtyAsync_WithProgress_ReportsProgress()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            var appDir = GetPath("app");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(appDir);

            var rng = new Random(55);
            for (int i = 1; i <= 3; i++)
            {
                var oldData = new byte[100]; rng.NextBytes(oldData);
                var newData = new byte[100]; rng.NextBytes(newData);
                File.WriteAllBytes(Path.Combine(sourceDir, $"f{i}.bin"), oldData);
                File.WriteAllBytes(Path.Combine(targetDir, $"f{i}.bin"), newData);
                File.WriteAllBytes(Path.Combine(appDir, $"f{i}.bin"), oldData);
            }

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            var patch2Dir = GetPath("patch2");
            CopyDirectory(patchDir, patch2Dir);

            var (progress, captured) = CreateProgressCapture();

            // Act
            await pipeline.DirtyAsync(appDir, patch2Dir, progress: progress);

            // Assert
            Assert.NotEmpty(captured);
            var last = captured[^1];
            Assert.True(last.IsComplete);
            Assert.True(last.Completed >= 3);
        }

        [Fact(DisplayName = "DirtyAsync_已取消令牌_抛出OperationCanceledException")]
        public async Task DirtyAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            var appDir = GetPath("app");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(appDir);

            var data = new byte[50]; new Random(1).NextBytes(data);
            var newData = new byte[50]; new Random(2).NextBytes(newData);
            File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), data);
            File.WriteAllBytes(Path.Combine(targetDir, "a.bin"), newData);
            File.WriteAllBytes(Path.Combine(appDir, "a.bin"), data);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            var patch2Dir = GetPath("patch2");
            CopyDirectory(patchDir, patch2Dir);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                pipeline.DirtyAsync(appDir, patch2Dir, cancellationToken: cts.Token));
        }

        [Fact(DisplayName = "端到端往返_多个文件Clean再Dirty_输出与目标一致")]
        public async Task FullPipeline_RoundTrip_ProducesIdenticalOutput()
        {
            // Arrange
            var sourceDir = GetPath("source");
            var targetDir = GetPath("target");
            var patchDir = GetPath("patch");
            var appDir = GetPath("app");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(appDir);

            var rng = new Random(1234);

            // Create 3 modified + 1 new + 1 identical
            var aOld = new byte[200]; rng.NextBytes(aOld);
            var aNew = new byte[200]; rng.NextBytes(aNew);

            var bOld = new byte[300]; rng.NextBytes(bOld);
            var bNew = new byte[300]; rng.NextBytes(bNew);

            var cOld = new byte[150]; rng.NextBytes(cOld);
            var cNew = new byte[150]; rng.NextBytes(cNew);

            var ident = new byte[100]; rng.NextBytes(ident);
            var onlyNew = new byte[80]; rng.NextBytes(onlyNew);

            // Source (old version)
            File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), aOld);
            File.WriteAllBytes(Path.Combine(sourceDir, "b.bin"), bOld);
            File.WriteAllBytes(Path.Combine(sourceDir, "c.bin"), cOld);
            File.WriteAllBytes(Path.Combine(sourceDir, "ident.bin"), ident);

            // Target (new version)
            File.WriteAllBytes(Path.Combine(targetDir, "a.bin"), aNew);
            File.WriteAllBytes(Path.Combine(targetDir, "b.bin"), bNew);
            File.WriteAllBytes(Path.Combine(targetDir, "c.bin"), cNew);
            File.WriteAllBytes(Path.Combine(targetDir, "ident.bin"), ident);
            File.WriteAllBytes(Path.Combine(targetDir, "newfile.bin"), onlyNew);

            var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 1 });

            // Phase 1: Clean
            await pipeline.CleanAsync(sourceDir, targetDir, patchDir);

            // Phase 2: set up app dir as old version
            File.WriteAllBytes(Path.Combine(appDir, "a.bin"), aOld);
            File.WriteAllBytes(Path.Combine(appDir, "b.bin"), bOld);
            File.WriteAllBytes(Path.Combine(appDir, "c.bin"), cOld);
            File.WriteAllBytes(Path.Combine(appDir, "ident.bin"), ident);

            var patch2Dir = GetPath("patch2");
            CopyDirectory(patchDir, patch2Dir);

            // Phase 3: Dirty
            await pipeline.DirtyAsync(appDir, patch2Dir);

            // Assert: all target files present with correct content
            Assert.Equal(aNew, File.ReadAllBytes(Path.Combine(appDir, "a.bin")));
            Assert.Equal(bNew, File.ReadAllBytes(Path.Combine(appDir, "b.bin")));
            Assert.Equal(cNew, File.ReadAllBytes(Path.Combine(appDir, "c.bin")));
            Assert.Equal(ident, File.ReadAllBytes(Path.Combine(appDir, "ident.bin")));
            Assert.Equal(onlyNew, File.ReadAllBytes(Path.Combine(appDir, "newfile.bin")));
        }
    }
}
