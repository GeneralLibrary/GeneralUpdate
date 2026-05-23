using System;
using GeneralUpdate.Bowl;

namespace BowlTest.Strategies
{
    /// <summary>
    /// Tests for the new BowlContext type (immutable record struct).
    /// </summary>
    public class BowlContextTests
    {
        [Fact]
        public void BowlContext_Normalize_AppliesDefaultTimeout()
        {
            var ctx = new BowlContext { ProcessNameOrId = "test.exe" };
            var normalized = ctx.Normalize();

            Assert.Equal(30_000, normalized.TimeoutMs);
        }

        [Fact]
        public void BowlContext_Normalize_PreservesExplicitTimeout()
        {
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                TimeoutMs = 60_000,
            };
            var normalized = ctx.Normalize();

            Assert.Equal(60_000, normalized.TimeoutMs);
        }

        [Fact]
        public void BowlContext_Normalize_AppliesDefaultWorkModel()
        {
            var ctx = new BowlContext { ProcessNameOrId = "test.exe" };
            var normalized = ctx.Normalize();

            Assert.Equal("Upgrade", normalized.WorkModel);
        }

        [Fact]
        public void BowlContext_Normalize_PreservesExplicitWorkModel()
        {
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                WorkModel = "Normal",
            };
            var normalized = ctx.Normalize();

            Assert.Equal("Normal", normalized.WorkModel);
        }

        [Fact]
        public void BowlContext_Normalize_AppliesDefaultDumpType()
        {
            var ctx = new BowlContext { ProcessNameOrId = "test.exe" };
            var normalized = ctx.Normalize();

            Assert.Equal(DumpType.Full, normalized.DumpType);
        }

        [Fact]
        public void BowlContext_Normalize_PreservesAllFields()
        {
            var ctx = new BowlContext
            {
                ProcessNameOrId = "MyApp.exe",
                DumpFileName = "crash.dmp",
                FailFileName = "crash.json",
                TargetPath = @"C:\App",
                FailDirectory = @"C:\App\fail\1.0",
                BackupDirectory = @"C:\App\1.0",
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                AutoRestore = true,
            };
            var normalized = ctx.Normalize();

            Assert.Equal("MyApp.exe", normalized.ProcessNameOrId);
            Assert.Equal("crash.dmp", normalized.DumpFileName);
            Assert.Equal("crash.json", normalized.FailFileName);
            Assert.Equal(@"C:\App", normalized.TargetPath);
            Assert.Equal(@"C:\App\fail\1.0", normalized.FailDirectory);
            Assert.Equal(@"C:\App\1.0", normalized.BackupDirectory);
            Assert.Equal("Upgrade", normalized.WorkModel);
            Assert.Equal("1.0.0", normalized.ExtendedField);
            Assert.True(normalized.AutoRestore);
        }

        [Fact]
        public void BowlContext_Normalize_IsValueType()
        {
            var ctx1 = new BowlContext { ProcessNameOrId = "a.exe", TimeoutMs = 0 };
            var ctx2 = ctx1.Normalize();

            // Normalize returns a new instance; original is unchanged
            Assert.Equal(0, ctx1.TimeoutMs);
            Assert.Equal(30_000, ctx2.TimeoutMs);
        }
    }
}
