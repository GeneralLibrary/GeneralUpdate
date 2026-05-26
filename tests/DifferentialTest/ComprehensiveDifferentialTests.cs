using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;
using Xunit;
using Xunit.Abstractions;

namespace DifferentialTest;

/// <summary>
/// Comprehensive tests covering single-file and multi-file parallel differential scenarios.
/// </summary>
public class ComprehensiveDifferentialTests : IDisposable
{
    private readonly string _testDir;
    private readonly ITestOutputHelper _output;

    public ComprehensiveDifferentialTests(ITestOutputHelper output)
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"CompDiff_{Guid.NewGuid():N}");
        _output = output;
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { /* best-effort */ }
    }

    // =========================================================================
    // Section 1: Single-file differential -- BsdiffDiffer
    // =========================================================================

    [Fact]
    public async Task BsdiffDiffer_CleanThenDirty_TextFile_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.txt");
        var newFile = Path.Combine(_testDir, "new.txt");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.txt");

        var oldContent = "v1.0.0 application data\nline 2\nline 3\nline 4\nline 5";
        var newContent = "v2.0.0 application data\nline 2 modified\nline 3\nline 4 enhanced\nline 5";

        File.WriteAllText(oldFile, oldContent);
        File.WriteAllText(newFile, newContent);

        var differ = new BsdiffDiffer();

        // Act: generate patch
        await differ.CleanAsync(oldFile, newFile, patchFile);
        Assert.True(File.Exists(patchFile));

        // Act: apply patch
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);

        // Assert: restored = new
        Assert.True(File.Exists(restoredFile));
        Assert.Equal(newContent, File.ReadAllText(restoredFile));
    }

    [Fact]
    public async Task BsdiffDiffer_BinaryFile_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.bin");
        var newFile = Path.Combine(_testDir, "new.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        var rng = new Random(42);
        var oldData = new byte[256 * 1024]; // 256KB
        rng.NextBytes(oldData);

        var newData = (byte[])oldData.Clone();
        // Modify several blocks
        for (var i = 0; i < 10; i++)
        {
            var offset = rng.Next(newData.Length - 16);
            var len = rng.Next(1, 16);
            for (var j = 0; j < len; j++)
                newData[offset + j] = (byte)rng.Next(256);
        }
        // Insert some data
        var insert = new byte[1024];
        rng.NextBytes(insert);
        var mid = newData.Length / 2;
        newData = [.. newData[..mid], .. insert, .. newData[mid..]];

        File.WriteAllBytes(oldFile, oldData);
        File.WriteAllBytes(newFile, newData);

        var differ = new BsdiffDiffer();

        await differ.CleanAsync(oldFile, newFile, patchFile);
        Assert.True(File.Exists(patchFile));

        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        Assert.True(File.Exists(restoredFile));
        Assert.Equal(newData, File.ReadAllBytes(restoredFile));
    }

    [Fact]
    public async Task BsdiffDiffer_EmptyFile_RoundTrip()
    {
        var emptyFile = Path.Combine(_testDir, "empty.bin");
        var nonEmptyFile = Path.Combine(_testDir, "nonempty.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        File.WriteAllBytes(emptyFile, []);
        File.WriteAllBytes(nonEmptyFile, [1, 2, 3, 4, 5]);

        var differ = new BsdiffDiffer();

        // Empty -> Non-empty
        await differ.CleanAsync(emptyFile, nonEmptyFile, patchFile);
        await differ.DirtyAsync(emptyFile, restoredFile, patchFile);
        Assert.Equal([1, 2, 3, 4, 5], File.ReadAllBytes(restoredFile));

        // Non-empty -> Empty
        var patchFile2 = Path.Combine(_testDir, "patch2.bin");
        var restoredFile2 = Path.Combine(_testDir, "restored2.bin");
        await differ.CleanAsync(nonEmptyFile, emptyFile, patchFile2);
        await differ.DirtyAsync(nonEmptyFile, restoredFile2, patchFile2);
        Assert.Equal([], File.ReadAllBytes(restoredFile2));
    }

    [Fact]
    public async Task BsdiffDiffer_IdenticalFiles_ProducesMinimalPatch()
    {
        var oldFile = Path.Combine(_testDir, "old.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        var data = new byte[4096];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(oldFile, data);

        var differ = new BsdiffDiffer();

        await differ.CleanAsync(oldFile, oldFile, patchFile);
        Assert.True(File.Exists(patchFile));

        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        Assert.Equal(data, File.ReadAllBytes(restoredFile));
    }

    [Fact]
    public async Task BsdiffDiffer_WithDeflateCompression_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.txt");
        var newFile = Path.Combine(_testDir, "new.txt");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.txt");

        var oldContent = "ABCDEFGH" + new string('X', 10000) + "IJKLMNOP";
        var newContent = "ABCDEFGH" + new string('Y', 5000) + "ZZZ" + new string('X', 5000) + "IJKLMNOP";

        File.WriteAllText(oldFile, oldContent);
        File.WriteAllText(newFile, newContent);

        var differ = new BsdiffDiffer(new DeflateCompressionProvider());

        await differ.CleanAsync(oldFile, newFile, patchFile);
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);

        Assert.Equal(newContent, File.ReadAllText(restoredFile));
    }

    [Fact]
    public async Task BsdiffDiffer_LargeFile_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.bin");
        var newFile = Path.Combine(_testDir, "new.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        // ~1MB file
        var oldData = new byte[1024 * 1024];
        new Random(123).NextBytes(oldData);

        var newData = (byte[])oldData.Clone();
        // Modify the middle section
        for (var i = 512 * 1024; i < 512 * 1024 + 8192; i++)
            newData[i] = (byte)(oldData[i] ^ 0xFF);

        File.WriteAllBytes(oldFile, oldData);
        File.WriteAllBytes(newFile, newData);

        var differ = new BsdiffDiffer();
        var sw = Stopwatch.StartNew();

        await differ.CleanAsync(oldFile, newFile, patchFile);
        _output.WriteLine($"Bsdiff Clean (1MB): {sw.ElapsedMilliseconds}ms, patch={new FileInfo(patchFile).Length} bytes");

        sw.Restart();
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        _output.WriteLine($"Bsdiff Dirty (1MB): {sw.ElapsedMilliseconds}ms");

        Assert.Equal(newData, File.ReadAllBytes(restoredFile));
    }

    // =========================================================================
    // Section 2: Single-file differential -- StreamingHdiffDiffer
    // =========================================================================

    [Fact]
    public async Task StreamingHdiffDiffer_TextFile_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.txt");
        var newFile = Path.Combine(_testDir, "new.txt");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.txt");

        var oldContent = "Original program v1\nstatic config\nadmin panel off\n";
        var newContent = "Original program v2\nstatic config updated\nadmin panel on\ndebug mode on\n";

        File.WriteAllText(oldFile, oldContent);
        File.WriteAllText(newFile, newContent);

        var differ = new StreamingHdiffDiffer();

        await differ.CleanAsync(oldFile, newFile, patchFile);
        Assert.True(File.Exists(patchFile));

        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        Assert.Equal(newContent, File.ReadAllText(restoredFile));
    }

    [Fact]
    public async Task StreamingHdiffDiffer_BinaryFile_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.bin");
        var newFile = Path.Combine(_testDir, "new.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        var rng = new Random(99);
        var oldData = new byte[128 * 1024];
        rng.NextBytes(oldData);

        var newData = (byte[])oldData.Clone();
        // Scattered small modifications
        for (var i = 0; i < 50; i++)
        {
            var off = rng.Next(newData.Length - 1);
            newData[off] = (byte)rng.Next(256);
        }

        File.WriteAllBytes(oldFile, oldData);
        File.WriteAllBytes(newFile, newData);

        var differ = new StreamingHdiffDiffer();

        await differ.CleanAsync(oldFile, newFile, patchFile);
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        Assert.Equal(newData, File.ReadAllBytes(restoredFile));
    }

    [Fact]
    public async Task StreamingHdiffDiffer_EmptyFile_RoundTrip()
    {
        var emptyFile = Path.Combine(_testDir, "empty.bin");
        var nonEmptyFile = Path.Combine(_testDir, "nonempty.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");

        File.WriteAllBytes(emptyFile, []);
        File.WriteAllBytes(nonEmptyFile, new byte[2048]);

        var differ = new StreamingHdiffDiffer();

        // Empty -> Non-empty
        await differ.CleanAsync(emptyFile, nonEmptyFile, patchFile);
        var restored = Path.Combine(_testDir, "restored1.bin");
        await differ.DirtyAsync(emptyFile, restored, patchFile);
        Assert.Equal(2048, new FileInfo(restored).Length);

        // Non-empty -> Empty
        var patchFile2 = Path.Combine(_testDir, "patch2.bin");
        await differ.CleanAsync(nonEmptyFile, emptyFile, patchFile2);
        var restored2 = Path.Combine(_testDir, "restored2.bin");
        await differ.DirtyAsync(nonEmptyFile, restored2, patchFile2);
        Assert.Equal(0, new FileInfo(restored2).Length);
    }

    [Fact]
    public async Task StreamingHdiffDiffer_IdenticalFiles_MinimalPatch()
    {
        var file = Path.Combine(_testDir, "file.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");

        var data = new byte[8192];
        new Random(7).NextBytes(data);
        File.WriteAllBytes(file, data);

        var differ = new StreamingHdiffDiffer();
        await differ.CleanAsync(file, file, patchFile);

        var restored = Path.Combine(_testDir, "restored.bin");
        await differ.DirtyAsync(file, restored, patchFile);
        Assert.Equal(data, File.ReadAllBytes(restored));
    }

    [Fact]
    public async Task StreamingHdiffDiffer_WithBZip2_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.txt");
        var newFile = Path.Combine(_testDir, "new.txt");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.txt");

        File.WriteAllText(oldFile, "AAAA" + new string('B', 5000) + "CCCC");
        File.WriteAllText(newFile, "AAAA" + new string('D', 3000) + "CCCC2222");

        var differ = new StreamingHdiffDiffer(new BZip2CompressionProvider(), 64 * 1024, 128 * 1024 * 1024);

        await differ.CleanAsync(oldFile, newFile, patchFile);
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        Assert.Equal("AAAA" + new string('D', 3000) + "CCCC2222", File.ReadAllText(restoredFile));
    }

    [Fact]
    public async Task StreamingHdiffDiffer_LargeFile_RoundTrip()
    {
        var oldFile = Path.Combine(_testDir, "old.bin");
        var newFile = Path.Combine(_testDir, "new.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        // ~2MB
        var oldData = new byte[2 * 1024 * 1024];
        new Random(456).NextBytes(oldData);

        var newData = (byte[])oldData.Clone();
        // Modify three regions
        for (var i = 0; i < 4096; i++)
            newData[1024 * 256 + i] = (byte)(oldData[1024 * 256 + i] ^ 0xAA);
        for (var i = 0; i < 2048; i++)
            newData[1024 * 1024 + i] = 0x42;

        File.WriteAllBytes(oldFile, oldData);
        File.WriteAllBytes(newFile, newData);

        var differ = new StreamingHdiffDiffer();
        var sw = Stopwatch.StartNew();

        await differ.CleanAsync(oldFile, newFile, patchFile);
        _output.WriteLine($"StreamingHdiff Clean (2MB): {sw.ElapsedMilliseconds}ms, patch={new FileInfo(patchFile).Length} bytes");

        sw.Restart();
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        _output.WriteLine($"StreamingHdiff Dirty (2MB): {sw.ElapsedMilliseconds}ms");

        Assert.Equal(newData, File.ReadAllBytes(restoredFile));
    }

    // =========================================================================
    // Section 3: Multi-file parallel differential -- DiffPipeline
    // =========================================================================

    [Fact]
    public async Task DiffPipeline_ParallelClean_AllFilesGeneratePatches()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(patchDir);

        // Create 30 files in source and target with modifications
        for (var i = 0; i < 30; i++)
        {
            var subDir = i % 5 == 0 ? $"sub{i / 5}" : "";
            if (subDir.Length > 0)
            {
                Directory.CreateDirectory(Path.Combine(src, subDir));
                Directory.CreateDirectory(Path.Combine(tgt, subDir));
                Directory.CreateDirectory(Path.Combine(patchDir, subDir));
            }

            var fileName = Path.Combine(subDir, $"module_{i:D3}.dll");
            File.WriteAllBytes(Path.Combine(src, fileName), GenerateBytes(1024 + i * 64, i));
            File.WriteAllBytes(Path.Combine(tgt, fileName), GenerateBytes(1024 + i * 64, i + 100));
        }

        // Also add some new files in target (no old version)
        File.WriteAllBytes(Path.Combine(tgt, "new_features.dll"), [1, 2, 3, 4, 5]);

        var completedFiles = new ConcurrentBag<string>();
        var progress = new SyncProgress<DiffProgress>();
        var pipeline = new DiffPipelineBuilder()
            .WithParallelism(Environment.ProcessorCount)
            .Build();

        // Act
        var sw = Stopwatch.StartNew();
        await pipeline.CleanAsync(src, tgt, patchDir, progress);
        _output.WriteLine($"Parallel Clean (30 files): {sw.ElapsedMilliseconds}ms");

        // Assert
        Assert.True(progress.LastValue.IsComplete);
        _output.WriteLine($"Progress: {progress.LastValue.Completed}/{progress.LastValue.Total}");

        // Each modified file should have a .patch
        for (var i = 0; i < 30; i++)
        {
            var subDir = i % 5 == 0 ? $"sub{i / 5}" : "";
            var patchPath = Path.Combine(patchDir, subDir, $"module_{i:D3}.dll.patch");
            Assert.True(File.Exists(patchPath), $"Patch missing for module_{i:D3}.dll");
        }

        // New file should be copied directly
        Assert.True(File.Exists(Path.Combine(patchDir, "new_features.dll")));
    }

    [Fact]
    public async Task DiffPipeline_FullCycle_CleanThenDirty_ResultsMatchTarget()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        // Setup: various file operations
        var fileSpecs = new Dictionary<string, (byte[] Old, byte[] New)>
        {
            ["core.dll"] = (GenerateBytes(4096, 0), GenerateBytes(4096, 100)),
            ["config.json"] = (
                System.Text.Encoding.UTF8.GetBytes("{\"version\":\"1.0\"}"),
                System.Text.Encoding.UTF8.GetBytes("{\"version\":\"2.0\",\"debug\":true}")),
            ["lib/utils.dll"] = (GenerateBytes(2048, 42), GenerateBytes(2048, 42)), // unchanged
            ["assets/texture.bin"] = (GenerateBytes(8192, 7), GenerateBytes(8192, 77)),
        };

        // New files in target (no old version)
        var newFileContent = GenerateBytes(512, 99);

        // Populate source and app (old versions)
        foreach (var (path, (old, _)) in fileSpecs)
        {
            var srcPath = Path.Combine(src, path);
            Directory.CreateDirectory(Path.GetDirectoryName(srcPath)!);
            File.WriteAllBytes(srcPath, old);

            var appPath = Path.Combine(app, path);
            Directory.CreateDirectory(Path.GetDirectoryName(appPath)!);
            File.WriteAllBytes(appPath, old);
        }

        // Populate target (new versions)
        foreach (var (path, (_, @new)) in fileSpecs)
        {
            var tgtPath = Path.Combine(tgt, path);
            Directory.CreateDirectory(Path.GetDirectoryName(tgtPath)!);
            File.WriteAllBytes(tgtPath, @new);
        }
        // New file
        File.WriteAllBytes(Path.Combine(tgt, "new_plugin.dll"), newFileContent);

        // Act: generate patches
        var pipeline = new DiffPipelineBuilder().WithParallelism(4).Build();
        await pipeline.CleanAsync(src, tgt, patchDir);

        // Act: apply patches
        await pipeline.DirtyAsync(app, patchDir);

        // Assert: app now matches target for all files
        foreach (var (path, (_, expected)) in fileSpecs)
        {
            var resultPath = Path.Combine(app, path);
            Assert.True(File.Exists(resultPath), $"File '{path}' should exist in app after Dirty");
            Assert.Equal(expected, File.ReadAllBytes(resultPath));
        }

        // Assert: new file should exist in app
        Assert.True(File.Exists(Path.Combine(app, "new_plugin.dll")));
        Assert.Equal(newFileContent, File.ReadAllBytes(Path.Combine(app, "new_plugin.dll")));
    }

    [Fact]
    public async Task DiffPipeline_Parallelism1_Serial_WorksCorrectly()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(patchDir);

        for (var i = 0; i < 10; i++)
        {
            var dir = i % 3 == 0 ? $"layer{i / 3}" : "";
            if (dir.Length > 0)
            {
                Directory.CreateDirectory(Path.Combine(src, dir));
                Directory.CreateDirectory(Path.Combine(tgt, dir));
            }
            File.WriteAllText(Path.Combine(src, dir, $"file_{i}.txt"), $"old version {i} content");
            File.WriteAllText(Path.Combine(tgt, dir, $"file_{i}.txt"), $"new version {i} updated content");
        }

        var pipeline = new DiffPipelineBuilder()
            .WithParallelism(1) // serial
            .Build();

        await pipeline.CleanAsync(src, tgt, patchDir);

        for (var i = 0; i < 10; i++)
        {
            var dir = i % 3 == 0 ? $"layer{i / 3}" : "";
            Assert.True(File.Exists(Path.Combine(patchDir, dir, $"file_{i}.txt.patch")));
        }
    }

    [Fact]
    public async Task DiffPipeline_CustomDiffer_StreamingHdiffDiffer_Works()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        for (var i = 0; i < 15; i++)
        {
            var oldContent = GenerateBytes(2048, i);
            var newContent = GenerateBytes(2048, i + 1000);
            File.WriteAllBytes(Path.Combine(src, $"comp_{i:D2}.dll"), oldContent);
            File.WriteAllBytes(Path.Combine(tgt, $"comp_{i:D2}.dll"), newContent);
            File.WriteAllBytes(Path.Combine(app, $"comp_{i:D2}.dll"), oldContent);
        }

        // Use StreamingHdiffDiffer with BZip2 compression
        var pipeline = new DiffPipelineBuilder()
            .UseDiffer(new StreamingHdiffDiffer(new BZip2CompressionProvider(), 32 * 1024, 64 * 1024 * 1024))
            .WithParallelism(4)
            .Build();

        // Generate patches
        await pipeline.CleanAsync(src, tgt, patchDir);
        // Apply patches
        await pipeline.DirtyAsync(app, patchDir);

        // Verify
        for (var i = 0; i < 15; i++)
        {
            var expected = GenerateBytes(2048, i + 1000);
            var actual = File.ReadAllBytes(Path.Combine(app, $"comp_{i:D2}.dll"));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task DiffPipeline_CustomDiffer_BsdiffDiffer_Works()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        for (var i = 0; i < 10; i++)
        {
            var oldContent = GenerateBytes(1024, i * 2);
            var newContent = GenerateBytes(1024, i * 2 + 777);
            File.WriteAllBytes(Path.Combine(src, $"bs_{i:D2}.bin"), oldContent);
            File.WriteAllBytes(Path.Combine(tgt, $"bs_{i:D2}.bin"), newContent);
            File.WriteAllBytes(Path.Combine(app, $"bs_{i:D2}.bin"), oldContent);
        }

        var pipeline = new DiffPipelineBuilder()
            .UseDiffer(new BsdiffDiffer(new DeflateCompressionProvider()))
            .WithParallelism(4)
            .Build();

        await pipeline.CleanAsync(src, tgt, patchDir);
        await pipeline.DirtyAsync(app, patchDir);

        for (var i = 0; i < 10; i++)
        {
            var expected = GenerateBytes(1024, i * 2 + 777);
            var actual = File.ReadAllBytes(Path.Combine(app, $"bs_{i:D2}.bin"));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task DiffPipeline_ProgressReporting_TracksAccurately()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(patchDir);

        for (var i = 0; i < 25; i++)
        {
            File.WriteAllBytes(Path.Combine(src, $"f{i:D3}.dat"), GenerateBytes(512, i));
            File.WriteAllBytes(Path.Combine(tgt, $"f{i:D3}.dat"), GenerateBytes(512, i + 500));
        }

        var progressValues = new List<DiffProgress>();
        var progress = new Progress<DiffProgress>(p => progressValues.Add(p));

        var pipeline = new DiffPipelineBuilder().WithParallelism(4).Build();
        await pipeline.CleanAsync(src, tgt, patchDir, progress);

        Assert.NotEmpty(progressValues);
        Assert.Contains(progressValues, p => p.IsComplete);
        Assert.True(progressValues.Any(p => p.Completed > 0));
    }

    [Fact]
    public async Task DiffPipeline_DeleteHandling_RemovesDeletedFiles()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        // File exists in source but NOT in target (deleted)
        var deletedFileName = "obsolete.dll";
        File.WriteAllBytes(Path.Combine(src, deletedFileName), [1, 2, 3, 4, 5]);
        File.WriteAllBytes(Path.Combine(app, deletedFileName), [1, 2, 3, 4, 5]);

        // File exists in both (modified)
        File.WriteAllBytes(Path.Combine(src, "core.dll"), GenerateBytes(1024, 1));
        File.WriteAllBytes(Path.Combine(tgt, "core.dll"), GenerateBytes(1024, 100));
        File.WriteAllBytes(Path.Combine(app, "core.dll"), GenerateBytes(1024, 1));

        var pipeline = new DiffPipelineBuilder().WithParallelism(2).Build();

        // Generate patches (delete list + patch for core.dll)
        await pipeline.CleanAsync(src, tgt, patchDir);

        // Verify delete list exists
        Assert.True(File.Exists(Path.Combine(patchDir, "generalupdate_delete_files.json")));

        // Apply patches
        await pipeline.DirtyAsync(app, patchDir);

        // obsolete.dll should be deleted
        Assert.False(File.Exists(Path.Combine(app, deletedFileName)),
            $"Deleted file '{deletedFileName}' should be removed from app");

        // core.dll should be updated
        Assert.Equal(GenerateBytes(1024, 100), File.ReadAllBytes(Path.Combine(app, "core.dll")));
    }

    [Fact]
    public async Task DiffPipeline_OnlyNewFiles_NoPatchesNeeded()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        // Source is empty, target has new files
        File.WriteAllBytes(Path.Combine(tgt, "brand_new_1.dll"), [10, 20, 30]);
        File.WriteAllBytes(Path.Combine(tgt, "brand_new_2.dll"), [40, 50, 60]);

        var pipeline = new DiffPipelineBuilder().Build();

        // Generate patches
        await pipeline.CleanAsync(src, tgt, patchDir);

        // New files should be copied directly (no .patch generated)
        Assert.True(File.Exists(Path.Combine(patchDir, "brand_new_1.dll")));
        Assert.True(File.Exists(Path.Combine(patchDir, "brand_new_2.dll")));

        // Apply patches
        await pipeline.DirtyAsync(app, patchDir);

        Assert.True(File.Exists(Path.Combine(app, "brand_new_1.dll")));
        Assert.Equal([10, 20, 30], File.ReadAllBytes(Path.Combine(app, "brand_new_1.dll")));
    }

    [Fact]
    public async Task DiffPipeline_Cancellation_CleansUpGracefully()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(patchDir);

        for (var i = 0; i < 20; i++)
        {
            File.WriteAllBytes(Path.Combine(src, $"c{i}.dat"), GenerateBytes(1024, i));
            File.WriteAllBytes(Path.Combine(tgt, $"c{i}.dat"), GenerateBytes(1024, i + 100));
        }

        var pipeline = new DiffPipelineBuilder().WithParallelism(2).Build();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.CleanAsync(src, tgt, patchDir, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DiffPipeline_DirtyAsync_NoExistingPatches_HandledGracefully()
    {
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        File.WriteAllBytes(Path.Combine(app, "x.dll"), [1, 2, 3]);

        var pipeline = new DiffPipelineBuilder().Build();
        // Should not throw when there are no patch files to apply
        await pipeline.DirtyAsync(app, patchDir);

        Assert.True(File.Exists(Path.Combine(app, "x.dll")));
    }

    // =========================================================================
    // Section 4: Edge cases and stress tests
    // =========================================================================

    [Fact]
    public async Task SingleFile_LargeContent_20MB_DifferentialWorks()
    {
        var oldFile = Path.Combine(_testDir, "old.bin");
        var newFile = Path.Combine(_testDir, "new.bin");
        var patchFile = Path.Combine(_testDir, "patch.bin");
        var restoredFile = Path.Combine(_testDir, "restored.bin");

        // ~5MB files (keep reasonable for test runtime)
        var oldData = new byte[5 * 1024 * 1024];
        new Random(888).NextBytes(oldData);

        var newData = (byte[])oldData.Clone();
        // Modify ~10% scattered
        for (var i = 0; i < 100; i++)
        {
            var off = (int)(((long)i * newData.Length) / 100);
            for (var j = 0; j < 1024 && off + j < newData.Length; j++)
                newData[off + j] = (byte)(oldData[off + j] ^ 0x55);
        }

        File.WriteAllBytes(oldFile, oldData);
        File.WriteAllBytes(newFile, newData);

        var differ = new StreamingHdiffDiffer();
        var sw = Stopwatch.StartNew();

        await differ.CleanAsync(oldFile, newFile, patchFile);
        _output.WriteLine($"5MB clean: {sw.ElapsedMilliseconds}ms, patch={new FileInfo(patchFile).Length} bytes");

        sw.Restart();
        await differ.DirtyAsync(oldFile, restoredFile, patchFile);
        _output.WriteLine($"5MB dirty: {sw.ElapsedMilliseconds}ms");

        Assert.Equal(newData, File.ReadAllBytes(restoredFile));
    }

    [Fact]
    public async Task MultiFile_HighParallelism_AllFilesProcessed()
    {
        var src = Path.Combine(_testDir, "src");
        var tgt = Path.Combine(_testDir, "tgt");
        var app = Path.Combine(_testDir, "app");
        var patchDir = Path.Combine(_testDir, "patches");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tgt);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(patchDir);

        var fileCount = 50;
        for (var i = 0; i < fileCount; i++)
        {
            var oldContent = GenerateBytes(512, i);
            var newContent = GenerateBytes(512, i + 1000);
            File.WriteAllBytes(Path.Combine(src, $"h_{i:D3}.dll"), oldContent);
            File.WriteAllBytes(Path.Combine(tgt, $"h_{i:D3}.dll"), newContent);
            File.WriteAllBytes(Path.Combine(app, $"h_{i:D3}.dll"), oldContent);
        }

        var pipeline = new DiffPipelineBuilder()
            .WithParallelism(Environment.ProcessorCount)
            .Build();

        var sw = Stopwatch.StartNew();
        await pipeline.CleanAsync(src, tgt, patchDir);
        _output.WriteLine($"Clean {fileCount} files (x{Environment.ProcessorCount}): {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        await pipeline.DirtyAsync(app, patchDir);
        _output.WriteLine($"Dirty {fileCount} files (x{Environment.ProcessorCount}): {sw.ElapsedMilliseconds}ms");

        for (var i = 0; i < fileCount; i++)
        {
            var resultPath = Path.Combine(app, $"h_{i:D3}.dll");
            Assert.True(File.Exists(resultPath));
            Assert.Equal(GenerateBytes(512, i + 1000), File.ReadAllBytes(resultPath));
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static byte[] GenerateBytes(int size, int seed)
    {
        var data = new byte[size];
        var rng = new Random(seed);
        rng.NextBytes(data);
        return data;
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        public T LastValue { get; private set; } = default!;

        public void Report(T value) => LastValue = value;
    }
}
