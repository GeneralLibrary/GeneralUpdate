using System.Security.Cryptography;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Core.Pipeline;
using Xunit;

namespace CoreTest.Pipeline
{
    /// <summary>
    /// Contains test cases for the HashMiddleware class.
    /// Tests hash verification functionality for downloaded files.
    /// </summary>
    public class HashMiddlewareTests
    {
        /// <summary>
        /// Tests that InvokeAsync throws CryptographicException when hash verification fails.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithInvalidHash_ThrowsCryptographicException()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var context = new PipelineContext();
            
            // Create a temporary test file
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");
            
            try
            {
                context.Add("ZipFilePath", tempFile);
                context.Add("Hash", "invalidhash123");

                // Act & Assert
                await Assert.ThrowsAsync<CryptographicException>(() => middleware.InvokeAsync(context));
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Tests that InvokeAsync succeeds with valid hash.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithValidHash_Succeeds()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var context = new PipelineContext();
            
            // Create a temporary test file
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");
            
            try
            {
                // Compute the actual SHA256 hash of the file
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(tempFile))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    
                    context.Add("ZipFilePath", tempFile);
                    context.Add("Hash", hash);

                    // Act - should not throw
                    await middleware.InvokeAsync(context);
                }
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Tests that hash comparison is case-insensitive.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_WithUppercaseHash_Succeeds()
        {
            // Arrange
            var middleware = new HashMiddleware();
            var context = new PipelineContext();
            
            // Create a temporary test file
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");
            
            try
            {
                // Compute the actual SHA256 hash of the file in uppercase
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(tempFile))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
                    
                    context.Add("ZipFilePath", tempFile);
                    context.Add("Hash", hash);

                    // Act - should not throw
                    await middleware.InvokeAsync(context);
                }
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
