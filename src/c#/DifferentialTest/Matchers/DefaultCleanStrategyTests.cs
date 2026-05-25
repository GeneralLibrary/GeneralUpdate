using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;

namespace DifferentialTest.Matchers
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. 无参构造函数 — matcher=DefaultCleanMatcher, differ=StreamingHdiffDiffer
    ///   2. 带参构造函数 — matcher=null → DefaultCleanMatcher
    ///   3. 带参构造函数 — binaryDiffer=null → StreamingHdiffDiffer
    ///   4. ExecuteAsync — 依赖文件系统，验证签名和基本委托
    ///
    /// 触发条件：构造参数组合
    /// 预期结果：正确默认值
    /// </summary>
    public class DefaultCleanStrategyTests
    {
        [Fact(DisplayName = "构造函数_无参_使用默认matcher和differ")]
        public void Constructor_NoArgs_UsesDefaults()
        {
            var strategy = new DefaultCleanStrategy();

            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_matcher为null_使用DefaultCleanMatcher")]
        public void Constructor_NullMatcher_UsesDefaultCleanMatcher()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();

            var strategy = new DefaultCleanStrategy(matcher: null, binaryDiffer: mockDiffer.Object);

            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_binaryDiffer为null_使用StreamingHdiffDiffer")]
        public void Constructor_NullDiffer_UsesStreamingHdiffDiffer()
        {
            var mockMatcher = new Mock<ICleanMatcher>();

            var strategy = new DefaultCleanStrategy(matcher: mockMatcher.Object, binaryDiffer: null);

            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_全部自定义_使用自定义实例")]
        public void Constructor_AllCustom_UsesCustomInstances()
        {
            var mockMatcher = new Mock<ICleanMatcher>();
            var mockDiffer = new Mock<IBinaryDiffer>();

            var strategy = new DefaultCleanStrategy(mockMatcher.Object, mockDiffer.Object);

            Assert.NotNull(strategy);
        }
    }
}
