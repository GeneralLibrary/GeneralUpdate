using GeneralUpdate.Bowl;

namespace BowlTest.Strategies
{
    /// <summary>
    /// Tests for the BowlResult record struct.
    /// </summary>
    public class BowlResultTests
    {
        [Fact]
        public void BowlResult_Ok_HasSuccessTrue()
        {
            var result = BowlResult.Ok;

            Assert.True(result.Success);
            Assert.Equal(0, result.ExitCode);
            Assert.False(result.DumpCaptured);
            Assert.Null(result.DumpFilePath);
            Assert.Null(result.CrashReportPath);
            Assert.False(result.Restored);
        }

        [Fact]
        public void BowlResult_CrashScenario_HasExpectedValues()
        {
            var result = new BowlResult
            {
                Success = false,
                ExitCode = -1,
                DumpCaptured = true,
                DumpFilePath = @"C:\fail\crash.dmp",
                CrashReportPath = @"C:\fail\crash.json",
                Restored = true,
            };

            Assert.False(result.Success);
            Assert.Equal(-1, result.ExitCode);
            Assert.True(result.DumpCaptured);
            Assert.Equal(@"C:\fail\crash.dmp", result.DumpFilePath);
            Assert.Equal(@"C:\fail\crash.json", result.CrashReportPath);
            Assert.True(result.Restored);
        }
    }
}
