using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Core.Pipeline;
using Xunit;

namespace CoreTest.Pipeline
{
    /// <summary>
    /// Contains test cases for the PatchMiddleware class.
    /// Tests binary patching functionality for incremental updates.
    /// </summary>
    public class PatchMiddlewareTests
    {
        /// <summary>
        /// Tests that PatchMiddleware can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var middleware = new PatchMiddleware();

            // Assert
            Assert.NotNull(middleware);
        }

        /// <summary>
        /// Tests that context stores patch-related values correctly.
        /// </summary>
        [Fact]
        public void Context_CanStorePatchPaths()
        {
            // Arrange
            var context = new PipelineContext();
            var sourcePath = "/test/source";
            var targetPath = "/test/target";

            // Act
            context.Add("SourcePath", sourcePath);
            context.Add("PatchPath", targetPath);

            // Assert
            Assert.Equal(sourcePath, context.Get<string>("SourcePath"));
            Assert.Equal(targetPath, context.Get<string>("PatchPath"));
        }
    }
}
