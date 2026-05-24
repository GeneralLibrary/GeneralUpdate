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
            var bowl = new Bowl();
            Assert.NotNull(bowl);
        }

        /// <summary>
        /// LaunchAsync with non-existent procdump path â€?either throws Win32Exception
        /// (file not found) or returns a result with no dump captured.
        /// </summary>
        [Fact]
        public async Task LaunchAsync_WithInvalidAppPath_Throws()
        {
            var bowl = new Bowl();
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

        /// <summary>
        /// MapToContext correctly translates old MonitorParameter to new BowlContext.
        /// </summary>
        [Fact]
        public void MapToContext_TranslatesAllFields()
        {
            var old = new GeneralUpdate.Bowl.Strategys.MonitorParameter
            {
                ProcessNameOrId = "app.exe",
                DumpFileName = "crash.dmp",
                FailFileName = "crash.json",
                TargetPath = "/install",
                FailDirectory = "/install/fail/1.0",
                BackupDirectory = "/install/1.0",
                WorkModel = "Normal",
                ExtendedField = "2.0.0",
            };

            var ctx = Bowl.MapToContext(old);

            Assert.Equal("app.exe", ctx.ProcessNameOrId);
            Assert.Equal("crash.dmp", ctx.DumpFileName);
            Assert.Equal("crash.json", ctx.FailFileName);
            Assert.Equal("/install", ctx.TargetPath);
            Assert.Equal("/install/fail/1.0", ctx.FailDirectory);
            Assert.Equal("/install/1.0", ctx.BackupDirectory);
            Assert.Equal("Normal", ctx.WorkModel);
            Assert.Equal("2.0.0", ctx.ExtendedField);
            Assert.Equal(30_000, ctx.TimeoutMs);
            Assert.Equal(DumpType.Full, ctx.DumpType);
            Assert.True(ctx.AutoRestore);
        }
    }
}
