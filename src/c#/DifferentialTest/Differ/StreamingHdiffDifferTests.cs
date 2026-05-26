using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;

namespace DifferentialTest.Differ
{
    /// <summary>
    /// StreamingHdiffDiffer 单元测试。
    ///
    /// 覆盖点：
    ///   1. 无参构造函数 — 使用 DeflateCompressionProvider 默认值
    ///   2. 有参构造函数 — 自定义压缩提供者和参数
    ///   3. 构造函数 — null provider → ArgumentNullException
    ///   4. 构造函数 — blockSize≤0 → ArgumentOutOfRangeException
    ///   5. 构造函数 — maxWindowSize≤0 → ArgumentOutOfRangeException
    ///   6. CleanAsync — null/空路径 → ArgumentNullException
    ///   7. CleanAsync — CancellationToken已取消 → OperationCanceledException
    ///   8. CleanAsync — 文件不存在 → FileNotFoundException
    ///   9. Clean — 相同文件生成最小补丁
    ///  10. Clean — 不同文件生成补丁
    ///  11. Clean — 空文件正确处理
    ///  12. CleanThenDirty — 相同文件往返验证
    ///  13. CleanThenDirty — 修改文件往返验证
    ///  14. CleanThenDirty — 完全不同文件往返验证
    ///  15. CleanThenDirty — 空旧文件生成新文件
    ///  16. DirtyAsync — null路径 → ArgumentNullException
    ///  17. DirtyAsync — CancellationToken已取消 → OperationCanceledException
    ///  18. Dirty — 不存在的补丁文件 → 抛出异常
    ///  19. 自定义BlockSize — 功能验证往返
    /// </summary>
    public class StreamingHdiffDifferTests : IDisposable
    {
        private readonly string _testDir;

        public StreamingHdiffDifferTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"StreamingHdiffDifferTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        #region Constructor Tests

        [Fact(DisplayName = "无参构造函数_使用默认DeflateCompressionProvider和默认参数")]
        public void Constructor_NoArgs_UsesDefaults()
        {
            // Arrange & Act
            var differ = new StreamingHdiffDiffer();

            // Assert
            Assert.NotNull(differ);
            Assert.Equal(64 * 1024, differ.BlockSize);
            Assert.Equal(128 * 1024 * 1024, differ.MaxWindowSize);
        }

        [Fact(DisplayName = "有参构造函数_自定义参数_正确创建实例")]
        public void Constructor_ValidParams_CreatesInstance()
        {
            // Arrange
            var mockProvider = new Mock<ICompressionProvider>();
            mockProvider.Setup(p => p.FormatVersion).Returns(0x01);
            const int blockSize = 32 * 1024;
            const int maxWindowSize = 64 * 1024 * 1024;

            // Act
            var differ = new StreamingHdiffDiffer(mockProvider.Object, blockSize, maxWindowSize);

            // Assert
            Assert.NotNull(differ);
            Assert.Equal(blockSize, differ.BlockSize);
            Assert.Equal(maxWindowSize, differ.MaxWindowSize);
        }

        [Fact(DisplayName = "构造函数_compressionProvider为null_抛出ArgumentNullException")]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new StreamingHdiffDiffer(null!, 64 * 1024, 128 * 1024 * 1024));

            Assert.Equal("compressionProvider", ex.ParamName);
        }

        [Fact(DisplayName = "构造函数_blockSize为零_抛出ArgumentOutOfRangeException")]
        public void Constructor_ZeroBlockSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var mockProvider = new Mock<ICompressionProvider>();
            mockProvider.Setup(p => p.FormatVersion).Returns(0x01);

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StreamingHdiffDiffer(mockProvider.Object, 0, 128 * 1024 * 1024));

            Assert.Equal("blockSize", ex.ParamName);
        }

        [Fact(DisplayName = "构造函数_maxWindowSize为负数_抛出ArgumentOutOfRangeException")]
        public void Constructor_NegativeMaxWindowSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var mockProvider = new Mock<ICompressionProvider>();
            mockProvider.Setup(p => p.FormatVersion).Returns(0x01);

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StreamingHdiffDiffer(mockProvider.Object, 64 * 1024, -1));

            Assert.Equal("maxWindowSize", ex.ParamName);
        }

        #endregion Constructor Tests

        #region CleanAsync Tests

        [Fact(DisplayName = "CleanAsync_oldFilePath为null_抛出ArgumentNullException")]
        public async Task CleanAsync_NullOldPath_ThrowsArgumentNullException()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var newPath = Path.Combine(_testDir, "new.bin");
            var patchPath = Path.Combine(_testDir, "patch.bin");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                differ.CleanAsync(null!, newPath, patchPath));
        }

        [Fact(DisplayName = "CleanAsync_newFilePath为空白字符串_抛出ArgumentNullException")]
        public async Task CleanAsync_WhitespaceNewPath_ThrowsArgumentNullException()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldPath = Path.Combine(_testDir, "old.bin");
            var patchPath = Path.Combine(_testDir, "patch.bin");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                differ.CleanAsync(oldPath, "   ", patchPath));
        }

        [Fact(DisplayName = "CleanAsync_CancellationToken已取消_抛出OperationCanceledException")]
        public async Task CleanAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                differ.CleanAsync("old", "new", "patch", cts.Token));
        }

        [Fact(DisplayName = "Clean_文件不存在_抛出FileNotFoundException")]
        public async Task Clean_NonExistentFiles_Throws()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                differ.CleanAsync(
                    Path.Combine(_testDir, "nonexistent_old.bin"),
                    Path.Combine(_testDir, "nonexistent_new.bin"),
                    Path.Combine(_testDir, "output.patch")));
        }

        [Fact(DisplayName = "Clean_相同文件_生成最小补丁")]
        public async Task Clean_IdenticalFiles_GeneratesMinimalPatch()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            var data = new byte[2048];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(oldFile, data);
            File.WriteAllBytes(newFile, data);

            // Act
            await differ.CleanAsync(oldFile, newFile, patchFile);

            // Assert
            Assert.True(File.Exists(patchFile));
            Assert.True(new FileInfo(patchFile).Length > 0, "补丁文件不应为空");
        }

        [Fact(DisplayName = "Clean_后半部分不同的文件_生成补丁")]
        public async Task Clean_DifferentFiles_GeneratesPatch()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            // 4KB 文件，前半相同，后半不同
            var oldData = new byte[4096];
            new Random(100).NextBytes(oldData);
            File.WriteAllBytes(oldFile, oldData);

            var newData = new byte[4096];
            Array.Copy(oldData, 0, newData, 0, 2048); // 前半相同
            var suffix = new byte[2048];
            new Random(200).NextBytes(suffix);
            Array.Copy(suffix, 0, newData, 2048, 2048); // 后半不同
            File.WriteAllBytes(newFile, newData);

            // Act
            await differ.CleanAsync(oldFile, newFile, patchFile);

            // Assert
            Assert.True(File.Exists(patchFile));
            Assert.True(new FileInfo(patchFile).Length > 0, "补丁文件不应为空");
        }

        [Fact(DisplayName = "Clean_空文件_正常处理")]
        public async Task Clean_EmptyFiles_HandlesGracefully()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "empty_old.bin");
            var newFile = Path.Combine(_testDir, "empty_new.bin");
            var patchFile = Path.Combine(_testDir, "empty_patch.bin");

            File.WriteAllBytes(oldFile, Array.Empty<byte>());
            File.WriteAllBytes(newFile, Array.Empty<byte>());

            // Act
            await differ.CleanAsync(oldFile, newFile, patchFile);

            // Assert
            Assert.True(File.Exists(patchFile));
            Assert.True(new FileInfo(patchFile).Length > 0, "即使空文件也应生成有效的BSDIF补丁头");
        }

        #endregion CleanAsync Tests

        #region CleanThenDirty Round-Trip Tests

        [Fact(DisplayName = "CleanThenDirty_相同文件_生成与源文件一致的结果")]
        public async Task CleanThenDirty_IdenticalFiles_ProducesIdenticalOutput()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            var data = new byte[4096];
            new Random(77).NextBytes(data);
            File.WriteAllBytes(oldFile, data);
            File.WriteAllBytes(newFile, data);

            // Act — Clean: 生成补丁
            await differ.CleanAsync(oldFile, newFile, patchFile);
            Assert.True(File.Exists(patchFile));

            // Act — Dirty: 应用补丁
            await differ.DirtyAsync(oldFile, patchedFile, patchFile);
            Assert.True(File.Exists(patchedFile));

            // Assert
            var resultData = File.ReadAllBytes(patchedFile);
            Assert.Equal(data, resultData);
        }

        [Fact(DisplayName = "CleanThenDirty_部分修改文件_生成正确结果")]
        public async Task CleanThenDirty_ModifiedFiles_ProducesExpectedOutput()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            // 4KB 旧文件，新文件前半2KB相同、后半2KB不同
            var oldData = new byte[4096];
            new Random(88).NextBytes(oldData);
            File.WriteAllBytes(oldFile, oldData);

            var newData = new byte[4096];
            Array.Copy(oldData, 0, newData, 0, 2048); // 前半相同
            var suffix = new byte[2048];
            new Random(99).NextBytes(suffix);
            Array.Copy(suffix, 0, newData, 2048, 2048); // 后半不同
            File.WriteAllBytes(newFile, newData);

            // Act — Clean
            await differ.CleanAsync(oldFile, newFile, patchFile);
            Assert.True(File.Exists(patchFile));

            // Act — Dirty
            await differ.DirtyAsync(oldFile, patchedFile, patchFile);
            Assert.True(File.Exists(patchedFile));

            // Assert
            var resultData = File.ReadAllBytes(patchedFile);
            Assert.Equal(newData, resultData);
        }

        [Fact(DisplayName = "CleanThenDirty_完全不同文件_生成正确结果")]
        public async Task CleanThenDirty_CompletelyDifferentFiles_ProducesExpectedOutput()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            var oldData = new byte[4096];
            new Random(111).NextBytes(oldData);
            File.WriteAllBytes(oldFile, oldData);

            var newData = new byte[4096];
            new Random(222).NextBytes(newData);
            File.WriteAllBytes(newFile, newData);

            // Act — Clean
            await differ.CleanAsync(oldFile, newFile, patchFile);
            Assert.True(File.Exists(patchFile));

            // Act — Dirty
            await differ.DirtyAsync(oldFile, patchedFile, patchFile);
            Assert.True(File.Exists(patchedFile));

            // Assert
            var resultData = File.ReadAllBytes(patchedFile);
            Assert.Equal(newData, resultData);
        }

        [Fact(DisplayName = "CleanThenDirty_空旧文件_正确生成新文件")]
        public async Task CleanThenDirty_EmptyOldFile_ProducesNewFile()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "empty_old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            File.WriteAllBytes(oldFile, Array.Empty<byte>());
            var newData = new byte[512];
            new Random(333).NextBytes(newData);
            File.WriteAllBytes(newFile, newData);

            // Act — Clean
            await differ.CleanAsync(oldFile, newFile, patchFile);
            Assert.True(File.Exists(patchFile));

            // Act — Dirty
            await differ.DirtyAsync(oldFile, patchedFile, patchFile);
            Assert.True(File.Exists(patchedFile));

            // Assert
            var resultData = File.ReadAllBytes(patchedFile);
            Assert.Equal(newData, resultData);
        }

        #endregion CleanThenDirty Round-Trip Tests

        #region DirtyAsync Tests

        [Fact(DisplayName = "DirtyAsync_oldFilePath为null_抛出ArgumentNullException")]
        public async Task DirtyAsync_NullOldPath_ThrowsArgumentNullException()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var newPath = Path.Combine(_testDir, "new.bin");
            var patchPath = Path.Combine(_testDir, "patch.bin");
            File.WriteAllBytes(patchPath, new byte[] { 1, 2, 3 });

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                differ.DirtyAsync(null!, newPath, patchPath));
        }

        [Fact(DisplayName = "DirtyAsync_CancellationToken已取消_抛出OperationCanceledException")]
        public async Task DirtyAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                differ.DirtyAsync("old", "new", "patch", cts.Token));
        }

        [Fact(DisplayName = "Dirty_不存在的补丁文件_抛出异常")]
        public async Task Dirty_NonExistentPatch_Throws()
        {
            // Arrange
            var differ = new StreamingHdiffDiffer();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchFile = Path.Combine(_testDir, "nonexistent.patch");

            File.WriteAllBytes(oldFile, new byte[] { 1, 2, 3, 4, 5 });

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                differ.DirtyAsync(oldFile, newFile, patchFile));
        }

        #endregion DirtyAsync Tests

        #region Custom Configuration Tests

        [Fact(DisplayName = "自定义BlockSize_16KB_往返功能正常")]
        public async Task Constructor_CustomBlockSize_Functional()
        {
            // Arrange
            var mockProvider = new Mock<ICompressionProvider>();
            mockProvider.Setup(p => p.FormatVersion).Returns(0x01);
            mockProvider
                .Setup(p => p.CreateCompressStream(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, CancellationToken>((s, _) => new System.IO.Compression.DeflateStream(s, System.IO.Compression.CompressionLevel.Fastest, true));
            mockProvider
                .Setup(p => p.CreateDecompressStream(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, CancellationToken>((s, _) => new System.IO.Compression.DeflateStream(s, System.IO.Compression.CompressionMode.Decompress, true));

            const int blockSize = 16 * 1024;
            var differ = new StreamingHdiffDiffer(mockProvider.Object, blockSize, 128 * 1024 * 1024);
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            // 创建足够大的文件测试16KB分块哈希
            var oldData = new byte[32768];
            new Random(444).NextBytes(oldData);
            File.WriteAllBytes(oldFile, oldData);

            var newData = new byte[32768];
            Array.Copy(oldData, 0, newData, 0, 16384); // 前半相同
            var suffix = new byte[16384];
            new Random(555).NextBytes(suffix);
            Array.Copy(suffix, 0, newData, 16384, 16384); // 后半不同
            File.WriteAllBytes(newFile, newData);

            // Act — Clean
            await differ.CleanAsync(oldFile, newFile, patchFile);
            Assert.True(File.Exists(patchFile));

            // Act — Dirty
            await differ.DirtyAsync(oldFile, patchedFile, patchFile);
            Assert.True(File.Exists(patchedFile));

            // Assert
            var resultData = File.ReadAllBytes(patchedFile);
            Assert.Equal(newData, resultData);
        }

        #endregion Custom Configuration Tests
    }
}
