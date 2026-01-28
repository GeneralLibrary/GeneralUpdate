using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Binary;
using Xunit;

namespace DifferentialTest.Binary
{
    /// <summary>
    /// Contains test cases for the BinaryHandler class.
    /// Tests binary diff generation (Clean) and patch application (Dirty) functionality.
    /// </summary>
    public class BinaryHandlerTests : IDisposable
    {
        private readonly string _testDirectory;

        public BinaryHandlerTests()
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var resultContent = File.ReadAllText(targetFilePath);
            Assert.Equal(newContent, resultContent);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch to binary files.
        /// </summary>
        [Fact]
        public async Task Dirty_WithBinaryPatch_CreatesNewBinaryFile()
        {
            // Arrange
            var handler = new BinaryHandler();
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
            var resultBytes = File.ReadAllBytes(targetFilePath);
            Assert.Equal(newBytes, resultBytes);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch when old file is empty.
        /// </summary>
        [Fact]
        public async Task Dirty_WithEmptyOldFile_CreatesNewFile()
        {
            // Arrange
            var handler = new BinaryHandler();
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
            var resultContent = File.ReadAllText(targetFilePath);
            Assert.Equal(newContent, resultContent);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch when new file is empty.
        /// </summary>
        [Fact]
        public async Task Dirty_WithEmptyNewFile_CreatesEmptyFile()
        {
            // Arrange
            var handler = new BinaryHandler();
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
            var resultContent = File.ReadAllText(targetFilePath);
            Assert.Equal("", resultContent);
        }

        /// <summary>
        /// Tests that Dirty correctly applies a patch with identical files.
        /// </summary>
        [Fact]
        public async Task Dirty_WithIdenticalFiles_KeepsFileUnchanged()
        {
            // Arrange
            var handler = new BinaryHandler();
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
            var resultContent = File.ReadAllText(targetFilePath);
            Assert.Equal(content, resultContent);
        }

        /// <summary>
        /// Tests that Dirty throws an exception with a corrupt patch.
        /// </summary>
        [Fact]
        public async Task Dirty_WithCorruptPatch_ThrowsException()
        {
            // Arrange
            var handler = new BinaryHandler();
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
            var handler = new BinaryHandler();
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
            var resultContent = File.ReadAllText(targetFilePath);
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
            var handler = new BinaryHandler();
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
            var resultContent = File.ReadAllText(targetFilePath);
            Assert.Equal(newContent, resultContent);
        }

        #endregion
    }
}
