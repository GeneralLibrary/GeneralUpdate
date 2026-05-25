using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;
using GeneralUpdate.Differential.Models;
using GeneralUpdate.Differential.Pipeline;

namespace DifferentialTest.Pipeline
{
    /// <summary>
    /// 分支覆盖点：构造函数、参数验证、异常分支。
    /// 触发条件：各种构造参数和运行时参数的组合。
    /// 预期结果：参数验证、异常分支正确。
    /// </summary>
    public class DiffPipelineTests
    {
        [Fact(DisplayName = "构造函数_无参_使用所有默认值")]
        public void Constructor_NoArgs_UsesAllDefaults()
        {
            var pipeline = new DiffPipeline();
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "构造函数_仅options_默认differ和matchers")]
        public void Constructor_OptionsOnly_UsesDefaults()
        {
            var options = new DiffPipelineOptions { MaxDegreeOfParallelism = 2 };
            var pipeline = new DiffPipeline(options);
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "构造函数_options为null_抛出ArgumentNullException")]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                DiffPipelineOptions? opt = null;
                _ = new DiffPipeline(options: opt!, binaryDiffer: mockDiffer.Object, cleanMatcher: (ICleanMatcher?)null);
            });
            Assert.Equal("options", ex.ParamName);
        }

        [Fact(DisplayName = "构造函数_binaryDiffer为null_抛出ArgumentNullException")]
        public void Constructor_NullDiffer_ThrowsArgumentNullException()
        {
            var options = new DiffPipelineOptions();
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                IBinaryDiffer? diff = null;
                _ = new DiffPipeline(options: options, binaryDiffer: diff!, cleanMatcher: (ICleanMatcher?)null);
            });
            Assert.Equal("binaryDiffer", ex.ParamName);
        }

        [Fact(DisplayName = "构造函数_cleanMatcher为null_使用DefaultCleanMatcher")]
        public void Constructor_NullCleanMatcher_UsesDefault()
        {
            var options = new DiffPipelineOptions();
            var mockDiffer = new Mock<IBinaryDiffer>();
            ICleanMatcher? cm = null;
            IDirtyMatcher? dm = null;
            IProgress<DiffProgress>? pr = null;
            var pipeline = new DiffPipeline(options, mockDiffer.Object, cm, dm, pr);
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "构造函数_dirtyMatcher为null_使用DefaultDirtyMatcher")]
        public void Constructor_NullDirtyMatcher_UsesDefault()
        {
            var options = new DiffPipelineOptions();
            var mockDiffer = new Mock<IBinaryDiffer>();
            ICleanMatcher? cm = null;
            IDirtyMatcher? dm = null;
            IProgress<DiffProgress>? pr = null;
            var pipeline = new DiffPipeline(options, mockDiffer.Object, cm, dm, pr);
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "构造函数_progress为null_允许")]
        public void Constructor_NullProgress_Allowed()
        {
            var options = new DiffPipelineOptions();
            var mockDiffer = new Mock<IBinaryDiffer>();
            ICleanMatcher? cm = null;
            IDirtyMatcher? dm = null;
            IProgress<DiffProgress>? pr = null;
            var pipeline = new DiffPipeline(options, mockDiffer.Object, cm, dm, pr);
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "构造函数_全参自定义_创建成功")]
        public void Constructor_AllCustom_CreatesSuccessfully()
        {
            var options = new DiffPipelineOptions { MaxDegreeOfParallelism = 4 };
            var mockDiffer = new Mock<IBinaryDiffer>();
            var mockCleanMatcher = new Mock<ICleanMatcher>();
            var mockDirtyMatcher = new Mock<IDirtyMatcher>();
            var mockProgress = new Mock<IProgress<DiffProgress>>();
            var pipeline = new DiffPipeline(options, mockDiffer.Object,
                mockCleanMatcher.Object, mockDirtyMatcher.Object, mockProgress.Object);
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "构造函数_兼容3参重载_创建成功")]
        public void Constructor_CompatOverload_CreatesSuccessfully()
        {
            var options = new DiffPipelineOptions();
            var mockDiffer = new Mock<IBinaryDiffer>();
            var mockProgress = new Mock<IProgress<DiffProgress>>();
            var pipeline = new DiffPipeline(options, mockDiffer.Object, mockProgress.Object);
            Assert.NotNull(pipeline);
        }

        [Fact(DisplayName = "CleanAsync_sourcePath为null_抛出ArgumentNullException")]
        public async Task CleanAsync_NullSourcePath_ThrowsArgumentNullException()
        {
            var pipeline = new DiffPipeline();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                pipeline.CleanAsync(null!, "target", "patch"));
        }

        [Fact(DisplayName = "CleanAsync_sourcePath为空白_抛出ArgumentNullException")]
        public async Task CleanAsync_WhitespaceSourcePath_ThrowsArgumentNullException()
        {
            var pipeline = new DiffPipeline();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                pipeline.CleanAsync("  ", "target", "patch"));
        }

        [Fact(DisplayName = "CleanAsync_targetPath为null_抛出ArgumentNullException")]
        public async Task CleanAsync_NullTargetPath_ThrowsArgumentNullException()
        {
            var pipeline = new DiffPipeline();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                pipeline.CleanAsync("source", null!, "patch"));
        }

        [Fact(DisplayName = "CleanAsync_patchPath为null_抛出ArgumentNullException")]
        public async Task CleanAsync_NullPatchPath_ThrowsArgumentNullException()
        {
            var pipeline = new DiffPipeline();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                pipeline.CleanAsync("source", "target", null!));
        }

        [Fact(DisplayName = "CleanAsync_目录不存在_抛出DirectoryNotFoundException")]
        public async Task CleanAsync_NonExistentDir_ThrowsDirectoryNotFoundException()
        {
            var pipeline = new DiffPipeline();
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                pipeline.CleanAsync(
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }

        [Fact(DisplayName = "CleanAsync_CancellationToken已取消_抛出OperationCanceledException")]
        public async Task CleanAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var pipeline = new DiffPipeline();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                pipeline.CleanAsync("src", "tgt", "patch", cancellationToken: cts.Token));
        }

        [Fact(DisplayName = "DirtyAsync_CancellationToken已取消_抛出OperationCanceledException")]
        public async Task DirtyAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var pipeline = new DiffPipeline();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                pipeline.DirtyAsync("app", "patch", cancellationToken: cts.Token));
        }
    }
}
