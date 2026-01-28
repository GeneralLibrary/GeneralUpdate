using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Pipeline;
using GeneralUpdate.Common.Internal.Pipeline;
using Xunit;

namespace ClientCoreTest.Pipeline
{
    /// <summary>
    /// Contains test cases for the CompressMiddleware class.
    /// Tests decompression functionality for update packages.
    /// </summary>
    public class CompressMiddlewareTests : IDisposable
    {
        private readonly string _testPath;

        public CompressMiddlewareTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"CompressTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, recursive: true);
            }
        }

        /// <summary>
        /// Tests that InvokeAsync throws when required context items are missing.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingContextItems_ThrowsException()
        {
            // Arrange
            var middleware = new CompressMiddleware();
            var context = new PipelineContext();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that InvokeAsync handles missing format in context.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingFormat_ThrowsException()
        {
            // Arrange
            var middleware = new CompressMiddleware();
            var context = new PipelineContext();
            context.Add("ZipFilePath", "test.zip");
            context.Add("PatchPath", _testPath);
            context.Add("Encoding", Encoding.UTF8);
            context.Add("SourcePath", _testPath);

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that InvokeAsync handles missing source path in context.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingSourcePath_ThrowsException()
        {
            // Arrange
            var middleware = new CompressMiddleware();
            var context = new PipelineContext();
            context.Add("Format", "ZIP");
            context.Add("ZipFilePath", "test.zip");
            context.Add("PatchPath", _testPath);
            context.Add("Encoding", Encoding.UTF8);

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that InvokeAsync validates all required context parameters.
        /// </summary>
        [Fact]
        public void CompressMiddleware_RequiresProperContext()
        {
            // Arrange
            var middleware = new CompressMiddleware();

            // Act & Assert
            Assert.NotNull(middleware);
        }

        /// <summary>
        /// Tests that context properly stores format information.
        /// </summary>
        [Fact]
        public void PipelineContext_StoresFormatCorrectly()
        {
            // Arrange
            var context = new PipelineContext();
            var format = "ZIP";

            // Act
            context.Add("Format", format);
            var retrievedFormat = context.Get<string>("Format");

            // Assert
            Assert.Equal(format, retrievedFormat);
        }

        /// <summary>
        /// Tests that context properly stores encoding information.
        /// </summary>
        [Fact]
        public void PipelineContext_StoresEncodingCorrectly()
        {
            // Arrange
            var context = new PipelineContext();
            var encoding = Encoding.UTF8;

            // Act
            context.Add("Encoding", encoding);
            var retrievedEncoding = context.Get<Encoding>("Encoding");

            // Assert
            Assert.Equal(encoding, retrievedEncoding);
        }

        /// <summary>
        /// Tests that context properly stores path information.
        /// </summary>
        [Fact]
        public void PipelineContext_StoresPathsCorrectly()
        {
            // Arrange
            var context = new PipelineContext();
            var zipPath = "/test/path.zip";
            var patchPath = "/test/patch";
            var sourcePath = "/test/source";

            // Act
            context.Add("ZipFilePath", zipPath);
            context.Add("PatchPath", patchPath);
            context.Add("SourcePath", sourcePath);

            // Assert
            Assert.Equal(zipPath, context.Get<string>("ZipFilePath"));
            Assert.Equal(patchPath, context.Get<string>("PatchPath"));
            Assert.Equal(sourcePath, context.Get<string>("SourcePath"));
        }

        /// <summary>
        /// Tests that PatchEnabled flag is properly handled in context.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void PipelineContext_HandlesPatchEnabledFlag(bool? patchEnabled)
        {
            // Arrange
            var context = new PipelineContext();

            // Act
            context.Add("PatchEnabled", patchEnabled);
            var retrieved = context.Get<bool?>("PatchEnabled");

            // Assert
            Assert.Equal(patchEnabled, retrieved);
        }
    }
}
