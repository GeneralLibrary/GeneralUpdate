using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Differential;
using Xunit;

namespace DifferentialTest.Matchers
{
    /// <summary>
    /// Tests for <see cref="DefaultCleanMatcher"/>, <see cref="DefaultDirtyMatcher"/>,
    /// and the custom matcher injection points on <see cref="DifferentialCore"/>.
    /// </summary>
    public class MatcherTests : IDisposable
    {
        private readonly string _testDirectory;

        public MatcherTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"MatcherTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try { Directory.Delete(_testDirectory, true); }
                catch { /* ignore cleanup errors */ }
            }
        }

        #region DefaultCleanMatcher — Compare and Except

        [Fact]
        public void DefaultCleanMatcher_Compare_ReturnsDifferentNodes_WhenFilesChange()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDirectory, "cmp_source");
            var targetDir = Path.Combine(_testDirectory, "cmp_target");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);

            File.WriteAllText(Path.Combine(sourceDir, "a.txt"), "old");
            File.WriteAllText(Path.Combine(targetDir, "a.txt"), "new");

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Compare(sourceDir, targetDir);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.DifferentNodes);
        }

        [Fact]
        public void DefaultCleanMatcher_Except_ReturnsDeletedFiles()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDirectory, "exc_source");
            var targetDir = Path.Combine(_testDirectory, "exc_target");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);

            File.WriteAllText(Path.Combine(sourceDir, "deleted.txt"), "gone");

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Except(sourceDir, targetDir);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(result!, f => f.Name == "deleted.txt");
        }

        #endregion

        #region DefaultCleanMatcher

        [Fact]
        public void DefaultCleanMatcher_ReturnsOldFile_WhenNamesAndPathsMatch()
        {
            // Arrange
            var file = Path.Combine(_testDirectory, "a.txt");
            File.WriteAllText(file, "content");

            var newFile = new FileNode { Name = "a.txt", FullName = file, RelativePath = "a.txt" };
            var oldFile = new FileNode { Name = "a.txt", FullName = file, RelativePath = "a.txt" };
            var leftNodes = new List<FileNode> { oldFile };

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, leftNodes);

            // Assert
            Assert.NotNull(result);
            Assert.Same(oldFile, result);
        }

        [Fact]
        public void DefaultCleanMatcher_ReturnsNull_WhenNameNotFound()
        {
            // Arrange
            var file = Path.Combine(_testDirectory, "a.txt");
            File.WriteAllText(file, "content");

            var newFile = new FileNode { Name = "b.txt", FullName = file, RelativePath = "b.txt" };
            var oldFile = new FileNode { Name = "a.txt", FullName = file, RelativePath = "a.txt" };
            var leftNodes = new List<FileNode> { oldFile };

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, leftNodes);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DefaultCleanMatcher_ReturnsNull_WhenRelativePathsDiffer()
        {
            // Arrange
            var file = Path.Combine(_testDirectory, "a.txt");
            File.WriteAllText(file, "content");

            var newFile = new FileNode { Name = "a.txt", FullName = file, RelativePath = "sub/a.txt" };
            var oldFile = new FileNode { Name = "a.txt", FullName = file, RelativePath = "a.txt" };
            var leftNodes = new List<FileNode> { oldFile };

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, leftNodes);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DefaultCleanMatcher_ReturnsNull_WhenOldFileDoesNotExist()
        {
            // Arrange
            var existingFile = Path.Combine(_testDirectory, "new.txt");
            File.WriteAllText(existingFile, "content");

            var newFile = new FileNode { Name = "a.txt", FullName = existingFile, RelativePath = "a.txt" };
            var oldFile = new FileNode { Name = "a.txt", FullName = Path.Combine(_testDirectory, "nonexistent.txt"), RelativePath = "a.txt" };
            var leftNodes = new List<FileNode> { oldFile };

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, leftNodes);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DefaultCleanMatcher_ReturnsNull_WhenNewFileDoesNotExist()
        {
            // Arrange
            var existingFile = Path.Combine(_testDirectory, "old.txt");
            File.WriteAllText(existingFile, "content");

            var newFile = new FileNode { Name = "a.txt", FullName = Path.Combine(_testDirectory, "nonexistent.txt"), RelativePath = "a.txt" };
            var oldFile = new FileNode { Name = "a.txt", FullName = existingFile, RelativePath = "a.txt" };
            var leftNodes = new List<FileNode> { oldFile };

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, leftNodes);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DefaultCleanMatcher_ReturnsCorrectNode_WhenSameNameFilesExistInDifferentDirectories()
        {
            // Arrange - two old files with the same name but different relative paths
            var fileInRoot = Path.Combine(_testDirectory, "a.txt");
            var subDir = Path.Combine(_testDirectory, "sub");
            Directory.CreateDirectory(subDir);
            var fileInSub = Path.Combine(subDir, "a.txt");
            File.WriteAllText(fileInRoot, "root");
            File.WriteAllText(fileInSub, "sub");

            // The new file lives in the sub-directory
            var newFile = new FileNode { Name = "a.txt", FullName = fileInSub, RelativePath = "sub/a.txt" };
            var oldFileRoot = new FileNode { Name = "a.txt", FullName = fileInRoot, RelativePath = "a.txt" };
            var oldFileSub  = new FileNode { Name = "a.txt", FullName = fileInSub,  RelativePath = "sub/a.txt" };

            // oldFileRoot appears first in the list - before the fix FirstOrDefault would
            // return oldFileRoot, then the RelativePath guard would return null, hiding oldFileSub.
            var leftNodes = new List<FileNode> { oldFileRoot, oldFileSub };

            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, leftNodes);

            // Assert - must find oldFileSub, not be tricked by oldFileRoot
            Assert.NotNull(result);
            Assert.Same(oldFileSub, result);
        }

        [Fact]
        public void DefaultCleanMatcher_ReturnsNull_WhenLeftNodesIsEmpty()
        {
            // Arrange
            var file = Path.Combine(_testDirectory, "a.txt");
            File.WriteAllText(file, "content");

            var newFile = new FileNode { Name = "a.txt", FullName = file, RelativePath = "a.txt" };
            var matcher = new DefaultCleanMatcher();

            // Act
            var result = matcher.Match(newFile, new List<FileNode>());

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region DefaultDirtyMatcher

        [Fact]
        public void DefaultDirtyMatcher_ReturnsPatchFile_WhenNameMatchesWithPatchExtension()
        {
            // Arrange
            var patchFilePath = Path.Combine(_testDirectory, "app.exe.patch");
            File.WriteAllText(patchFilePath, "patch");

            var oldFile = new FileInfo(Path.Combine(_testDirectory, "app.exe"));
            var patchFiles = new List<FileInfo> { new FileInfo(patchFilePath) };

            var matcher = new DefaultDirtyMatcher();

            // Act
            var result = matcher.Match(oldFile, patchFiles);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(patchFilePath, result!.FullName);
        }

        [Fact]
        public void DefaultDirtyMatcher_ReturnsNull_WhenNoPatchFileExists()
        {
            // Arrange
            var oldFile = new FileInfo(Path.Combine(_testDirectory, "app.exe"));
            var patchFiles = new List<FileInfo>
            {
                new FileInfo(Path.Combine(_testDirectory, "other.exe.patch"))
            };

            var matcher = new DefaultDirtyMatcher();

            // Act
            var result = matcher.Match(oldFile, patchFiles);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DefaultDirtyMatcher_ReturnsNull_WhenFileExistsButNotPatchExtension()
        {
            // Arrange
            var nonPatchFile = Path.Combine(_testDirectory, "app.exe.zip");
            File.WriteAllText(nonPatchFile, "data");

            var oldFile = new FileInfo(Path.Combine(_testDirectory, "app.exe"));
            var patchFiles = new List<FileInfo> { new FileInfo(nonPatchFile) };

            var matcher = new DefaultDirtyMatcher();

            // Act
            var result = matcher.Match(oldFile, patchFiles);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DefaultDirtyMatcher_ReturnsNull_WhenPatchFilesIsEmpty()
        {
            // Arrange
            var oldFile = new FileInfo(Path.Combine(_testDirectory, "app.exe"));
            var matcher = new DefaultDirtyMatcher();

            // Act
            var result = matcher.Match(oldFile, new List<FileInfo>());

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Helper types

        /// <summary>
        /// Custom <see cref="ICleanMatcher"/> that uses the default directory comparison logic
        /// but always returns <c>null</c> from <see cref="ICleanMatcher.Match"/> so every file
        /// is treated as new.
        /// </summary>
        private sealed class AlwaysNewFileMatcher : ICleanMatcher
        {
            private readonly DefaultCleanMatcher _inner = new DefaultCleanMatcher();

            public ComparisonResult Compare(string sourcePath, string targetPath)
                => _inner.Compare(sourcePath, targetPath);

            public IEnumerable<FileNode>? Except(string sourcePath, string targetPath)
                => _inner.Except(sourcePath, targetPath);

            public FileNode? Match(FileNode newFile, IEnumerable<FileNode> leftNodes) => null;
        }

        /// <summary>Custom <see cref="IDirtyMatcher"/> that never finds a patch file.</summary>
        private sealed class NeverMatchDirtyMatcher : IDirtyMatcher
        {
            public FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles) => null;
        }

        #endregion
    }
}
