using Moq;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Core.Differential;

namespace DifferentialTest.Matchers
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. 无参构造函数 — matcher=DefaultCleanMatcher, differ=StreamingHdiffDiffer
    ///   2. 带参构造函数 — matcher=null → DefaultCleanMatcher
    ///   3. 带参构造函数 — binaryDiffer=null → StreamingHdiffDiffer
    ///   4. ExecuteAsync — 文件系统集成测试 & Mock 分支
    ///
    /// 触发条件：构造参数组合 / 文件状态 (新增/修改/相同/删除/空目录)
    /// 预期结果：正确默认值 / 正确生成补丁 / 跳过相同文件 / 生成删除列表
    /// </summary>
    public class DefaultCleanStrategyTests : IDisposable
    {
        private readonly string _testDir;

        public DefaultCleanStrategyTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"CleanStrategyTests_{Guid.NewGuid():N}");
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
            var strategy = new DefaultCleanStrategy();
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_matcher为null_使用DefaultCleanMatcher")]
        public void Constructor_NullMatcher_UsesDefaultCleanMatcher()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultCleanStrategy(matcher: null, binaryDiffer: mockDiffer.Object);
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_binaryDiffer为null_使用StreamingHdiffDiffer")]
        public void Constructor_NullDiffer_UsesStreamingHdiffDiffer()
        {
            var mockMatcher = new Mock<ICleanMatcher>();
            var strategy = new DefaultCleanStrategy(matcher: mockMatcher.Object, binaryDiffer: null);
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_全部自定义_使用自定义实例")]
        public void Constructor_AllCustom_UsesCustomInstances()
        {
            var mockMatcher = new Mock<ICleanMatcher>();
            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultCleanStrategy(mockMatcher.Object, mockDiffer.Object);
            Assert.NotNull(strategy);
        }

        #endregion

        #region ExecuteAsync tests — using mocked matcher & differ

        [Fact(DisplayName = "ExecuteAsync_目标有新文件但源目录不存在_直接复制到补丁目录")]
        public async Task ExecuteAsync_NewFileExistsOnlyInTarget_CopiesFileToPatchDir()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            // Real file in target only
            var newFilePath = Path.Combine(targetDir, "brandnew.dll");
            File.WriteAllText(newFilePath, "brand new file content");

            var newFileNode = new FileNode
            {
                Id = 1,
                Name = "brandnew.dll",
                FullName = newFilePath,
                Path = targetDir,
                Hash = "some_hash",
                RelativePath = "brandnew.dll"
            };

            var comparisonResult = new ComparisonResult();
            comparisonResult.AddDifferent(new[] { newFileNode });
            // LeftNodes is empty — no corresponding old file

            var mockMatcher = new Mock<ICleanMatcher>();
            mockMatcher
                .Setup(m => m.Compare(sourceDir, targetDir))
                .Returns(comparisonResult);
            mockMatcher
                .Setup(m => m.Match(It.IsAny<FileNode>(), It.IsAny<IEnumerable<FileNode>>()))
                .Returns((FileNode?)null);
            mockMatcher
                .Setup(m => m.Except(sourceDir, targetDir))
                .Returns(Array.Empty<FileNode>());

            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultCleanStrategy(mockMatcher.Object, mockDiffer.Object);

            // Act
            await strategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Assert — file was copied directly (no diff needed for brand-new files)
            var copiedPath = Path.Combine(patchDir, "brandnew.dll");
            Assert.True(File.Exists(copiedPath));
            Assert.Equal("brand new file content", File.ReadAllText(copiedPath));
        }

        [Fact(DisplayName = "ExecuteAsync_文件已修改_生成补丁文件")]
        public async Task ExecuteAsync_ModifiedFile_GeneratesPatch()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            // Different content in source vs target
            var oldFilePath = Path.Combine(sourceDir, "lib.dll");
            var newFilePath = Path.Combine(targetDir, "lib.dll");
            File.WriteAllText(oldFilePath, "old library version 1.0");
            File.WriteAllText(newFilePath, "new library version 2.0 updated");

            var oldFileNode = new FileNode
            {
                Id = 1, Name = "lib.dll", FullName = oldFilePath,
                Path = sourceDir, RelativePath = "lib.dll"
            };
            var newFileNode = new FileNode
            {
                Id = 2, Name = "lib.dll", FullName = newFilePath,
                Path = targetDir, RelativePath = "lib.dll"
            };

            var comparisonResult = new ComparisonResult();
            comparisonResult.AddToLeft(new[] { oldFileNode });
            comparisonResult.AddDifferent(new[] { newFileNode });

            var mockMatcher = new Mock<ICleanMatcher>();
            mockMatcher
                .Setup(m => m.Compare(sourceDir, targetDir))
                .Returns(comparisonResult);
            mockMatcher
                .Setup(m => m.Match(It.IsAny<FileNode>(), It.IsAny<IEnumerable<FileNode>>()))
                .Returns(oldFileNode);
            mockMatcher
                .Setup(m => m.Except(sourceDir, targetDir))
                .Returns(Array.Empty<FileNode>());

            var mockDiffer = new Mock<IBinaryDiffer>();
            mockDiffer
                .Setup(d => d.CleanAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, string, CancellationToken>((_, _, patchFilePath, _) =>
                {
                    // Simulate creating a patch file
                    File.WriteAllText(patchFilePath, "PATCH_DATA");
                })
                .Returns(Task.CompletedTask);

            var strategy = new DefaultCleanStrategy(mockMatcher.Object, mockDiffer.Object);

            // Act
            await strategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Assert
            mockDiffer.Verify(
                d => d.CleanAsync(
                    oldFilePath, newFilePath,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());

            var patchFiles = Directory.GetFiles(patchDir, "*.patch", SearchOption.AllDirectories);
            Assert.NotEmpty(patchFiles);
        }

        [Fact(DisplayName = "ExecuteAsync_文件未更改_不生成补丁")]
        public async Task ExecuteAsync_IdenticalFile_NoPatchGenerated()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            // Identical content in both directories
            var fileName = "unchanged.dll";
            var content = "same content everywhere";
            File.WriteAllText(Path.Combine(sourceDir, fileName), content);
            File.WriteAllText(Path.Combine(targetDir, fileName), content);

            // Use the default strategy (real matcher and differ) —
            // identical files will be excluded from DifferentNodes
            var strategy = new DefaultCleanStrategy();

            // Act
            await strategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Assert — no patch should be generated for unchanged files
            var patchFiles = Directory.GetFiles(patchDir, "*.patch", SearchOption.AllDirectories);
            Assert.Empty(patchFiles);
        }

        [Fact(DisplayName = "ExecuteAsync_源目录有文件但目标目录缺失_写入删除列表JSON")]
        public async Task ExecuteAsync_DeletedFile_WritesDeleteListJson()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var deletedFileNode = new FileNode
            {
                Id = 1,
                Name = "removed.dll",
                FullName = Path.Combine(sourceDir, "removed.dll"),
                Path = sourceDir,
                Hash = "deadbeef",
                RelativePath = "removed.dll"
            };

            var comparisonResult = new ComparisonResult();
            // DifferentNodes is empty — no files to diff
            // Except returns the deleted file

            var mockMatcher = new Mock<ICleanMatcher>();
            mockMatcher
                .Setup(m => m.Compare(sourceDir, targetDir))
                .Returns(comparisonResult);
            mockMatcher
                .Setup(m => m.Except(sourceDir, targetDir))
                .Returns(new[] { deletedFileNode });

            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultCleanStrategy(mockMatcher.Object, mockDiffer.Object);

            // Act
            await strategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Assert — delete list JSON should be written
            var jsonPath = Path.Combine(patchDir, "generalupdate_delete_files.json");
            Assert.True(File.Exists(jsonPath));

            var jsonContent = File.ReadAllText(jsonPath);
            Assert.Contains("removed.dll", jsonContent);
        }

        [Fact(DisplayName = "ExecuteAsync_空目录_无异常完成")]
        public async Task ExecuteAsync_EmptyDirectories_CompletesWithoutError()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "empty_source");
            var targetDir = Path.Combine(_testDir, "empty_target");
            var patchDir = Path.Combine(_testDir, "empty_patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var strategy = new DefaultCleanStrategy();

            // Act
            await strategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Assert — no exception thrown, no files created
            Assert.Empty(Directory.GetFiles(patchDir, "*", SearchOption.AllDirectories));
        }

        [Fact(DisplayName = "ExecuteAsync_使用MockDiffer_验证CleanAsync被调用")]
        public async Task ExecuteAsync_WithMockedDiffer_VerifiesCleanAsyncCalled()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var oldFilePath = Path.Combine(sourceDir, "changed.dll");
            var newFilePath = Path.Combine(targetDir, "changed.dll");
            File.WriteAllText(oldFilePath, "old data");
            File.WriteAllText(newFilePath, "new data that is different");

            var oldFileNode = new FileNode
            {
                Id = 1, Name = "changed.dll", FullName = oldFilePath,
                Path = sourceDir, RelativePath = "changed.dll"
            };
            var newFileNode = new FileNode
            {
                Id = 2, Name = "changed.dll", FullName = newFilePath,
                Path = targetDir, RelativePath = "changed.dll"
            };

            var comparisonResult = new ComparisonResult();
            comparisonResult.AddToLeft(new[] { oldFileNode });
            comparisonResult.AddDifferent(new[] { newFileNode });

            var mockMatcher = new Mock<ICleanMatcher>();
            mockMatcher
                .Setup(m => m.Compare(sourceDir, targetDir))
                .Returns(comparisonResult);
            mockMatcher
                .Setup(m => m.Match(It.IsAny<FileNode>(), It.IsAny<IEnumerable<FileNode>>()))
                .Returns(oldFileNode);
            mockMatcher
                .Setup(m => m.Except(sourceDir, targetDir))
                .Returns(Array.Empty<FileNode>());

            var mockDiffer = new Mock<IBinaryDiffer>();
            mockDiffer
                .Setup(d => d.CleanAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var strategy = new DefaultCleanStrategy(mockMatcher.Object, mockDiffer.Object);

            // Act
            await strategy.ExecuteAsync(sourceDir, targetDir, patchDir);

            // Assert
            mockDiffer.Verify(
                d => d.CleanAsync(
                    oldFilePath,
                    newFilePath,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }

        #endregion
    }
}
