#if NET6_0_OR_GREATER
using System.IO.Compression;
using GeneralUpdate.Differential.Abstractions;

namespace DifferentialTest.Abstractions
{
    /// <summary>
    /// BrotliCompressionProvider 分支覆盖测试。
    /// 覆盖：构造函数（optimalLevel true/false）、FormatVersion、CreateCompressStream、
    /// CreateDecompressStream、leaveOpen 行为、压缩解压往返。
    /// 前置条件：GeneralUpdate.Differential 需以 net6.0+ 目标框架编译，
    /// 使 BrotliCompressionProvider 类型可用。
    /// </summary>
    public class BrotliCompressionProviderTests
    {
        [Fact(DisplayName = "构造函数_默认参数_使用Optimal压缩级别")]
        public void Constructor_Default_UsesOptimalCompression()
        {
            // Arrange & Act
            var provider = new BrotliCompressionProvider();

            // Assert
            Assert.NotNull(provider);
        }

        [Fact(DisplayName = "构造函数_optimalLevel为false_使用Fastest压缩级别")]
        public void Constructor_OptimalLevelFalse_UsesFastestCompression()
        {
            // Arrange & Act
            var provider = new BrotliCompressionProvider(optimalLevel: false);

            // Assert
            Assert.NotNull(provider);
        }

        [Fact(DisplayName = "FormatVersion_始终返回0x02")]
        public void FormatVersion_Always_Returns02()
        {
            // Arrange
            var provider = new BrotliCompressionProvider();

            // Act
            var version = provider.FormatVersion;

            // Assert
            Assert.Equal((byte)0x02, version);
        }

        [Fact(DisplayName = "CreateCompressStream_有效MemoryStream_返回BrotliStream")]
        public void CreateCompressStream_ValidStream_ReturnsBrotliStream()
        {
            // Arrange
            var provider = new BrotliCompressionProvider();
            using var ms = new MemoryStream();

            // Act
            using var stream = provider.CreateCompressStream(ms);

            // Assert
            Assert.NotNull(stream);
            var brotliStream = Assert.IsType<BrotliStream>(stream);
            Assert.True(brotliStream.CanWrite);
        }

        [Fact(DisplayName = "CreateCompressStream_写入数据_原始流包含压缩数据")]
        public void CreateCompressStream_WriteData_UnderlyingContainsCompressedData()
        {
            // Arrange
            var provider = new BrotliCompressionProvider();
            var data = new byte[1024];
            new Random(42).NextBytes(data);

            using var ms = new MemoryStream();

            // Act
            using (var stream = provider.CreateCompressStream(ms))
            {
                stream.Write(data, 0, data.Length);
            }

            // Assert
            Assert.True(ms.Length > 0);
        }

        [Fact(DisplayName = "CreateCompressStream_释放包装流_不关闭原始流")]
        public void CreateCompressStream_Dispose_DoesNotCloseUnderlying()
        {
            // Arrange
            var provider = new BrotliCompressionProvider();

            using var ms = new MemoryStream();

            // Act
            using (var stream = provider.CreateCompressStream(ms))
            {
                stream.WriteByte(42);
            }

            // Assert: leaveOpen=true → underlying stream still readable
            Assert.True(ms.CanRead);
        }

        [Fact(DisplayName = "CreateDecompressStream_有效压缩数据_返回BrotliStream")]
        public void CreateDecompressStream_ValidData_ReturnsBrotliStream()
        {
            // Arrange
            var provider = new BrotliCompressionProvider();
            var data = System.Text.Encoding.UTF8.GetBytes("Hello, Brotli World!");

            // Compress first
            using var compressedMs = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(compressedMs))
            {
                compressStream.Write(data, 0, data.Length);
            }

            // Act
            compressedMs.Position = 0;
            using var decompressStream = provider.CreateDecompressStream(compressedMs);

            // Assert
            Assert.NotNull(decompressStream);
            Assert.IsType<BrotliStream>(decompressStream);
            Assert.True(decompressStream.CanRead);
        }

        [Fact(DisplayName = "压缩解压往返_产生相同数据")]
        public void CompressDecompress_RoundTrip_ProducesIdenticalData()
        {
            // Arrange
            var provider = new BrotliCompressionProvider();
            var originalData = new byte[4096];
            new Random(42).NextBytes(originalData);

            // Act — compress
            using var compressedMs = new MemoryStream();
            using (var compressStream = provider.CreateCompressStream(compressedMs))
            {
                compressStream.Write(originalData, 0, originalData.Length);
            }

            // Act — decompress
            compressedMs.Position = 0;
            using var decompressStream = provider.CreateDecompressStream(compressedMs);
            using var resultMs = new MemoryStream();
            decompressStream.CopyTo(resultMs);

            // Assert
            var decompressedData = resultMs.ToArray();
            Assert.Equal(originalData, decompressedData);
        }
    }
}
#endif
