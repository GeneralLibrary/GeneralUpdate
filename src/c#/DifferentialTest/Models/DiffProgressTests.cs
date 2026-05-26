using GeneralUpdate.Core.Models;

namespace DifferentialTest.Models
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. 构造函数正常分支 (completed/total/currentFile/error 不同组合)
    ///   2. Percentage — Total == 0 返回 100；Total > 0 返回比率
    ///   3. IsComplete — Completed >= Total (等于/大于)
    ///   4. Complete 静态工厂方法
    ///   5. ToString — IsComplete 为 true / false / Error 有值/无值
    ///
    /// 触发条件：各属性组合
    /// 预期结果：属性值、百分比、完成标志、字符串格式符合预期
    /// </summary>
    public class DiffProgressTests
    {
        [Fact(DisplayName = "构造函数_正常赋值_属性正确返回")]
        public void Constructor_ValidValues_PropertiesReturnCorrectly()
        {
            var progress = new DiffProgress(5, 10, "file.dll");

            Assert.Equal(5, progress.Completed);
            Assert.Equal(10, progress.Total);
            Assert.Equal("file.dll", progress.CurrentFile);
            Assert.Null(progress.Error);
        }

        [Fact(DisplayName = "构造函数_带Error参数_Error属性正确返回")]
        public void Constructor_WithError_ErrorPropertyReturnsCorrectly()
        {
            var progress = new DiffProgress(3, 10, "bad.dll", "IO error");

            Assert.Equal("IO error", progress.Error);
        }

        [Theory(DisplayName = "Percentage_不同Total值_返回正确百分比")]
        [InlineData(5, 10, 50.0)]
        [InlineData(0, 10, 0.0)]
        [InlineData(10, 10, 100.0)]
        [InlineData(1, 3, 100.0 / 3.0)]
        [InlineData(0, 0, 100.0)]
        [InlineData(7, 1, 700.0)]
        public void Percentage_VariousTotals_ReturnsCorrectPercentage(int completed, int total, double expected)
        {
            var progress = new DiffProgress(completed, total, null);

            Assert.Equal(expected, progress.Percentage);
        }

        [Theory(DisplayName = "IsComplete_Completed与Total的关系_正确返回完成标志")]
        [InlineData(5, 10, false)]
        [InlineData(10, 10, true)]
        [InlineData(0, 0, true)]
        [InlineData(11, 10, true)]
        public void IsComplete_CompletedVsTotal_ReturnsCorrectFlag(int completed, int total, bool expected)
        {
            var progress = new DiffProgress(completed, total, null);

            Assert.Equal(expected, progress.IsComplete);
        }

        [Fact(DisplayName = "Complete_静态工厂_返回已完成标记")]
        public void Complete_StaticFactory_ReturnsCompleteMarker()
        {
            var progress = DiffProgress.Complete(42);

            Assert.Equal(42, progress.Completed);
            Assert.Equal(42, progress.Total);
            Assert.Null(progress.CurrentFile);
            Assert.Null(progress.Error);
            Assert.True(progress.IsComplete);
        }

        [Fact(DisplayName = "ToString_已完成状态_返回格式化的完成字符串")]
        public void ToString_CompleteState_ReturnsFormattedCompleteString()
        {
            var progress = DiffProgress.Complete(10);

            var result = progress.ToString();

            Assert.Contains("Complete", result);
            Assert.Contains("10/10", result);
        }

        [Fact(DisplayName = "ToString_进行中状态_返回百分比和处理文件")]
        public void ToString_InProgressState_ReturnsPercentageAndFile()
        {
            var progress = new DiffProgress(5, 10, "app.exe");

            var result = progress.ToString();

            Assert.Contains("5/10", result);
            Assert.Contains("app.exe", result);
            Assert.DoesNotContain("failed", result);
        }

        [Fact(DisplayName = "ToString_带Error的进行中状态_包含失败信息")]
        public void ToString_InProgressWithError_ContainsFailureInfo()
        {
            var progress = new DiffProgress(3, 10, "bad.dll", "access denied");

            var result = progress.ToString();

            Assert.Contains("failed: access denied", result);
        }

        [Fact(DisplayName = "ToString_CurrentFile为null_显示省略号")]
        public void ToString_NullCurrentFile_ShowsEllipsis()
        {
            var progress = new DiffProgress(1, 10, null);

            var result = progress.ToString();

            Assert.Contains("...", result);
        }

        [Fact(DisplayName = "DiffProgress_值类型语义_相等比较")]
        public void ValueTypeSemantics_EqualityComparison()
        {
            var a = new DiffProgress(1, 10, "f");
            var b = new DiffProgress(1, 10, "f");

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact(DisplayName = "DiffProgress_CurrentFile为null_属性返回null")]
        public void NullCurrentFile_ReturnsNull()
        {
            var progress = new DiffProgress(0, 10, null);

            Assert.Null(progress.CurrentFile);
        }
    }
}
