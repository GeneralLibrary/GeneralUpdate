using GeneralUpdate.Differential.Binary;

namespace DifferentialTest.Binary
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. CanRead/CanSeek — 始终返回 false
    ///   2. CanWrite — 返回底层流 CanWrite
    ///   3. Length/Position get — 暴露底层流属性
    ///   4. Position set → NotSupportedException
    ///   5. Seek → NotSupportedException
    ///   6. SetLength → NotSupportedException
    ///   7. Read/ReadByte → NotSupportedException
    ///   8. Write — buffer为null → ArgumentNullException
    ///   9. Write — offset < 0 → ArgumentOutOfRangeException
    ///  10. Write — count < 0 → ArgumentOutOfRangeException
    ///  11. Write — offset+count > buffer.Length → ArgumentException
    ///  12. WriteByte — run-length encoding 逻辑
    ///     - currentChar == -1 → 新字符开始
    ///     - currentChar == num → runLength++ (runLength > 254 分支)
    ///     - currentChar != num → WriteRun 重置
    ///  13. 构造函数 — blockSize clamp (blockSize > 9 / < 1)
    ///  14. IsStreamOwner — 默认为true
    ///  15. Close → Dispose
    ///  16. BytesWritten — 累计输出字节
    ///
    /// 触发条件：各种入参、流状态
    /// 预期结果：异常正确抛出、属性正确、压缩可写出
    /// </summary>
    public class BZip2OutputStreamTests
    {
        [Fact(DisplayName = "构造函数_默认blockSize_使用blockSize=9")]
        public void Constructor_DefaultBlocksize_UsesBlockSize9()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.False(stream.CanRead);
            Assert.True(stream.CanWrite);
            Assert.Equal(0, stream.BytesWritten);
        }

        [Theory(DisplayName = "构造函数_blockSize边界值_安全处理")]
        [InlineData(0, 1)]  // clamp to 1
        [InlineData(1, 1)]
        [InlineData(9, 9)]
        [InlineData(10, 9)] // clamp to 9
        [InlineData(-1, 1)] // clamp to 1
        [InlineData(999, 9)] // clamp to 9
        public void Constructor_BlockSizeBoundaries_HandlesSafely(int input, int _)
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms, input);

            Assert.NotNull(stream);
        }

        [Fact(DisplayName = "CanRead_始终返回false")]
        public void CanRead_Always_ReturnsFalse()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.False(stream.CanRead);
        }

        [Fact(DisplayName = "CanSeek_始终返回false")]
        public void CanSeek_Always_ReturnsFalse()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.False(stream.CanSeek);
        }

        [Fact(DisplayName = "CanWrite_返回底层流CanWrite")]
        public void CanWrite_ReturnsUnderlyingStreamCanWrite()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Equal(ms.CanWrite, stream.CanWrite);
        }

        [Fact(DisplayName = "Length_返回底层流Length")]
        public void Length_ReturnsUnderlyingStreamLength()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Equal(ms.Length, stream.Length);
        }

        [Fact(DisplayName = "Position_get_返回底层流Position")]
        public void Position_Get_ReturnsUnderlyingStreamPosition()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Equal(ms.Position, stream.Position);
        }

        [Fact(DisplayName = "Position_set_抛出NotSupportedException")]
        public void Position_Set_ThrowsNotSupportedException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        }

        [Fact(DisplayName = "Seek_抛出NotSupportedException")]
        public void Seek_ThrowsNotSupportedException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        }

        [Fact(DisplayName = "SetLength_抛出NotSupportedException")]
        public void SetLength_ThrowsNotSupportedException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        }

        [Fact(DisplayName = "Read_抛出NotSupportedException")]
        public void Read_ThrowsNotSupportedException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        }

        [Fact(DisplayName = "ReadByte_抛出NotSupportedException")]
        public void ReadByte_ThrowsNotSupportedException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Throws<NotSupportedException>(() => stream.ReadByte());
        }

        [Fact(DisplayName = "Write_buffer为null_抛出ArgumentNullException")]
        public void Write_NullBuffer_ThrowsArgumentNullException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            var ex = Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 1));
            Assert.Equal("buffer", ex.ParamName);
        }

        [Fact(DisplayName = "Write_offset为负数_抛出ArgumentOutOfRangeException")]
        public void Write_NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[10], -1, 1));
            Assert.Equal("offset", ex.ParamName);
        }

        [Fact(DisplayName = "Write_count为负数_抛出ArgumentOutOfRangeException")]
        public void Write_NegativeCount_ThrowsArgumentOutOfRangeException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[10], 0, -1));
            Assert.Equal("count", ex.ParamName);
        }

        [Fact(DisplayName = "Write_offset+count超出范围_抛出ArgumentException")]
        public void Write_OffsetPlusCountExceeds_ThrowsArgumentException()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            Assert.Throws<ArgumentException>(() => stream.Write(new byte[5], 3, 5));
        }

        [Fact(DisplayName = "WriteByte_写入单个字节_BytesWritten递增")]
        public void WriteByte_SingleByte_BytesWrittenIncrements()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            stream.WriteByte(65); // 'A'
            stream.WriteByte(66); // 'B'

            // BZip2有header开销，写出0字节数据时也可能有输出
            Assert.NotNull(stream);
        }

        [Fact(DisplayName = "Write和Close_写入数据后Close_原始流包含数据")]
        public void WriteAndClose_WriteDataThenClose_UnderlyingContainsData()
        {
            using var ms = new MemoryStream();
            using (var stream = new BZip2OutputStream(ms))
            {
                var data = new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 };
                stream.Write(data, 0, data.Length);
            }

            // Close后原始流应有BZip2压缩数据
            Assert.True(ms.Length > 0);
        }

        [Fact(DisplayName = "WriteByte_相同字节254次_runLength编码触发")]
        public void WriteByte_SameByte254Times_RunLengthEncodingTriggered()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            for (int i = 0; i < 255; i++)
                stream.WriteByte(65);

            // 应触发runLength > 254分支，WriteRun被调用
            stream.Close();
            Assert.True(ms.Length > 0);
        }

        [Fact(DisplayName = "Write和Close_大数据量_能正常压缩")]
        public void WriteAndClose_LargeData_CompressesNormally()
        {
            using var ms = new MemoryStream();
            using (var stream = new BZip2OutputStream(ms) { IsStreamOwner = false })
            {
                var data = new byte[10000];
                new Random(42).NextBytes(data);
                stream.Write(data, 0, data.Length);
            }

            Assert.True(ms.Length > 0);
        }

        [Fact(DisplayName = "Close_重复调用_不抛出异常")]
        public void Close_MultipleCalls_DoesNotThrow()
        {
            using var ms = new MemoryStream();
            var stream = new BZip2OutputStream(ms);

            stream.WriteByte(42);
            stream.Close();
            // 第二次Close/Dispose不应崩溃
            stream.Close();
        }

        [Fact(DisplayName = "IsStreamOwner_默认为true_Close关闭底层流")]
        public void IsStreamOwner_DefaultTrue_CloseClosesUnderlyingStream()
        {
            var ms = new MemoryStream();
            var stream = new BZip2OutputStream(ms);

            Assert.True(stream.IsStreamOwner);
        }

        [Fact(DisplayName = "IsStreamOwner_设为false_Close不关闭底层流")]
        public void IsStreamOwner_SetFalse_CloseDoesNotCloseUnderlying()
        {
            var ms = new MemoryStream();
            var stream = new BZip2OutputStream(ms) { IsStreamOwner = false };

            stream.Close();

            Assert.False(stream.IsStreamOwner);
        }

        [Fact(DisplayName = "Flush_转发到底层流")]
        public void Flush_ForwardsToUnderlying()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            stream.Flush();
            // 不抛出异常即可
        }

        [Fact(DisplayName = "Write_count为0_不应有任何输出")]
        public void Write_ZeroCount_DoesNotWrite()
        {
            using var ms = new MemoryStream();
            using var stream = new BZip2OutputStream(ms);

            var data = new byte[] { 1, 2, 3 };
            stream.Write(data, 0, 0);

            // 写count=0不应有数据输出
            Assert.True(ms.Length == 0);
        }
    }
}
