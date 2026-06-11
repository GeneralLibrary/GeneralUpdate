using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Differ;
using Xunit;

namespace DifferentialTest.Binary
{
    /// <summary>
    /// Contains test cases for the BsdiffDiffer class.
    /// Tests binary diff generation (Clean) and patch application (Dirty) functionality.
    /// </summary>
    public class BsdiffDifferTests : IDisposable
    {
        private readonly string _testDirectory;

        public BsdiffDifferTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"DifferentialTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        #region Clean Method Tests

        /// <summary>
        /// Tests that Clean throws ArgumentNullException when oldfilePath is null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Clean_WithInvalidOldFilePath_ThrowsArgumentNullException(string invalidPath)
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "patch.patch");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Clean(invalidPath, newFilePath, patchPath));
        }

        /// <summary>
        /// Tests that Clean throws ArgumentNullException when newfilePath is null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Clean_WithInvalidNewFilePath_ThrowsArgumentNullException(string invalidPath)
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var patchPath = Path.Combine(_testDirectory, "patch.patch");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Clean(oldFilePath, invalidPath, patchPath));
        }

        /// <summary>
        /// Tests that Clean throws ArgumentNullException when patchPath is null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Clean_WithInvalidPatchPath_ThrowsArgumentNullException(string invalidPath)
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Clean(oldFilePath, newFilePath, invalidPath));
        }

        /// <summary>
        /// Tests that Clean generates a patch file for different files.
        /// </summary>
        [Fact]
        public async Task Clean_WithDifferentFiles_GeneratesPatchFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");

            File.WriteAllText(oldFilePath, "This is the old version of the file.");
            File.WriteAllText(newFilePath, "This is the new version of the file with changes.");

            // Act
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Assert
            Assert.True(File.Exists(patchPath), "Patch file should be created");
            Assert.True(new FileInfo(patchPath).Length > 0, "Patch file should not be empty");
        }

        /// <summary>
        /// Tests that Clean generates a patch file for identical files.
        /// </summary>
        [Fact]
        public async Task Clean_WithIdenticalFiles_GeneratesPatchFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");

            File.WriteAllText(oldFilePath, "Same content in both files.");
            File.WriteAllText(newFilePath, "Same content in both files.");

            // Act
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Assert
            Assert.True(File.Exists(patchPath), "Patch file should be created even for identical files");
        }

        /// <summary>
        /// Tests that Clean handles empty old file correctly.
        /// </summary>
        [Fact]
        public async Task Clean_WithEmptyOldFile_GeneratesPatchFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");

            File.WriteAllText(oldFilePath, "");
            File.WriteAllText(newFilePath, "New content added.");

            // Act
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Assert
            Assert.True(File.Exists(patchPath), "Patch file should be created");
            Assert.True(new FileInfo(patchPath).Length > 0, "Patch file should not be empty");
        }

        /// <summary>
        /// Tests that Clean handles empty new file correctly.
        /// </summary>
        [Fact]
        public async Task Clean_WithEmptyNewFile_GeneratesPatchFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");

            File.WriteAllText(oldFilePath, "Old content to be removed.");
            File.WriteAllText(newFilePath, "");

            // Act
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Assert
            Assert.True(File.Exists(patchPath), "Patch file should be created");
        }

        /// <summary>
        /// Tests that Clean handles binary files correctly.
        /// </summary>
        [Fact]
        public async Task Clean_WithBinaryFiles_GeneratesPatchFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.bin");
            var newFilePath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "test.patch");

            var oldBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
            var newBytes = new byte[] { 0x00, 0x01, 0x05, 0x06, 0x07, 0x08 };

            File.WriteAllBytes(oldFilePath, oldBytes);
            File.WriteAllBytes(newFilePath, newBytes);

            // Act
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Assert
            Assert.True(File.Exists(patchPath), "Patch file should be created for binary files");
            Assert.True(new FileInfo(patchPath).Length > 0, "Patch file should not be empty");
        }

        /// <summary>
        /// Tests that Clean handles large text changes correctly.
        /// </summary>
        [Fact]
        public async Task Clean_WithLargeTextChanges_GeneratesPatchFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");

            var oldContent = new StringBuilder();
            var newContent = new StringBuilder();

            for (int i = 0; i < 1000; i++)
            {
                oldContent.AppendLine($"Line {i}: Old content");
                newContent.AppendLine($"Line {i}: New content");
            }

            File.WriteAllText(oldFilePath, oldContent.ToString());
            File.WriteAllText(newFilePath, newContent.ToString());

            // Act
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Assert
            Assert.True(File.Exists(patchPath), "Patch file should be created");
            Assert.True(new FileInfo(patchPath).Length > 0, "Patch file should not be empty");
        }

        #endregion

        #region Dirty Method Tests

        /// <summary>
        /// Tests that Dirty throws ArgumentNullException when oldfilePath is null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Dirty_WithInvalidOldFilePath_ThrowsArgumentNullException(string invalidPath)
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "patch.patch");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Dirty(invalidPath, newFilePath, patchPath));
        }

        /// <summary>
        /// Tests that Dirty throws ArgumentNullException when newfilePath is null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Dirty_WithInvalidNewFilePath_ThrowsArgumentNullException(string invalidPath)
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var patchPath = Path.Combine(_testDirectory, "patch.patch");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Dirty(oldFilePath, invalidPath, patchPath));
        }

        /// <summary>
        /// Tests that Dirty throws ArgumentNullException when patchPath is null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Dirty_WithInvalidPatchPath_ThrowsArgumentNullException(string invalidPath)
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Dirty(oldFilePath, newFilePath, invalidPath));
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch to create the new file.
        /// </summary>
        [Fact]
        public async Task Dirty_WithValidPatch_CreatesNewFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");

            var oldContent = "This is the old version of the file.";
            var newContent = "This is the new version of the file with changes.";

            File.WriteAllText(oldFilePath, oldContent);
            File.WriteAllText(newFilePath, newContent);

            // Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to target location for patching
            File.Copy(oldFilePath, targetFilePath);

            // Act - Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath);

            // Assert
            var resultContent = File.ReadAllText(Path.Combine(_testDirectory, "result.txt"));
            Assert.Equal(newContent, resultContent);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch to binary files.
        /// </summary>
        [Fact]
        public async Task Dirty_WithBinaryPatch_CreatesNewBinaryFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.bin");
            var newFilePath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.bin");

            var oldBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
            var newBytes = new byte[] { 0x00, 0x01, 0x05, 0x06, 0x07, 0x08 };

            File.WriteAllBytes(oldFilePath, oldBytes);
            File.WriteAllBytes(newFilePath, newBytes);

            // Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to target location for patching
            File.Copy(oldFilePath, targetFilePath);

            // Act - Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.bin"), patchPath);

            // Assert
            var resultBytes = File.ReadAllBytes(Path.Combine(_testDirectory, "result.bin"));
            Assert.Equal(newBytes, resultBytes);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch when old file is empty.
        /// </summary>
        [Fact]
        public async Task Dirty_WithEmptyOldFile_CreatesNewFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");

            var newContent = "New content added.";

            File.WriteAllText(oldFilePath, "");
            File.WriteAllText(newFilePath, newContent);

            // Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to target location for patching
            File.Copy(oldFilePath, targetFilePath);

            // Act - Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath);

            // Assert
            var resultContent = File.ReadAllText(Path.Combine(_testDirectory, "result.txt"));
            Assert.Equal(newContent, resultContent);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch when new file is empty.
        /// </summary>
        [Fact]
        public async Task Dirty_WithEmptyNewFile_CreatesEmptyFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");

            File.WriteAllText(oldFilePath, "Old content to be removed.");
            File.WriteAllText(newFilePath, "");

            // Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to target location for patching
            File.Copy(oldFilePath, targetFilePath);

            // Act - Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath);

            // Assert
            var resultContent = File.ReadAllText(Path.Combine(_testDirectory, "result.txt"));
            Assert.Equal("", resultContent);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch with identical files.
        /// </summary>
        [Fact]
        public async Task Dirty_WithIdenticalFiles_KeepsFileUnchanged()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");

            var content = "Same content in both files.";

            File.WriteAllText(oldFilePath, content);
            File.WriteAllText(newFilePath, content);

            // Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to target location for patching
            File.Copy(oldFilePath, targetFilePath);

            // Act - Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath);

            // Assert
            var resultContent = File.ReadAllText(Path.Combine(_testDirectory, "result.txt"));
            Assert.Equal(content, resultContent);
        }

        /// <summary>
        /// Tests that Dirty throws an exception with a corrupt patch.
        /// </summary>
        [Fact]
        public async Task Dirty_WithCorruptPatch_ThrowsException()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");
            var patchPath = Path.Combine(_testDirectory, "corrupt.patch");

            File.WriteAllText(oldFilePath, "Some content");
            File.Copy(oldFilePath, targetFilePath);

            // Create a corrupt patch file
            File.WriteAllBytes(patchPath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            // Act & Assert - Either InvalidOperationException or EndOfStreamException is acceptable
            await Assert.ThrowsAnyAsync<Exception>(() =>
                handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath));
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch with large files.
        /// </summary>
        [Fact]
        public async Task Dirty_WithLargeFiles_CreatesNewFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");

            var oldContent = new StringBuilder();
            var newContent = new StringBuilder();

            for (int i = 0; i < 1000; i++)
            {
                oldContent.AppendLine($"Line {i}: Old content");
                newContent.AppendLine($"Line {i}: New content");
            }

            File.WriteAllText(oldFilePath, oldContent.ToString());
            File.WriteAllText(newFilePath, newContent.ToString());

            // Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to target location for patching
            File.Copy(oldFilePath, targetFilePath);

            // Act - Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath);

            // Assert
            var resultContent = File.ReadAllText(Path.Combine(_testDirectory, "result.txt"));
            Assert.Equal(newContent.ToString(), resultContent);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests the full cycle of generating and applying patches (Clean + Dirty).
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_FullCycle_RecreatesOriginalFile()
        {
            // Arrange
            var handler = new BsdiffDiffer();
            var oldFilePath = Path.Combine(_testDirectory, "old.txt");
            var newFilePath = Path.Combine(_testDirectory, "new.txt");
            var patchPath = Path.Combine(_testDirectory, "test.patch");
            var targetFilePath = Path.Combine(_testDirectory, "target.txt");

            var oldContent = "Original content version 1.0";
            var newContent = "Updated content version 2.0 with significant changes";

            File.WriteAllText(oldFilePath, oldContent);
            File.WriteAllText(newFilePath, newContent);

            // Act - Generate patch
            await handler.Clean(oldFilePath, newFilePath, patchPath);

            // Copy old file to simulate the client side
            File.Copy(oldFilePath, targetFilePath);

            // Apply patch
            await handler.Dirty(targetFilePath, Path.Combine(_testDirectory, "result.txt"), patchPath);

            // Assert
            var resultContent = File.ReadAllText(Path.Combine(_testDirectory, "result.txt"));
            Assert.Equal(newContent, resultContent);
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests round-trip with single-byte files — exercises the suffix-array sentinel path
        /// where Split() may write I[k] = -1 for singleton buckets.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_SingleByteFiles_RoundTrip()
        {
            var handler = new BsdiffDiffer();
            var oldPath = Path.Combine(_testDirectory, "old.bin");
            var newPath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "patch.bin");
            var resultPath = Path.Combine(_testDirectory, "result.bin");

            File.WriteAllBytes(oldPath, new byte[] { 0x42 });
            File.WriteAllBytes(newPath, new byte[] { 0x43 });

            await handler.Clean(oldPath, newPath, patchPath);
            Assert.True(new FileInfo(patchPath).Length > 0);
            await handler.Dirty(oldPath, resultPath, patchPath);
            Assert.Equal(new byte[] { 0x43 }, File.ReadAllBytes(resultPath));
        }

        /// <summary>
        /// Tests round-trip with two-byte files where the suffix array creates
        /// single-element buckets with sentinel -1 entries.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_TwoByteFiles_RoundTrip()
        {
            var handler = new BsdiffDiffer();
            var oldPath = Path.Combine(_testDirectory, "old.bin");
            var newPath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "patch.bin");
            var resultPath = Path.Combine(_testDirectory, "result.bin");

            File.WriteAllBytes(oldPath, new byte[] { 0x00, 0xFF });
            File.WriteAllBytes(newPath, new byte[] { 0x00, 0xFE });

            await handler.Clean(oldPath, newPath, patchPath);
            await handler.Dirty(oldPath, resultPath, patchPath);
            Assert.Equal(new byte[] { 0x00, 0xFE }, File.ReadAllBytes(resultPath));
        }

        /// <summary>
        /// Tests round-trip where old has one byte and new has many bytes.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_TinyOldToLargeNew_RoundTrip()
        {
            var handler = new BsdiffDiffer();
            var oldPath = Path.Combine(_testDirectory, "old.bin");
            var newPath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "patch.bin");
            var resultPath = Path.Combine(_testDirectory, "result.bin");

            File.WriteAllBytes(oldPath, new byte[] { 0x00 });
            File.WriteAllBytes(newPath, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });

            await handler.Clean(oldPath, newPath, patchPath);
            await handler.Dirty(oldPath, resultPath, patchPath);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, File.ReadAllBytes(resultPath));
        }

        /// <summary>
        /// Tests round-trip with repeating byte patterns which stress the
        /// block-hash index and suffix-array matching logic.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_RepeatingPattern_RoundTrip()
        {
            var handler = new BsdiffDiffer();
            var oldPath = Path.Combine(_testDirectory, "old.bin");
            var newPath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "patch.bin");
            var resultPath = Path.Combine(_testDirectory, "result.bin");

            var oldPattern = new byte[256];
            var newPattern = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                oldPattern[i] = (byte)(i % 16);
                newPattern[i] = (byte)((i % 16) == 15 ? 0xFF : (i % 16));
            }

            File.WriteAllBytes(oldPath, oldPattern);
            File.WriteAllBytes(newPath, newPattern);

            await handler.Clean(oldPath, newPath, patchPath);
            await handler.Dirty(oldPath, resultPath, patchPath);
            Assert.Equal(newPattern, File.ReadAllBytes(resultPath));
        }

        /// <summary>
        /// Tests round-trip where a single byte changes in the middle of a large buffer.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_SingleByteChange_RoundTrip()
        {
            var handler = new BsdiffDiffer();
            var oldPath = Path.Combine(_testDirectory, "old.bin");
            var newPath = Path.Combine(_testDirectory, "new.bin");
            var patchPath = Path.Combine(_testDirectory, "patch.bin");
            var resultPath = Path.Combine(_testDirectory, "result.bin");

            var oldData = new byte[4096];
            var newData = new byte[4096];
            new Random(42).NextBytes(oldData);
            Buffer.BlockCopy(oldData, 0, newData, 0, 4096);
            newData[2048] ^= 0xFF; // flip one byte in the middle

            File.WriteAllBytes(oldPath, oldData);
            File.WriteAllBytes(newPath, newData);

            await handler.Clean(oldPath, newPath, patchPath);
            await handler.Dirty(oldPath, resultPath, patchPath);
            Assert.Equal(newData, File.ReadAllBytes(resultPath));
        }

        #endregion
    }
}
