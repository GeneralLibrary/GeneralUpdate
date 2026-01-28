using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Pipeline;
using GeneralUpdate.Common.Internal.Pipeline;
using Xunit;

namespace ClientCoreTest.Pipeline
{
    /// <summary>
    /// Contains test cases for the HashMiddleware class.
    /// Tests hash verification functionality.
    /// </summary>
    public class HashMiddlewareTests : IDisposable
    {
        private readonly string _testPath;

        public HashMiddlewareTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"HashTest_{Guid.NewGuid()}");
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
        /// Tests that InvokeAsync throws CryptographicException when hash does not match.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithIncorrectHash_ThrowsCryptographicException()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var testFile = Path.Combine(_testPath, "test.txt");
            File.WriteAllText(testFile, "test content");
            
            var context = new PipelineContext();
            context.Add("ZipFilePath", testFile);
            context.Add("Hash", "incorrecthash123");

            // Act & Assert
            await Assert.ThrowsAsync<CryptographicException>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that InvokeAsync succeeds when hash matches.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithCorrectHash_Succeeds()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var testFile = Path.Combine(_testPath, "test.txt");
            var content = "test content for hash verification";
            File.WriteAllText(testFile, content);
            
            // Calculate the correct hash
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(testFile);
            var hashBytes = sha256.ComputeHash(stream);
            var correctHash = BitConverter.ToString(hashBytes).Replace("-", "");
            
            var context = new PipelineContext();
            context.Add("ZipFilePath", testFile);
            context.Add("Hash", correctHash);

            // Act
            await middleware.InvokeAsync(context);

            // Assert - no exception means success
            Assert.True(true);
        }

        /// <summary>
        /// Tests that InvokeAsync throws when file path is missing from context.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingFilePath_ThrowsException()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var context = new PipelineContext();
            context.Add("Hash", "somehash");

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that InvokeAsync throws when hash is missing from context.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithMissingHash_ThrowsException()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var testFile = Path.Combine(_testPath, "test.txt");
            File.WriteAllText(testFile, "test content");
            
            var context = new PipelineContext();
            context.Add("ZipFilePath", testFile);

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that InvokeAsync handles file not found gracefully.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithNonExistentFile_ThrowsException()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var nonExistentFile = Path.Combine(_testPath, "nonexistent.txt");
            
            var context = new PipelineContext();
            context.Add("ZipFilePath", nonExistentFile);
            context.Add("Hash", "somehash");

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await middleware.InvokeAsync(context);
            });
        }

        /// <summary>
        /// Tests that hash verification is case-insensitive.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithDifferentCaseHash_Succeeds()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var testFile = Path.Combine(_testPath, "test.txt");
            File.WriteAllText(testFile, "test content");
            
            // Calculate the correct hash
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(testFile);
            var hashBytes = sha256.ComputeHash(stream);
            var correctHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            
            var context = new PipelineContext();
            context.Add("ZipFilePath", testFile);
            context.Add("Hash", correctHash.ToUpper());

            // Act
            await middleware.InvokeAsync(context);

            // Assert - no exception means success
            Assert.True(true);
        }
    }
}
