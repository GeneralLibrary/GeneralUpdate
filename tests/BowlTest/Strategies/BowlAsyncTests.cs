using System;
using System.Threading.Tasks;
using GeneralUpdate.Bowl;

namespace BowlTest.Strategies
{
    /// <summary>
    /// Tests for the new async Bowl API (instance-based).
    /// </summary>
    public class BowlAsyncTests
    {
        /// <summary>
        /// Bowl constructor with defaults should not throw.
        /// </summary>
        [Fact]
        public void Bowl_Constructor_DoesNotThrow()
        {
            var bowl = new BowlBootstrap();
            Assert.NotNull(bowl);
        }

        /// <summary>
        /// LaunchAsync with non-existent procdump path �?either throws Win32Exception
        /// (file not found) or returns a result with no dump captured.
        /// </summary>
        [Fact]
        public async Task LaunchAsync_WithInvalidAppPath_Throws()
        {
            var bowl = new BowlBootstrap();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "__nonexistent_app_42__",
                TargetPath = System.IO.Path.GetTempPath(),
                FailDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fail", "test"),
                DumpFileName = "test.dmp",
                FailFileName = "test.json",
                WorkModel = "Normal",
                TimeoutMs = 5_000,
            };

            try
            {
                var result = await bowl.LaunchAsync(ctx);
                Assert.False(result.DumpCaptured);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Expected: procdump binary not found in temp directory
            }
            catch (InvalidOperationException)
            {
                // Also expected: process start failure
            }
        }

    }
}
