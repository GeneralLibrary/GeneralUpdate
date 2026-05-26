using Moq;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;

namespace DifferentialTest.Matchers
{
    /// <summary>
    /// Tests for <see cref="DefaultDirtyStrategy"/> — constructor validation
    /// and ExecuteAsync behavioural coverage (patch application, delete list,
    /// unknown-file copying, edge cases, and mocked-differ verification).
    /// </summary>
    public class DefaultDirtyStrategyTests : IDisposable
    {
        private readonly string _testDir;

        public DefaultDirtyStrategyTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"DirtyStrategyTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        #region Constructor tests (preserved)

        [Fact(DisplayName = "构造函数_无参_使用默认matcher和differ")]
        public void Constructor_NoArgs_UsesDefaults()
        {
            var strategy = new DefaultDirtyStrategy();
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_matcher为null_使用DefaultDirtyMatcher")]
        public void Constructor_NullMatcher_UsesDefaultDirtyMatcher()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultDirtyStrategy(matcher: null, binaryDiffer: mockDiffer.Object);
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_binaryDiffer为null_使用StreamingHdiffDiffer")]
        public void Constructor_NullDiffer_UsesStreamingHdiffDiffer()
        {
            var mockMatcher = new Mock<IDirtyMatcher>();
            var strategy = new DefaultDirtyStrategy(matcher: mockMatcher.Object, binaryDiffer: null);
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_全部自定义_使用自定义实例")]
        public void Constructor_AllCustom_UsesCustomInstances()
        {
            var mockMatcher = new Mock<IDirtyMatcher>();
            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultDirtyStrategy(mockMatcher.Object, mockDiffer.Object);
            Assert.NotNull(strategy);
        }

        #endregion

        #region ExecuteAsync tests

        [Fact(DisplayName = "ExecuteAsync_完整往返_正确应用补丁")]
        public async Task ExecuteAsync_AppliesPatchCorrectly()
        {
            // Arrange — generate a patch with DefaultCleanStrategy, then apply with DefaultDirtyStrategy
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            var fileName = "core.dll";
            var oldContent = "core assembly version 1.0 payload";
            var newContent = "core assembly version 2.0 payload with additional features";

            // Old version lives in source and app
            File.WriteAllText(Path.Combine(sourceDir, fileName), oldContent);
            File.WriteAllText(Path.Combine(appDir, fileName), oldContent);
            // New version in target
            File.WriteAllText(Path.Combine(targetDir, fileName), newContent);

            // Generate patch via Clean strategy
            var cleanStrategy = new DefaultCleanStrategy();
            await cleanStrategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Act — apply the patch
            var dirtyStrategy = new DefaultDirtyStrategy();
            await dirtyStrategy.ExecuteAsync(appDir, patchDir);

            // Assert — file in appDir should now match the target version
            var resultPath = Path.Combine(appDir, fileName);
            Assert.True(File.Exists(resultPath));
            Assert.Equal(newContent, File.ReadAllText(resultPath));
        }

        [Fact(DisplayName = "ExecuteAsync_删除列表_删除匹配哈希的文件")]
        public async Task ExecuteAsync_DeleteList_DeletesMatchingFiles()
        {
            // Arrange
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            // File to be deleted
            var deleteFilePath = Path.Combine(appDir, "obsolete.dll");
            File.WriteAllText(deleteFilePath, "obsolete content to be removed");

            // Compute actual SHA-256 hash of the file
            var hashAlgorithm = new Sha256HashAlgorithm();
            var fileHash = hashAlgorithm.ComputeHash(deleteFilePath);

            // Create the delete-list JSON with the matching hash
            var deleteNodes = new List<FileNode>
            {
                new FileNode
                {
                    Id = 1,
                    Name = "obsolete.dll",
                    FullName = deleteFilePath,
                    Path = appDir,
                    Hash = fileHash,
                    RelativePath = "obsolete.dll"
                }
            };

            var jsonPath = Path.Combine(patchDir, "generalupdate_delete_files.json");
            StorageManager.CreateJson(jsonPath, deleteNodes, FileNodesJsonContext.Default.ListFileNode);

            // Also place a dummy patch file for the delete-list JSON to be found by GetAllFiles
            File.WriteAllText(Path.Combine(patchDir, "other.dll.patch"), "dummy patch data");

            var strategy = new DefaultDirtyStrategy();

            // Act
            await strategy.ExecuteAsync(appDir, patchDir);

            // Assert — the obsolete file should be deleted
            Assert.False(File.Exists(deleteFilePath));
        }

        [Fact(DisplayName = "ExecuteAsync_仅补丁目录存在未知文件_复制到应用目录")]
        public async Task ExecuteAsync_UnknownFiles_CopiedToAppPath()
        {
            // Arrange
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            // File that exists ONLY in the patch directory (not matched to any app file)
            var unknownFileName = "readme.txt";
            var unknownContent = "This file is brand-new in the update";
            File.WriteAllText(Path.Combine(patchDir, unknownFileName), unknownContent);

            // Also include a .patch file to satisfy GetAllFiles returning something meaningful
            File.WriteAllText(Path.Combine(patchDir, "somefile.dll.patch"), "binary patch content");

            var strategy = new DefaultDirtyStrategy();

            // Act
            await strategy.ExecuteAsync(appDir, patchDir);

            // Assert — unknown file should be copied to the app directory
            var copiedPath = Path.Combine(appDir, unknownFileName);
            Assert.True(File.Exists(copiedPath));
            Assert.Equal(unknownContent, File.ReadAllText(copiedPath));
        }

        [Fact(DisplayName = "ExecuteAsync_目录不存在_无异常直接返回")]
        public async Task ExecuteAsync_NonExistentDirectories_ReturnsWithoutError()
        {
            // Arrange
            var nonExistentApp = Path.Combine(_testDir, "no_app");
            var nonExistentPatch = Path.Combine(_testDir, "no_patch");
            // Intentionally do NOT create these directories

            var strategy = new DefaultDirtyStrategy();

            // Act
            await strategy.ExecuteAsync(nonExistentApp, nonExistentPatch);

            // Assert — no exception thrown
            Assert.False(Directory.Exists(nonExistentApp));
            Assert.False(Directory.Exists(nonExistentPatch));
        }

        [Fact(DisplayName = "ExecuteAsync_使用MockDiffer_验证DirtyAsync被调用")]
        public async Task ExecuteAsync_WithMockedDiffer_VerifiesDirtyAsyncCalled()
        {
            // Arrange
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            // Real files on disk so ApplyPatch's File.Exists check passes
            var appFilePath = Path.Combine(appDir, "engine.dll");
            var patchFilePath = Path.Combine(patchDir, "engine.dll.patch");
            File.WriteAllText(appFilePath, "old engine binary");
            File.WriteAllText(patchFilePath, "engine patch data");

            // Mock matcher: match engine.dll → engine.dll.patch
            var mockMatcher = new Mock<IDirtyMatcher>();
            mockMatcher
                .Setup(m => m.Match(It.IsAny<FileInfo>(), It.IsAny<IEnumerable<FileInfo>>()))
                .Returns((FileInfo appFile, IEnumerable<FileInfo> patchFiles) =>
                {
                    return patchFiles.FirstOrDefault(f => f.Name == appFile.Name + ".patch");
                });

            // Mock differ: verify DirtyAsync is called with the correct paths
            var mockDiffer = new Mock<IBinaryDiffer>();
            mockDiffer
                .Setup(d => d.DirtyAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var strategy = new DefaultDirtyStrategy(mockMatcher.Object, mockDiffer.Object);

            // Act
            await strategy.ExecuteAsync(appDir, patchDir);

            // Assert — differ.DirtyAsync must be called for the matched file
            mockDiffer.Verify(
                d => d.DirtyAsync(
                    appFilePath,
                    It.IsAny<string>(),     // temp path is random
                    patchFilePath,
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        #endregion
    }
}
