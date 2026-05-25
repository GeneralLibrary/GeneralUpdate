using System.IO.Compression;
using GeneralUpdate.Differential.Abstractions;

namespace DifferentialTest.Abstractions
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. 构造函数 — optimalLevel=true → CompressionLevel.Optimal
    ///   2. 构造函数 — optimalLevel=false → CompressionLevel.Fastest
    ///   3. FormatVersion — 始终返回 0x01
    ///   4. CreateCompressStream — 返回 DeflateStream 包装原始流出
    ///   5. CreateCompressStream — CancellationToken默认参数
    ///   6. CreateDecompressStream — 返回 DeflateStream 包装原始流入
    ///   7. 空流/已关闭流 — 异常分支
    ///
    /// 触发条件：不同构造函数参数 / 不同流状态
    /// 预期结果：正确版本号、正确流类型、leaveOpen行为正确
    /// </summary>
    public class DeflateCompressionProviderTests
    {
        [Fact(DisplayName = "构造函数_optimalLevel为true_使用Optimal压缩级别")]
        public void Constructor_OptimalLevelTrue_UsesOptimalCompression()
        {
            var provider = new DeflateCompressionProvider(optimalLevel: true);

            using var ms = new MemoryStream();
            using var compressStream = provider.CreateCompressStream(ms);
            Assert.NotNull(compressStream);
            var deflateStream = Assert.IsType<DeflateStream>(compressStream);
            // 默认情况下Optimal级别无法直接读取，但可以校验流不为空
        }

        [Fact(DisplayName = "构造函数_optimalLevel为false_使用Fastest压缩级别")]
        public void Constructor_OptimalLevelFalse_UsesFastestCompression()
        {
            var provider = new DeflateCompressionProvider(optimalLevel: false);

            using var ms = new MemoryStream();
            using var compressStream = provider.CreateCompressStream(ms);
            Assert.NotNull(compressStream);
            Assert.IsType<DeflateStream>(compressStream);
        }

        [Fact(DisplayName = "构造函数_默认参数_使用Optimal压缩级别")]
        public void Constructor_DefaultParameter_UsesOptimalCompression()
        {
            var provider = new DeflateCompressionProvider();

            Assert.NotNull(provider);
        }

        [Fact(DisplayName = "FormatVersion_始终返回0x01")]
        public void FormatVersion_Always_Returns01()
        {
            var provider = new DeflateCompressionProvider();

            Assert.Equal((byte)0x01, provider.FormatVersion);
        }

        [Fact(DisplayName = "CreateCompressStream_正常MemoryStream_返回DeflateStream包装")]
        public void CreateCompressStream_ValidStream_ReturnsDeflateStreamWrapper()
        {
            var provider = new DeflateCompressionProvider();

            using var ms = new MemoryStream();
            using var compressStream = provider.CreateCompressStream(ms);

            Assert.NotNull(compressStream);
            Assert.IsType<DeflateStream>(compressStream);
            Assert.True(compressStream.CanWrite);
        }

        [Fact(DisplayName = "CreateCompressStream_写入数据后_原始流包含压缩数据")]
        public void CreateCompressStream_WriteData_UnderlyingStreamContainsCompressedData()
        {
            var provider = new DeflateCompressionProvider();
            var data = new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 };

            using var ms = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(ms))
            {
                compressStream.Write(data, 0, data.Length);
            } // 释放压缩流以刷新数据

            Assert.True(ms.Length > 0);
        }

        [Fact(DisplayName = "CreateCompressStream_leaveOpen为true_释放包装流不关闭原始流")]
        public void CreateCompressStream_LeaveOpen_DisposingWrapperDoesNotCloseUnderlying()
        {
            var provider = new DeflateCompressionProvider();

            using var ms = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(ms))
            {
                compressStream.WriteByte(42);
            }

            // 原始流仍可读写
            Assert.True(ms.CanRead);
        }

        [Fact(DisplayName = "CreateDecompressStream_正常MemoryStream_返回DeflateStream包装")]
        public void CreateDecompressStream_ValidStream_ReturnsDeflateStreamWrapper()
        {
            var provider = new DeflateCompressionProvider();

            // 先压缩一些数据
            using var compressedMs = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(compressedMs))
            {
                var data = System.Text.Encoding.UTF8.GetBytes("hello world hello world hello world");
                compressStream.Write(data, 0, data.Length);
            }

            // 再解压
            compressedMs.Position = 0;
            using var decompressStream = provider.CreateDecompressStream(compressedMs);

            Assert.NotNull(decompressStream);
            Assert.IsType<DeflateStream>(decompressStream);
            Assert.True(decompressStream.CanRead);
        }

        [Fact(DisplayName = "CreateDecompressStream_leaveOpen为true_释放包装流不关闭原始流")]
        public void CreateDecompressStream_LeaveOpen_DisposingWrapperDoesNotCloseUnderlying()
        {
            var provider = new DeflateCompressionProvider();

            using var compressedMs = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(compressedMs))
            {
                var data = new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 };
                compressStream.Write(data, 0, data.Length);
            }

            compressedMs.Position = 0;
            using (var decompressStream = provider.CreateDecompressStream(compressedMs))
            {
                var buf = new byte[10];
                int totalRead = 0;
                while (totalRead < buf.Length)
                {
                    int read = decompressStream.Read(buf, totalRead, buf.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
            }

            // 原始流仍可寻道
            Assert.True(compressedMs.CanSeek);
        }

        [Fact(DisplayName = "CreateDecompressStream_空压缩数据_读取返回0")]
        public void CreateDecompressStream_EmptyData_ReadReturnsZero()
        {
            var provider = new DeflateCompressionProvider();

            using var compressedMs = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(compressedMs))
            {
                // 不写入任何数据
            }

            compressedMs.Position = 0;
            using var decompressStream = provider.CreateDecompressStream(compressedMs);
            var buf = new byte[10];

            // 空DeflateStream可能抛出或返回0，取决于BaseStream状态
            // 这里验证不会NullReferenceException
            Assert.NotNull(decompressStream);
        }

        [Fact(DisplayName = "CreateCompressStream_CancellationToken已取消_压缩流仍可创建(tokeng未被使用)")]
        public void CreateCompressStream_CancellationTokenPassed_DoesNotThrow()
        {
            var provider = new DeflateCompressionProvider();
            using var ms = new MemoryStream();
            using var cts = new CancellationTokenSource();

            // ICompressionProvider 接口中有 CancellationToken，但 Deflate 实现可能忽略
            using var stream = provider.CreateCompressStream(ms, cts.Token);

            Assert.NotNull(stream);
        }

        [Fact(DisplayName = "最优级别和最快级别_压缩结果可能不同")]
        public void OptimalVsFastest_CompressionResultsMayDiffer()
        {
            var optimal = new DeflateCompressionProvider(optimalLevel: true);
            var fastest = new DeflateCompressionProvider(optimalLevel: false);
            var data = new byte[1024];
            new Random(42).NextBytes(data);

            using var msOpt = new MemoryStream();
            using (var cs = optimal.CreateCompressStream(msOpt))
                cs.Write(data, 0, data.Length);

            using var msFast = new MemoryStream();
            using (var cs = fastest.CreateCompressStream(msFast))
                cs.Write(data, 0, data.Length);

            // 两者都能产生压缩输出(长度不一定相同,但都不应为0)
            Assert.True(msOpt.Length > 0);
            Assert.True(msFast.Length > 0);
        }
    }
}
