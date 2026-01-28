using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Differential;
using Xunit;

namespace DifferentialTest
{
    /// <summary>
    /// Contains test cases for the DifferentialCore class.
    /// Tests the singleton pattern, Clean method (patch generation), and Dirty method (patch application).
    /// </summary>
    public class DifferentialCoreTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _sourceDirectory;
        private readonly string _targetDirectory;
        private readonly string _patchDirectory;
        private readonly string _appDirectory;

        public DifferentialCoreTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"DifferentialCoreTest_{Guid.NewGuid()}");
            _sourceDirectory = Path.Combine(_testDirectory, "source");
            _targetDirectory = Path.Combine(_testDirectory, "target");
            _patchDirectory = Path.Combine(_testDirectory, "patch");
            _appDirectory = Path.Combine(_testDirectory, "app");

            Directory.CreateDirectory(_testDirectory);
            Directory.CreateDirectory(_sourceDirectory);
            Directory.CreateDirectory(_targetDirectory);
            Directory.CreateDirectory(_patchDirectory);
            Directory.CreateDirectory(_appDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        #region Singleton Pattern Tests

        /// <summary>
        /// Tests that DifferentialCore.Instance returns a non-null instance.
        /// </summary>
        [Fact]
        public void Instance_ReturnsNonNullInstance()
        {
            // Act
            var instance = DifferentialCore.Instance;

            // Assert
            Assert.NotNull(instance);
        }

        /// <summary>
        /// Tests that DifferentialCore.Instance always returns the same instance (singleton).
        /// </summary>
        [Fact]
        public void Instance_ReturnsSameInstanceOnMultipleCalls()
        {
            // Act
            var instance1 = DifferentialCore.Instance;
            var instance2 = DifferentialCore.Instance;

            // Assert
            Assert.Same(instance1, instance2);
        }

        /// <summary>
        /// Tests that DifferentialCore.Instance is thread-safe.
        /// </summary>
        [Fact]
        public void Instance_IsThreadSafe()
        {
            // Arrange
            var instances = new DifferentialCore[10];
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() =>
                {
                    instances[index] = DifferentialCore.Instance;
                });
            }

            Task.WaitAll(tasks);

            // Assert
            var firstInstance = instances[0];
            foreach (var instance in instances)
            {
                Assert.Same(firstInstance, instance);
            }
        }

        #endregion

        #region Clean Method Tests

        /// <summary>
        /// Tests that Clean generates patch files for modified files.
        /// </summary>
        [Fact]
        public async Task Clean_WithModifiedFiles_GeneratesPatchFiles()
        {
            // Arrange
            var sourceFile = Path.Combine(_sourceDirectory, "test.txt");
            var targetFile = Path.Combine(_targetDirectory, "test.txt");

            File.WriteAllText(sourceFile, "Original content");
            File.WriteAllText(targetFile, "Modified content");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var patchFile = Path.Combine(_patchDirectory, "test.txt.patch");
            Assert.True(File.Exists(patchFile), "Patch file should be created for modified files");
        }

        /// <summary>
        /// Tests that Clean copies new files directly to patch directory.
        /// </summary>
        [Fact]
        public async Task Clean_WithNewFiles_CopiesFilesToPatchDirectory()
        {
            // Arrange
            var targetFile = Path.Combine(_targetDirectory, "newfile.txt");
            File.WriteAllText(targetFile, "New file content");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var copiedFile = Path.Combine(_patchDirectory, "newfile.txt");
            Assert.True(File.Exists(copiedFile), "New file should be copied to patch directory");
            Assert.Equal("New file content", File.ReadAllText(copiedFile));
        }

        /// <summary>
        /// Tests that Clean generates delete list for removed files.
        /// </summary>
        [Fact]
        public async Task Clean_WithDeletedFiles_GeneratesDeleteList()
        {
            // Arrange
            var sourceFile = Path.Combine(_sourceDirectory, "deleted.txt");
            File.WriteAllText(sourceFile, "File to be deleted");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var deleteListFile = Path.Combine(_patchDirectory, "generalupdate_delete_files.json");
            Assert.True(File.Exists(deleteListFile), "Delete list file should be created");

            var deleteList = JsonSerializer.Deserialize<List<FileNode>>(
                File.ReadAllText(deleteListFile),
                FileNodesJsonContext.Default.ListFileNode);
            
            Assert.NotNull(deleteList);
            Assert.Single(deleteList);
            Assert.Equal("deleted.txt", deleteList[0].Name);
        }

        /// <summary>
        /// Tests that Clean handles files in subdirectories.
        /// </summary>
        [Fact]
        public async Task Clean_WithSubdirectories_GeneratesPatchesInCorrectStructure()
        {
            // Arrange
            var subDir = Path.Combine(_targetDirectory, "subfolder");
            Directory.CreateDirectory(subDir);

            var targetFile = Path.Combine(subDir, "test.txt");
            File.WriteAllText(targetFile, "Content in subdirectory");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var copiedFile = Path.Combine(_patchDirectory, "subfolder", "test.txt");
            Assert.True(File.Exists(copiedFile), "File in subdirectory should be copied with correct structure");
        }

        /// <summary>
        /// Tests that Clean handles identical files correctly (no patch generated).
        /// </summary>
        [Fact]
        public async Task Clean_WithIdenticalFiles_DoesNotGeneratePatch()
        {
            // Arrange
            var sourceFile = Path.Combine(_sourceDirectory, "same.txt");
            var targetFile = Path.Combine(_targetDirectory, "same.txt");

            File.WriteAllText(sourceFile, "Identical content");
            File.WriteAllText(targetFile, "Identical content");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var patchFile = Path.Combine(_patchDirectory, "same.txt.patch");
            Assert.False(File.Exists(patchFile), "Patch should not be generated for identical files");
        }

        /// <summary>
        /// Tests that Clean handles empty source directory.
        /// </summary>
        [Fact]
        public async Task Clean_WithEmptySourceDirectory_CopiesAllTargetFiles()
        {
            // Arrange
            var targetFile1 = Path.Combine(_targetDirectory, "file1.txt");
            var targetFile2 = Path.Combine(_targetDirectory, "file2.txt");

            File.WriteAllText(targetFile1, "File 1 content");
            File.WriteAllText(targetFile2, "File 2 content");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            Assert.True(File.Exists(Path.Combine(_patchDirectory, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(_patchDirectory, "file2.txt")));
        }

        /// <summary>
        /// Tests that Clean handles empty target directory.
        /// </summary>
        [Fact]
        public async Task Clean_WithEmptyTargetDirectory_GeneratesDeleteList()
        {
            // Arrange
            var sourceFile1 = Path.Combine(_sourceDirectory, "file1.txt");
            var sourceFile2 = Path.Combine(_sourceDirectory, "file2.txt");

            File.WriteAllText(sourceFile1, "File 1 content");
            File.WriteAllText(sourceFile2, "File 2 content");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var deleteListFile = Path.Combine(_patchDirectory, "generalupdate_delete_files.json");
            Assert.True(File.Exists(deleteListFile), "Delete list should be created");

            var deleteList = JsonSerializer.Deserialize<List<FileNode>>(
                File.ReadAllText(deleteListFile),
                FileNodesJsonContext.Default.ListFileNode);
            
            Assert.NotNull(deleteList);
            Assert.Equal(2, deleteList.Count);
        }

        /// <summary>
        /// Tests that Clean handles multiple file operations simultaneously.
        /// </summary>
        [Fact]
        public async Task Clean_WithMixedOperations_HandlesAllCorrectly()
        {
            // Arrange
            // File to be modified
            File.WriteAllText(Path.Combine(_sourceDirectory, "modified.txt"), "Original");
            File.WriteAllText(Path.Combine(_targetDirectory, "modified.txt"), "Modified");

            // File to be deleted
            File.WriteAllText(Path.Combine(_sourceDirectory, "deleted.txt"), "To delete");

            // New file
            File.WriteAllText(Path.Combine(_targetDirectory, "new.txt"), "New content");

            // Unchanged file
            File.WriteAllText(Path.Combine(_sourceDirectory, "unchanged.txt"), "Same");
            File.WriteAllText(Path.Combine(_targetDirectory, "unchanged.txt"), "Same");

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            // Check patch file for modified
            Assert.True(File.Exists(Path.Combine(_patchDirectory, "modified.txt.patch")));

            // Check new file copied
            Assert.True(File.Exists(Path.Combine(_patchDirectory, "new.txt")));

            // Check delete list
            var deleteListFile = Path.Combine(_patchDirectory, "generalupdate_delete_files.json");
            Assert.True(File.Exists(deleteListFile));

            var deleteList = JsonSerializer.Deserialize<List<FileNode>>(
                File.ReadAllText(deleteListFile),
                FileNodesJsonContext.Default.ListFileNode);
            
            Assert.NotNull(deleteList);
            Assert.Contains(deleteList, f => f.Name == "deleted.txt");

            // Unchanged file should not have patch
            Assert.False(File.Exists(Path.Combine(_patchDirectory, "unchanged.txt.patch")));
        }

        /// <summary>
        /// Tests that Clean handles binary files correctly.
        /// </summary>
        [Fact]
        public async Task Clean_WithBinaryFiles_GeneratesPatchFiles()
        {
            // Arrange
            var sourceFile = Path.Combine(_sourceDirectory, "binary.bin");
            var targetFile = Path.Combine(_targetDirectory, "binary.bin");

            File.WriteAllBytes(sourceFile, new byte[] { 0x00, 0x01, 0x02 });
            File.WriteAllBytes(targetFile, new byte[] { 0x00, 0x01, 0x03 });

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Assert
            var patchFile = Path.Combine(_patchDirectory, "binary.bin.patch");
            Assert.True(File.Exists(patchFile), "Patch file should be created for binary files");
        }

        #endregion

        #region Dirty Method Tests

        /// <summary>
        /// Tests that Dirty returns early if appPath doesn't exist.
        /// </summary>
        [Fact]
        public async Task Dirty_WithNonExistentAppPath_ReturnsWithoutError()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent");

            // Act & Assert (should not throw)
            await DifferentialCore.Instance.Dirty(nonExistentPath, _patchDirectory);
        }

        /// <summary>
        /// Tests that Dirty returns early if patchPath doesn't exist.
        /// </summary>
        [Fact]
        public async Task Dirty_WithNonExistentPatchPath_ReturnsWithoutError()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent");

            // Act & Assert (should not throw)
            await DifferentialCore.Instance.Dirty(_appDirectory, nonExistentPath);
        }

        /// <summary>
        /// Tests that Dirty applies patch files correctly.
        /// </summary>
        [Fact]
        public async Task Dirty_WithPatchFiles_AppliesPatches()
        {
            // Arrange
            var appFile = Path.Combine(_appDirectory, "test.txt");
            var sourceFile = Path.Combine(_sourceDirectory, "test.txt");
            var targetFile = Path.Combine(_targetDirectory, "test.txt");

            File.WriteAllText(sourceFile, "Original content");
            File.WriteAllText(targetFile, "Modified content");

            // Generate patch
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Copy source to app directory to simulate the application
            File.Copy(sourceFile, appFile);

            // Act - Apply patch
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            var appliedContent = File.ReadAllText(appFile);
            Assert.Equal("Modified content", appliedContent);
        }

        /// <summary>
        /// Tests that Dirty copies new files from patch directory.
        /// </summary>
        [Fact]
        public async Task Dirty_WithNewFiles_CopiesFilesToApp()
        {
            // Arrange
            var patchFile = Path.Combine(_patchDirectory, "newfile.txt");
            File.WriteAllText(patchFile, "New file content");

            // Act
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            var appFile = Path.Combine(_appDirectory, "newfile.txt");
            Assert.True(File.Exists(appFile), "New file should be copied to app directory");
            Assert.Equal("New file content", File.ReadAllText(appFile));
        }

        /// <summary>
        /// Tests that Dirty deletes files listed in delete list.
        /// </summary>
        [Fact]
        public async Task Dirty_WithDeleteList_DeletesFiles()
        {
            // Arrange
            var appFile = Path.Combine(_appDirectory, "todelete.txt");
            File.WriteAllText(appFile, "File to delete");

            // Create delete list
            var deleteList = new List<FileNode>
            {
                new FileNode
                {
                    Name = "todelete.txt",
                    FullName = appFile,
                    RelativePath = "todelete.txt",
                    Hash = new GeneralUpdate.Common.HashAlgorithms.Sha256HashAlgorithm().ComputeHash(appFile)
                }
            };

            var deleteListFile = Path.Combine(_patchDirectory, "generalupdate_delete_files.json");
            File.WriteAllText(deleteListFile, JsonSerializer.Serialize(deleteList, FileNodesJsonContext.Default.ListFileNode));

            // Act
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            Assert.False(File.Exists(appFile), "File should be deleted");
        }

        /// <summary>
        /// Tests that Dirty handles subdirectories correctly.
        /// </summary>
        [Fact]
        public async Task Dirty_WithSubdirectories_CopiesFilesWithStructure()
        {
            // Arrange
            var subDir = Path.Combine(_patchDirectory, "subfolder");
            Directory.CreateDirectory(subDir);

            var patchFile = Path.Combine(subDir, "test.txt");
            File.WriteAllText(patchFile, "Content in subdirectory");

            // Act
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            var appFile = Path.Combine(_appDirectory, "subfolder", "test.txt");
            Assert.True(File.Exists(appFile), "File should be copied with correct directory structure");
            Assert.Equal("Content in subdirectory", File.ReadAllText(appFile));
        }

        /// <summary>
        /// Tests that Dirty cleans up patch directory after applying.
        /// </summary>
        [Fact]
        public async Task Dirty_AfterApplying_RemovesPatchDirectory()
        {
            // Arrange
            var patchFile = Path.Combine(_patchDirectory, "test.txt");
            File.WriteAllText(patchFile, "Test content");

            // Act
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            Assert.False(Directory.Exists(_patchDirectory), "Patch directory should be removed after applying");
        }

        /// <summary>
        /// Tests that Dirty handles binary files correctly.
        /// </summary>
        [Fact]
        public async Task Dirty_WithBinaryFiles_AppliesPatchesCorrectly()
        {
            // Arrange
            var appFile = Path.Combine(_appDirectory, "binary.bin");
            var sourceFile = Path.Combine(_sourceDirectory, "binary.bin");
            var targetFile = Path.Combine(_targetDirectory, "binary.bin");

            var sourceBytes = new byte[] { 0x00, 0x01, 0x02 };
            var targetBytes = new byte[] { 0x00, 0x01, 0x03 };

            File.WriteAllBytes(sourceFile, sourceBytes);
            File.WriteAllBytes(targetFile, targetBytes);

            // Generate patch
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Copy source to app directory
            File.Copy(sourceFile, appFile);

            // Act
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            var appliedBytes = File.ReadAllBytes(appFile);
            Assert.Equal(targetBytes, appliedBytes);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests the full cycle of Clean and Dirty operations.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_FullCycle_UpdatesApplicationCorrectly()
        {
            // Arrange - Setup initial application state (source)
            File.WriteAllText(Path.Combine(_sourceDirectory, "file1.txt"), "Version 1.0");
            File.WriteAllText(Path.Combine(_sourceDirectory, "file2.txt"), "Unchanged");
            File.WriteAllText(Path.Combine(_sourceDirectory, "file3.txt"), "To be deleted");

            // Setup new version (target)
            File.WriteAllText(Path.Combine(_targetDirectory, "file1.txt"), "Version 2.0");
            File.WriteAllText(Path.Combine(_targetDirectory, "file2.txt"), "Unchanged");
            File.WriteAllText(Path.Combine(_targetDirectory, "file4.txt"), "New file");

            // Copy source to app to simulate the actual application
            foreach (var file in Directory.GetFiles(_sourceDirectory))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(_appDirectory, fileName));
            }

            // Act - Generate patches
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);

            // Apply patches
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            // Modified file should be updated
            Assert.Equal("Version 2.0", File.ReadAllText(Path.Combine(_appDirectory, "file1.txt")));

            // Unchanged file should remain
            Assert.Equal("Unchanged", File.ReadAllText(Path.Combine(_appDirectory, "file2.txt")));

            // Deleted file should be removed
            Assert.False(File.Exists(Path.Combine(_appDirectory, "file3.txt")));

            // New file should be added
            Assert.Equal("New file", File.ReadAllText(Path.Combine(_appDirectory, "file4.txt")));
        }

        /// <summary>
        /// Tests that Clean and Dirty handle complex directory structures.
        /// </summary>
        [Fact]
        public async Task CleanAndDirty_WithComplexStructure_UpdatesCorrectly()
        {
            // Arrange
            var sourceSubDir = Path.Combine(_sourceDirectory, "subdir");
            var targetSubDir = Path.Combine(_targetDirectory, "subdir");
            var appSubDir = Path.Combine(_appDirectory, "subdir");

            Directory.CreateDirectory(sourceSubDir);
            Directory.CreateDirectory(targetSubDir);

            File.WriteAllText(Path.Combine(sourceSubDir, "nested.txt"), "Original nested");
            File.WriteAllText(Path.Combine(targetSubDir, "nested.txt"), "Modified nested");
            File.WriteAllText(Path.Combine(targetSubDir, "new_nested.txt"), "New nested file");

            // Copy to app
            Directory.CreateDirectory(appSubDir);
            File.Copy(Path.Combine(sourceSubDir, "nested.txt"), Path.Combine(appSubDir, "nested.txt"));

            // Act
            await DifferentialCore.Instance.Clean(_sourceDirectory, _targetDirectory, _patchDirectory);
            await DifferentialCore.Instance.Dirty(_appDirectory, _patchDirectory);

            // Assert
            Assert.Equal("Modified nested", File.ReadAllText(Path.Combine(appSubDir, "nested.txt")));
            Assert.Equal("New nested file", File.ReadAllText(Path.Combine(appSubDir, "new_nested.txt")));
        }

        #endregion
    }
}
