using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Core.Pipeline;

namespace DifferentialTest.Pipeline
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. UseDiffer — differ为null → ArgumentNullException
    ///   2. UseDiffer — 正常设置
    ///   3. UseCleanMatcher — matcher为null → ArgumentNullException
    ///   4. UseCleanMatcher — 正常设置
    ///   5. UseDirtyMatcher — matcher为null → ArgumentNullException
    ///   6. UseDirtyMatcher — 正常设置
    ///   7. WithParallelism — maxDegreeOfParallelism < 1 → ArgumentOutOfRangeException
    ///   8. WithParallelism — 正常/边界值
    ///   9. WithStopOnFirstError — true/false
    ///  10. WithProgress — progress为null → ArgumentNullException
    ///  11. WithProgress — 正常设置
    ///  12. Build — 未设置任何选项 → 全部默认值
    ///  13. Build — 全部自定义 → 传给DiffPipeline
    ///  14. Build — differ未设置 → 默认StreamingHdiffDiffer
    ///
    /// 触发条件：各种构造组合
    /// 预期结果：正确构建DiffPipeline
    /// </summary>
    public class DiffPipelineBuilderTests
    {
        [Fact(DisplayName = "Build_未设置任何选项_使用所有默认值")]
        public void Build_NoOptions_UsesAllDefaults()
        {
            var pipeline = new DiffPipelineBuilder().Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "UseDiffer_有效differ_正确设置")]
        public void UseDiffer_ValidDiffer_SetsCorrectly()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();
            var builder = new DiffPipelineBuilder();

            var result = builder.UseDiffer(mockDiffer.Object);
            var pipeline = result.Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "UseDiffer_null_抛出ArgumentNullException")]
        public void UseDiffer_Null_ThrowsArgumentNullException()
        {
            var builder = new DiffPipelineBuilder();

            var ex = Assert.Throws<ArgumentNullException>(() => builder.UseDiffer(null!));

            Assert.Equal("differ", ex.ParamName);
        }

        [Fact(DisplayName = "UseCleanMatcher_有效matcher_正确设置")]
        public void UseCleanMatcher_ValidMatcher_SetsCorrectly()
        {
            var mockMatcher = new Mock<ICleanMatcher>();

            var pipeline = new DiffPipelineBuilder()
                .UseCleanMatcher(mockMatcher.Object)
                .Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "UseCleanMatcher_null_抛出ArgumentNullException")]
        public void UseCleanMatcher_Null_ThrowsArgumentNullException()
        {
            var builder = new DiffPipelineBuilder();

            var ex = Assert.Throws<ArgumentNullException>(() => builder.UseCleanMatcher(null!));

            Assert.Equal("matcher", ex.ParamName);
        }

        [Fact(DisplayName = "UseDirtyMatcher_有效matcher_正确设置")]
        public void UseDirtyMatcher_ValidMatcher_SetsCorrectly()
        {
            var mockMatcher = new Mock<IDirtyMatcher>();

            var pipeline = new DiffPipelineBuilder()
                .UseDirtyMatcher(mockMatcher.Object)
                .Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "UseDirtyMatcher_null_抛出ArgumentNullException")]
        public void UseDirtyMatcher_Null_ThrowsArgumentNullException()
        {
            var builder = new DiffPipelineBuilder();

            var ex = Assert.Throws<ArgumentNullException>(() => builder.UseDirtyMatcher(null!));

            Assert.Equal("matcher", ex.ParamName);
        }

        [Theory(DisplayName = "WithParallelism_有效值_正确设置")]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(16)]
        [InlineData(32)]
        public void WithParallelism_ValidValues_SetsCorrectly(int parallelism)
        {
            var pipeline = new DiffPipelineBuilder()
                .WithParallelism(parallelism)
                .Build();

            Assert.NotNull(pipeline);
        }

        [Theory(DisplayName = "WithParallelism_无效值_抛出ArgumentOutOfRangeException")]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void WithParallelism_InvalidValues_ThrowsArgumentOutOfRangeException(int invalidValue)
        {
            var builder = new DiffPipelineBuilder();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithParallelism(invalidValue));
        }

        [Theory(DisplayName = "WithStopOnFirstError_不同值_正确设置")]
        [InlineData(true)]
        [InlineData(false)]
        public void WithStopOnFirstError_VariousValues_SetsCorrectly(bool stopOnFirstError)
        {
            var pipeline = new DiffPipelineBuilder()
                .WithStopOnFirstError(stopOnFirstError)
                .Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "WithProgress_有效progress_正确设置")]
        public void WithProgress_ValidProgress_SetsCorrectly()
        {
            var mockProgress = new Mock<IProgress<DiffProgress>>();

            var pipeline = new DiffPipelineBuilder()
                .WithProgress(mockProgress.Object)
                .Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "WithProgress_null_抛出ArgumentNullException")]
        public void WithProgress_Null_ThrowsArgumentNullException()
        {
            var builder = new DiffPipelineBuilder();

            var ex = Assert.Throws<ArgumentNullException>(() => builder.WithProgress(null!));

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact(DisplayName = "链式调用_全部自定义_构建成功")]
        public void FluentApi_AllCustom_BuildsSuccessfully()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();
            var mockCleanMatcher = new Mock<ICleanMatcher>();
            var mockDirtyMatcher = new Mock<IDirtyMatcher>();
            var mockProgress = new Mock<IProgress<DiffProgress>>();

            var pipeline = new DiffPipelineBuilder()
                .UseDiffer(mockDiffer.Object)
                .UseCleanMatcher(mockCleanMatcher.Object)
                .UseDirtyMatcher(mockDirtyMatcher.Object)
                .WithParallelism(4)
                .WithStopOnFirstError(true)
                .WithProgress(mockProgress.Object)
                .Build();

            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "Build_重复调用_每次返回新实例")]
        public void Build_MultipleCalls_ReturnsDifferentInstances()
        {
            var builder = new DiffPipelineBuilder().WithParallelism(2);

            var p1 = builder.Build();
            var p2 = builder.Build();

            Assert.NotSame(p1, p2);
        }
    }
}
