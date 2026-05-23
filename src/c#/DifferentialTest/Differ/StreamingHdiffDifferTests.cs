using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Differ;
using Xunit;

namespace DifferentialTest.Differ
{
    /// <summary>
    /// Test cases for StreamingHdiffDiffer — round-trip diff and patch,
    /// IBinaryDiffer interface compliance, and edge cases.
    /// </summary>
    public class StreamingHdiffDifferTests : IDisposable
    {
        private readonly string _testDir;

        public StreamingHdiffDifferTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"HdiffTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
        }

        [Fact]
        public async Task CleanAsync_ProducesPatchFile()
        {
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patch = Path.Combine(_testDir, "patch.bin");

            File.WriteAllBytes(oldFile, new byte[] { 0, 1, 2, 3, 4, 5 });
            File.WriteAllBytes(newFile, new byte[] { 0, 1, 9, 9, 4, 5 });

            await differ.CleanAsync(oldFile, newFile, patch);

            Assert.True(File.Exists(patch));
            Assert.True(new FileInfo(patch).Length > 0);
        }

        [Fact]
        public async Task DirtyAsync_RoundTrip_RestoresNewFile()
        {
            var differ = new StreamingHdiffDiffer();
            var original = new byte[1024];
            var modified = new byte[1024];
            new Random(42).NextBytes(original);
            Array.Copy(original, modified, 1024);
            modified[512] = 0xFF; // single byte change

            var oldPath = Path.Combine(_testDir, "old.bin");
            var newPath = Path.Combine(_testDir, "new.bin");
            var patchPath = Path.Combine(_testDir, "patch.bin");
            var outputPath = Path.Combine(_testDir, "output.bin");

            File.WriteAllBytes(oldPath, original);
            File.WriteAllBytes(newPath, modified);

            await differ.CleanAsync(oldPath, newPath, patchPath);
            await differ.DirtyAsync(oldPath, outputPath, patchPath);

            var result = File.ReadAllBytes(outputPath);
            Assert.Equal(modified, result);
        }

        [Fact]
        public async Task DirtyAsync_EmptyFiles_HandlesCorrectly()
        {
            var differ = new StreamingHdiffDiffer();
            var empty = Array.Empty<byte>();

            var oldPath = Path.Combine(_testDir, "empty_old.bin");
            var newPath = Path.Combine(_testDir, "empty_new.bin");
            var patchPath = Path.Combine(_testDir, "patch.bin");
            var outputPath = Path.Combine(_testDir, "output.bin");

            File.WriteAllBytes(oldPath, empty);
            File.WriteAllBytes(newPath, empty);

            await differ.CleanAsync(oldPath, newPath, patchPath);
            await differ.DirtyAsync(oldPath, outputPath, patchPath);

            var result = File.ReadAllBytes(outputPath);
            Assert.Empty(result);
        }

        [Fact]
        public async Task CleanAsync_LargeBinary_RoundTrip_ProducesCorrectOutput()
        {
            var differ = new StreamingHdiffDiffer();
            var rng = new Random(123);
            var oldData = new byte[100_000];
            var newData = new byte[100_000];
            rng.NextBytes(oldData);
            Array.Copy(oldData, newData, 100_000);
            for (int i = 40_000; i < 40_100; i++)
                newData[i] = (byte)(oldData[i] ^ 0xFF);

            var oldPath = Path.Combine(_testDir, "large_old.bin");
            var newPath = Path.Combine(_testDir, "large_new.bin");
            var patchPath = Path.Combine(_testDir, "large_patch.bin");
            var outputPath = Path.Combine(_testDir, "large_output.bin");

            File.WriteAllBytes(oldPath, oldData);
            File.WriteAllBytes(newPath, newData);

            await differ.CleanAsync(oldPath, newPath, patchPath);
            Assert.True(File.Exists(patchPath));
            Assert.True(new FileInfo(patchPath).Length < newData.Length / 2);

            // Round-trip: apply patch and verify output
            await differ.DirtyAsync(oldPath, outputPath, patchPath);
            Assert.Equal(newData, File.ReadAllBytes(outputPath));
        }
    }
}
