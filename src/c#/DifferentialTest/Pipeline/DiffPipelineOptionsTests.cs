using GeneralUpdate.Core.Pipeline;

namespace DifferentialTest.Pipeline
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. MaxDegreeOfParallelism — 默认 = 2
    ///   2. StopOnFirstError — 默认 = false
    ///   3. DeletePatchAfterApply — 默认 = true
    ///   4. 属性 set/get — 修改后正确返回
    ///   5. 边界值 — MaxDegreeOfParallelism=0 (合法，由调用者检查)
    ///
    /// 触发条件：属性get/set操作
    /// 预期结果：默认值符合规范，set后get返回新值
    /// </summary>
    public class DiffPipelineOptionsTests
    {
        [Fact(DisplayName = "默认构造_MaxDegreeOfParallelism为2")]
        public void DefaultConstructor_MaxDegreeOfParallelism_Equals2()
        {
            var options = new DiffPipelineOptions();

            Assert.Equal(2, options.MaxDegreeOfParallelism);
        }

        [Fact(DisplayName = "默认构造_StopOnFirstError为false")]
        public void DefaultConstructor_StopOnFirstError_IsFalse()
        {
            var options = new DiffPipelineOptions();

            Assert.False(options.StopOnFirstError);
        }

        [Fact(DisplayName = "默认构造_DeletePatchAfterApply为true")]
        public void DefaultConstructor_DeletePatchAfterApply_IsTrue()
        {
            var options = new DiffPipelineOptions();

            Assert.True(options.DeletePatchAfterApply);
        }

        [Fact(DisplayName = "MaxDegreeOfParallelism_set_设置后get返回新值")]
        public void MaxDegreeOfParallelism_Set_ReturnsNewValue()
        {
            var options = new DiffPipelineOptions { MaxDegreeOfParallelism = 8 };

            Assert.Equal(8, options.MaxDegreeOfParallelism);
        }

        [Fact(DisplayName = "StopOnFirstError_set_设置后get返回新值")]
        public void StopOnFirstError_Set_ReturnsNewValue()
        {
            var options = new DiffPipelineOptions { StopOnFirstError = true };

            Assert.True(options.StopOnFirstError);
        }

        [Fact(DisplayName = "DeletePatchAfterApply_set_设置后get返回新值")]
        public void DeletePatchAfterApply_Set_ReturnsNewValue()
        {
            var options = new DiffPipelineOptions { DeletePatchAfterApply = false };

            Assert.False(options.DeletePatchAfterApply);
        }

        [Theory(DisplayName = "MaxDegreeOfParallelism_边界值_正确设置")]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(64)]
        [InlineData(int.MaxValue)]
        public void MaxDegreeOfParallelism_BoundaryValues_SetsCorrectly(int value)
        {
            var options = new DiffPipelineOptions { MaxDegreeOfParallelism = value };

            Assert.Equal(value, options.MaxDegreeOfParallelism);
        }
    }
}
