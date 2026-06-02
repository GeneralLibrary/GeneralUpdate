using GeneralUpdate.Core.Pipeline;

namespace DifferentialTest.Pipeline
{
    /// <summary>
    /// Unit tests for <see cref="DiffPipelineOptions"/>.
    /// </summary>
    public class DiffPipelineOptionsTests
    {
        [Fact]
        public void DefaultConstructor_MaxDegreeOfParallelism_Equals2()
        {
            var options = new DiffPipelineOptions();
            Assert.Equal(2, options.MaxDegreeOfParallelism);
        }

        [Fact]
        public void DefaultConstructor_StopOnFirstError_IsFalse()
        {
            var options = new DiffPipelineOptions();
            Assert.False(options.StopOnFirstError);
        }

        [Fact]
        public void MaxDegreeOfParallelism_Set_ReturnsNewValue()
        {
            var options = new DiffPipelineOptions { MaxDegreeOfParallelism = 8 };
            Assert.Equal(8, options.MaxDegreeOfParallelism);
        }

        [Fact]
        public void StopOnFirstError_Set_ReturnsNewValue()
        {
            var options = new DiffPipelineOptions { StopOnFirstError = true };
            Assert.True(options.StopOnFirstError);
        }

        [Theory]
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
