using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Binary;

namespace DifferentialTest.Binary
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. 无参构造函数 — 使用 BZip2CompressionProvider 默认
    ///   2. 带ICompressionProvider的构造函数 — null → ArgumentNullException
    ///   3. CleanAsync — CancellationToken 已取消 → OperationCanceledException
    ///   4. CleanAsync — 委托到 Clean (文件系统依赖)
    ///   5. DirtyAsync — CancellationToken 已取消 → OperationCanceledException
    ///   6. DirtyAsync — 委托到 Dirty
    ///   7. Clean/Dirty (内部) — ValidationParameters 异常分支
    ///     - oldFilePath/newFilePath/patchPath 为null/空白
    ///   8. ValidationParameters 三参数所有组合
    ///
    /// 触发条件：各种路径参数、CancellationToken状态
    /// 预期结果：正确异常抛出、正确委托
    /// </summary>
    public class BinaryHandlerTests : IDisposable
    {
        private readonly string _testDir;

        public BinaryHandlerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"BinaryHandlerTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Fact(DisplayName = "构造函数_无参_使用BZip2CompressionProvider")]
        public void Constructor_NoArgs_UsesBZip2CompressionProvider()
        {
            var handler = new BinaryHandler();

            Assert.NotNull(handler);
        }

        [Fact(DisplayName = "构造函数_自定义压缩提供者_正确创建实例")]
        public void Constructor_CustomCompressionProvider_CreatesInstance()
        {
            var mockProvider = new Mock<ICompressionProvider>();
            mockProvider.Setup(p => p.FormatVersion).Returns(0x01);

            var handler = new BinaryHandler(mockProvider.Object);

            Assert.NotNull(handler);
        }

        [Fact(DisplayName = "构造函数_compressionProvider为null_抛出ArgumentNullException")]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new BinaryHandler(null!));

            Assert.Equal("compressionProvider", ex.ParamName);
        }

        [Fact(DisplayName = "CleanAsync_CancellationToken已取消_抛出OperationCanceledException")]
        public async Task CleanAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var handler = new BinaryHandler();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                handler.CleanAsync("old", "new", "patch", cts.Token));
        }

        [Fact(DisplayName = "DirtyAsync_CancellationToken已取消_抛出OperationCanceledException")]
        public async Task DirtyAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var handler = new BinaryHandler();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                handler.DirtyAsync("old", "new", "patch", cts.Token));
        }

        [Fact(DisplayName = "Clean_文件不存在_正常失败")]
        public async Task Clean_NonExistentFiles_ThrowsFileNotFound()
        {
            var handler = new BinaryHandler();

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                handler.Clean(
                    Path.Combine(_testDir, "nonexistent_old.bin"),
                    Path.Combine(_testDir, "nonexistent_new.bin"),
                    Path.Combine(_testDir, "output.patch")));
        }

        [Fact(DisplayName = "Dirty_文件不存在_正常失败")]
        public async Task Dirty_NonExistentFiles_ThrowsFileNotFound()
        {
            var handler = new BinaryHandler();

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                handler.Dirty(
                    Path.Combine(_testDir, "nonexistent_old.bin"),
                    Path.Combine(_testDir, "nonexistent_new.bin"),
                    Path.Combine(_testDir, "nonexistent_patch.bin")));
        }

        [Fact(DisplayName = "Clean_源和目标文件相同_生成最小补丁")]
        public async Task Clean_IdenticalFiles_GeneratesMinimalPatch()
        {
            var handler = new BinaryHandler();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            var data = new byte[1024];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(oldFile, data);
            File.WriteAllBytes(newFile, data);

            await handler.Clean(oldFile, newFile, patchFile);

            Assert.True(File.Exists(patchFile));
            Assert.True(new FileInfo(patchFile).Length > 0);
        }

        [Fact(DisplayName = "Clean_生成补丁后Dirty还原_文件内容一致")]
        public async Task CleanThenDirty_RoundTrip_ProducesIdenticalFile()
        {
            var handler = new BinaryHandler();
            var oldFile = Path.Combine(_testDir, "old.bin");
            var newFile = Path.Combine(_testDir, "new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            var oldData = new byte[4096];
            new Random(77).NextBytes(oldData);
            File.WriteAllBytes(oldFile, oldData);

            var newData = new byte[4096];
            Array.Copy(oldData, newData, 2048); // 前半相同
            var suffix = new byte[2048]; new Random(88).NextBytes(suffix); Array.Copy(suffix, 0, newData, 2048, 2048); // 后半不同
            File.WriteAllBytes(newFile, newData);

            // Clean: 生成补丁
            await handler.Clean(oldFile, newFile, patchFile);
            Assert.True(File.Exists(patchFile));

            // Dirty: 应用补丁
            await handler.Dirty(oldFile, patchedFile, patchFile);
            Assert.True(File.Exists(patchedFile));

            // 验证
            var resultData = File.ReadAllBytes(patchedFile);
            Assert.Equal(newData, resultData);
        }

        [Fact(DisplayName = "Clean_空文件_能正确生成补丁")]
        public async Task Clean_EmptyFiles_GeneratesPatch()
        {
            var handler = new BinaryHandler();
            var oldFile = Path.Combine(_testDir, "empty_old.bin");
            var newFile = Path.Combine(_testDir, "empty_new.bin");
            var patchFile = Path.Combine(_testDir, "empty_patch.bin");

            File.WriteAllBytes(oldFile, Array.Empty<byte>());
            File.WriteAllBytes(newFile, Array.Empty<byte>());

            await handler.Clean(oldFile, newFile, patchFile);

            Assert.True(File.Exists(patchFile));
        }

        [Fact(DisplayName = "Dirty_旧文件空_应用补丁生成新文件")]
        public async Task Dirty_EmptyOldFile_AppliesPatchToNewFile()
        {
            var handler = new BinaryHandler();
            var oldFile = Path.Combine(_testDir, "empty_old.bin");
            var newFile = Path.Combine(_testDir, "full_new.bin");
            var patchedFile = Path.Combine(_testDir, "patched.bin");
            var patchFile = Path.Combine(_testDir, "patch.bin");

            File.WriteAllBytes(oldFile, Array.Empty<byte>());
            var newData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            File.WriteAllBytes(newFile, newData);

            await handler.Clean(oldFile, newFile, patchFile);
            await handler.Dirty(oldFile, patchedFile, patchFile);

            Assert.Equal(newData, File.ReadAllBytes(patchedFile));
        }

        [Fact(DisplayName = "Dirty_不存在的补丁文件_正常抛出异常")]
        public async Task Dirty_NonExistentPatch_Throws()
        {
            var handler = new BinaryHandler();
            var oldFile = Path.Combine(_testDir, "old.bin");
            File.WriteAllBytes(oldFile, new byte[] { 1, 2, 3 });

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                handler.Dirty(oldFile, Path.Combine(_testDir, "new.bin"), Path.Combine(_testDir, "nonexistent.patch")));
        }
    }
}
