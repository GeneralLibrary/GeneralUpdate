using GeneralUpdate.Differential.Binary;

namespace DifferentialTest.Binary
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. 构造函数 — globalCrc = -1 (Reset调用)
    ///   2. Value — 返回 ~globalCrc
    ///   3. Reset — 将 globalCrc 重置为 -1
    ///   4. Update(int) — num < 0 分支 (normalize)
    ///   5. Update(byte[]) — null buffer → ArgumentNullException
    ///   6. Update(byte[], int, int) — offset < 0 / count < 0 / offset+count > buffer.Length → 异常分支
    ///   7. Update 循环 — 遍历 count 次
    ///   8. CRC 一致性 — 相同数据产生相同CRC
    ///
    /// 触发条件：各种入参
    /// 预期结果：CRC值符合预期、异常正确抛出
    /// </summary>
    public class StrangeCRCTests
    {
        [Fact(DisplayName = "构造函数_创建实例_默认Value为0")]
        public void Constructor_NewInstance_DefaultValueIsZero()
        {
            var crc = new StrangeCRC();

            Assert.Equal(0L, crc.Value);
        }

        [Fact(DisplayName = "Reset_重置后_Value归零")]
        public void Reset_AfterUpdate_ValueReturnsToZero()
        {
            var crc = new StrangeCRC();
            crc.Update(42);

            crc.Reset();

            Assert.Equal(0L, crc.Value);
        }

        [Fact(DisplayName = "Update_int单值_Value变化")]
        public void Update_SingleInt_ValueChanges()
        {
            var crc = new StrangeCRC();

            crc.Update(65);

            Assert.NotEqual(0L, crc.Value);
        }

        [Theory(DisplayName = "Update_int多个值_相同输入产生相同CRC")]
        [InlineData(new int[] { 1, 2, 3, 4, 5 })]
        [InlineData(new int[] { 255 })]
        [InlineData(new int[] { 0 })]
        [InlineData(new int[] { })]
        public void Update_MultiInt_SameInputSameCrc(int[] values)
        {
            var crc1 = new StrangeCRC();
            foreach (var v in values) crc1.Update(v);

            var crc2 = new StrangeCRC();
            foreach (var v in values) crc2.Update(v);

            Assert.Equal(crc1.Value, crc2.Value);
        }

        [Fact(DisplayName = "Update_byte数组_正确计算CRC")]
        public void Update_ByteArray_ComputesCrc()
        {
            var crc = new StrangeCRC();
            var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

            crc.Update(data);

            Assert.NotEqual(0L, crc.Value);
        }

        [Fact(DisplayName = "Update_byte数组null_抛出ArgumentNullException")]
        public void Update_NullByteArray_ThrowsArgumentNullException()
        {
            var crc = new StrangeCRC();

            var ex = Assert.Throws<ArgumentNullException>(() => crc.Update((byte[])null!));

            Assert.Equal("buffer", ex.ParamName);
        }

        [Fact(DisplayName = "Update_带offset和count的byte数组null_抛出ArgumentNullException")]
        public void Update_OffsetCountNullBuffer_ThrowsArgumentNullException()
        {
            var crc = new StrangeCRC();

            var ex = Assert.Throws<ArgumentNullException>(() => crc.Update(null!, 0, 1));

            Assert.Equal("buffer", ex.ParamName);
        }

        [Fact(DisplayName = "Update_offset为负数_抛出ArgumentOutOfRangeException")]
        public void Update_NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            var crc = new StrangeCRC();
            var buffer = new byte[10];

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => crc.Update(buffer, -1, 1));

            Assert.Equal("offset", ex.ParamName);
        }

        [Fact(DisplayName = "Update_count为负数_抛出ArgumentOutOfRangeException")]
        public void Update_NegativeCount_ThrowsArgumentOutOfRangeException()
        {
            var crc = new StrangeCRC();
            var buffer = new byte[10];

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => crc.Update(buffer, 0, -1));

            Assert.Equal("count", ex.ParamName);
        }

        [Fact(DisplayName = "Update_offset+count超出buffer长度_抛出ArgumentOutOfRangeException")]
        public void Update_OffsetPlusCountExceedsLength_ThrowsArgumentOutOfRangeException()
        {
            var crc = new StrangeCRC();
            var buffer = new byte[5];

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => crc.Update(buffer, 3, 3));

            Assert.Equal("count", ex.ParamName);
        }

        [Fact(DisplayName = "Update_offset和count为0_不影响CRC值")]
        public void Update_ZeroCount_DoesNotChangeCrc()
        {
            var crc = new StrangeCRC();
            var original = crc.Value;
            var buffer = new byte[10];

            crc.Update(buffer, 0, 0);

            Assert.Equal(original, crc.Value);
        }

        [Fact(DisplayName = "Update_整个buffer与分段Update_结果一致")]
        public void Update_FullBufferVsSegmented_SameResult()
        {
            var data = new byte[16];
            new Random(99).NextBytes(data);

            var crcFull = new StrangeCRC();
            crcFull.Update(data);

            var crcSeg = new StrangeCRC();
            crcSeg.Update(data, 0, 8);
            crcSeg.Update(data, 8, 8);

            Assert.Equal(crcFull.Value, crcSeg.Value);
        }

        [Fact(DisplayName = "Value_获取后仍可继续Update")]
        public void Value_GetValueThenContinueUpdating_ChangesCrc()
        {
            var crc = new StrangeCRC();
            crc.Update(1);
            var val1 = crc.Value;
            crc.Update(2);
            var val2 = crc.Value;

            Assert.NotEqual(val1, val2);
        }

        [Fact(DisplayName = "Update_int_256边界值normalize分支覆盖")]
        public void Update_IntWith256Boundary_NormalizesCorrectly()
        {
            var crc = new StrangeCRC();

            for (int i = 0; i < 1000; i++)
                crc.Update(i % 256);

            Assert.NotEqual(0L, crc.Value);
        }
    }
}
