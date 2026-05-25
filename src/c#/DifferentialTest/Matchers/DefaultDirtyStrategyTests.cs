using Moq;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;

namespace DifferentialTest.Matchers
{
    public class DefaultDirtyStrategyTests
    {
        [Fact(DisplayName = "构造函数_无参_使用默认matcher和differ")]
        public void Constructor_NoArgs_UsesDefaults()
        {
            var strategy = new DefaultDirtyStrategy();
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_matcher为null_使用DefaultDirtyMatcher")]
        public void Constructor_NullMatcher_UsesDefaultDirtyMatcher()
        {
            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultDirtyStrategy(matcher: null, binaryDiffer: mockDiffer.Object);
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_binaryDiffer为null_使用StreamingHdiffDiffer")]
        public void Constructor_NullDiffer_UsesStreamingHdiffDiffer()
        {
            var mockMatcher = new Mock<IDirtyMatcher>();
            var strategy = new DefaultDirtyStrategy(matcher: mockMatcher.Object, binaryDiffer: null);
            Assert.NotNull(strategy);
        }

        [Fact(DisplayName = "构造函数_全部自定义_使用自定义实例")]
        public void Constructor_AllCustom_UsesCustomInstances()
        {
            var mockMatcher = new Mock<IDirtyMatcher>();
            var mockDiffer = new Mock<IBinaryDiffer>();
            var strategy = new DefaultDirtyStrategy(mockMatcher.Object, mockDiffer.Object);
            Assert.NotNull(strategy);
        }
    }
}
