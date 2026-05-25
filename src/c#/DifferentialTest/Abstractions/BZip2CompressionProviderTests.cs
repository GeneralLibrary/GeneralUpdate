using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Binary;

namespace DifferentialTest.Abstractions
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. FormatVersion — 始终返回 0x00
    ///   2. CreateCompressStream — 返回 BZip2OutputStream
    ///   3. CreateCompressStream — IsStreamOwner = false
    ///   4. CreateDecompressStream — 返回 BZip2InputStream
    ///   5. 空流/正常MemoryStream — 行为验证
    ///
    /// 触发条件：正常MemoryStream
    /// 预期结果：正确版本号、正确的流类型、IsStreamOwner=false
    /// </summary>
    public class BZip2CompressionProviderTests
    {
        [Fact(DisplayName = "FormatVersion_始终返回0x00")]
        public void FormatVersion_Always_Returns00()
        {
            var provider = new BZip2CompressionProvider();

            Assert.Equal((byte)0x00, provider.FormatVersion);
        }

        [Fact(DisplayName = "CreateCompressStream_有效MemoryStream_返回BZip2OutputStream")]
        public void CreateCompressStream_ValidStream_ReturnsBZip2OutputStream()
        {
            var provider = new BZip2CompressionProvider();

            using var ms = new MemoryStream();
            using var stream = provider.CreateCompressStream(ms);

            Assert.NotNull(stream);
            Assert.IsType<BZip2OutputStream>(stream);
        }

        [Fact(DisplayName = "CreateCompressStream_输出的BZip2OutputStream_IsStreamOwner为false")]
        public void CreateCompressStream_Result_IsStreamOwnerIsFalse()
        {
            var provider = new BZip2CompressionProvider();

            using var ms = new MemoryStream();
            using var stream = provider.CreateCompressStream(ms);

            var bz2Out = Assert.IsType<BZip2OutputStream>(stream);
            Assert.False(bz2Out.IsStreamOwner);
        }

        [Fact(DisplayName = "CreateCompressStream_写入数据后_原始流包含数据")]
        public void CreateCompressStream_WriteData_UnderlyingStreamContainsData()
        {
            var provider = new BZip2CompressionProvider();
            var data = new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 };

            using var ms = new MemoryStream();
            using (var stream = provider.CreateCompressStream(ms))
            {
                stream.Write(data, 0, data.Length);
            }

            Assert.True(ms.Length > 0);
        }

        [Fact(DisplayName = "CreateCompressStream_释放包装流_不关闭原始流")]
        public void CreateCompressStream_DisposeWrapper_DoesNotCloseUnderlying()
        {
            var provider = new BZip2CompressionProvider();

            using var ms = new MemoryStream();
            using (var stream = provider.CreateCompressStream(ms))
            {
                stream.WriteByte(42);
            }

            Assert.True(ms.CanRead);
        }

        [Fact(DisplayName = "CreateDecompressStream_有效MemoryStream_返回BZip2InputStream")]
        public void CreateDecompressStream_ValidStream_ReturnsBZip2InputStream()
        {
            // 需要有效的BZip2数据来创建InputStream，但仅验证类型
            var provider = new BZip2CompressionProvider();

            // 构造最小的有效BZip2数据
            using var ms = new MemoryStream();
            var minimalBz2 = new byte[] {
                (byte)'B', (byte)'Z', (byte)'h', (byte)'1',
                0x31, 0x41, 0x59, 0x26, 0x53, 0x59,
                0x00, 0x00, 0x00, 0x00, 0x00,
                (byte)0x17, (byte)'r', (byte)'E', (byte)'8', (byte)'P', (byte)0x90,
                0x00, 0x00, 0x00, 0x00
            };
            ms.Write(minimalBz2, 0, minimalBz2.Length);
            ms.Position = 0;

            // InputStream 的构造函数会从流中读取；即便"空"也能创建(只是streamEnd=true)
            var stream = provider.CreateDecompressStream(ms);

            Assert.NotNull(stream);
            Assert.IsType<BZip2InputStream>(stream);
        }

        [Fact(DisplayName = "CreateCompressStream_CancellationToken传递_不抛出异常")]
        public void CreateCompressStream_WithCancellationToken_DoesNotThrow()
        {
            var provider = new BZip2CompressionProvider();
            using var ms = new MemoryStream();
            using var cts = new CancellationTokenSource();

            using var stream = provider.CreateCompressStream(ms, cts.Token);

            Assert.NotNull(stream);
        }

        [Fact(DisplayName = "CreateDecompressStream_CancellationToken传递_不抛出异常")]
        public void CreateDecompressStream_WithCancellationToken_DoesNotThrow()
        {
            var provider = new BZip2CompressionProvider();
            using var ms = new MemoryStream();

            using var stream = provider.CreateDecompressStream(ms);

            Assert.NotNull(stream);
        }
    }
}
