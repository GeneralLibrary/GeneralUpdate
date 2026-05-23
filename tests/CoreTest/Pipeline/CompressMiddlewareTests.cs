using System.Text;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Pipeline;
using Xunit;

namespace CoreTest.Pipeline
{
    /// <summary>
    /// Contains test cases for the CompressMiddleware class.
    /// Tests decompression functionality for update packages.
    /// </summary>
    public class CompressMiddlewareTests
    {
        /// <summary>
        /// Tests that InvokeAsync requires necessary context values.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingContextValues_ThrowsException()
        {
            // Arrange
            var middleware = new CompressMiddleware();
            var context = new PipelineContext();
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => middleware.InvokeAsync(context));
        }

        /// <summary>
        /// Tests that context contains expected keys after setup.
        /// </summary>
        [Fact]
        public void Context_CanStoreAndRetrieveValues()
        {
            // Arrange
            var context = new PipelineContext();
            var format = Format.ZIP;
            var sourcePath = "/test/source.zip";
            var patchPath = "/test/patch";
            var encoding = Encoding.UTF8;
            var appPath = "/test/app";
            var patchEnabled = true;

            // Act
            context.Add("Format", format);
            context.Add("ZipFilePath", sourcePath);
            context.Add("PatchPath", patchPath);
            context.Add("Encoding", encoding);
            context.Add("SourcePath", appPath);
            context.Add("PatchEnabled", patchEnabled);

            // Assert
            Assert.Equal(format, context.Get<string>("Format"));
            Assert.Equal(sourcePath, context.Get<string>("ZipFilePath"));
            Assert.Equal(patchPath, context.Get<string>("PatchPath"));
            Assert.Equal(encoding, context.Get<Encoding>("Encoding"));
            Assert.Equal(appPath, context.Get<string>("SourcePath"));
            Assert.Equal(patchEnabled, context.Get<bool?>("PatchEnabled"));
        }
    }
}
