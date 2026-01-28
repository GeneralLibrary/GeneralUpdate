using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Core.Pipeline;
using Xunit;

namespace CoreTest.Pipeline
{
    /// <summary>
    /// Contains test cases for the DriverMiddleware class.
    /// Tests driver installation and management functionality.
    /// </summary>
    public class DriverMiddlewareTests
    {
        /// <summary>
        /// Tests that DriverMiddleware can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var middleware = new DriverMiddleware();

            // Assert
            Assert.NotNull(middleware);
        }

        /// <summary>
        /// Tests that InvokeAsync returns early when DriverOutPut is missing.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingDriverOutPut_ReturnsEarly()
        {
            // Arrange
            var middleware = new DriverMiddleware();
            var context = new PipelineContext();

            // Act - should return early without throwing
            await middleware.InvokeAsync(context);
        }

        /// <summary>
        /// Tests that InvokeAsync returns early when PatchPath is missing.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingPatchPath_ReturnsEarly()
        {
            // Arrange
            var middleware = new DriverMiddleware();
            var context = new PipelineContext();
            context.Add("DriverOutPut", "/test/output");

            // Act - should return early without throwing
            await middleware.InvokeAsync(context);
        }

        /// <summary>
        /// Tests that InvokeAsync returns early when FieldMappings is missing.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingFieldMappings_ReturnsEarly()
        {
            // Arrange
            var middleware = new DriverMiddleware();
            var context = new PipelineContext();
            context.Add("DriverOutPut", "/test/output");
            context.Add("PatchPath", "/test/patch");

            // Act - should return early without throwing
            await middleware.InvokeAsync(context);
        }

        /// <summary>
        /// Tests that context can store driver-related values.
        /// </summary>
        [Fact]
        public void Context_CanStoreDriverValues()
        {
            // Arrange
            var context = new PipelineContext();
            var outPutPath = "/test/output";
            var patchPath = "/test/patch";
            var fieldMappings = new Dictionary<string, string>
            {
                { "field1", "value1" },
                { "field2", "value2" }
            };

            // Act
            context.Add("DriverOutPut", outPutPath);
            context.Add("PatchPath", patchPath);
            context.Add("FieldMappings", fieldMappings);

            // Assert
            Assert.Equal(outPutPath, context.Get<string>("DriverOutPut"));
            Assert.Equal(patchPath, context.Get<string>("PatchPath"));
            Assert.Equal(fieldMappings, context.Get<Dictionary<string, string>>("FieldMappings"));
        }
    }
}
