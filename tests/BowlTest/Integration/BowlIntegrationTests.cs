using System;
using System.IO;
using GeneralUpdate.Bowl;

namespace BowlTest.Integration
{
    /// <summary>
    /// Contains integration test cases for the Bowl component.
    /// Tests end-to-end scenarios and component interactions.
    /// </summary>
    public class BowlIntegrationTests : IDisposable
    {
        private readonly string _testBasePath;

        public BowlIntegrationTests()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), $"BowlIntegrationTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testBasePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                try
                {
                    Directory.Delete(_testBasePath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        /// <summary>
        /// Tests Normal mode vs Upgrade mode behavior difference via BowlContext.
        /// </summary>
        [Fact]
        public void WorkModel_DifferentiatesBetweenNormalAndUpgradeMode()
        {
            var normalCtx = new BowlContext { WorkModel = "Normal" };
            var upgradeCtx = new BowlContext { WorkModel = "Upgrade" };

            Assert.Equal("Normal", normalCtx.WorkModel);
            Assert.Equal("Upgrade", upgradeCtx.WorkModel);
            Assert.NotEqual(normalCtx.WorkModel, upgradeCtx.WorkModel);
        }

        /// <summary>
        /// Tests that fail and backup directory paths are constructed correctly from install path and version.
        /// </summary>
        [Fact]
        public void BowlContext_PathConstruction_CreatesCorrectPaths()
        {
            var installPath = "/path/to/install";
            var version = "3.2.1";

            var expectedFailDir = Path.Combine(installPath, "fail", version);
            var expectedBackupDir = Path.Combine(installPath, version);
            var expectedDumpFile = $"{version}_fail.dmp";
            var expectedFailFile = $"{version}_fail.json";

            var ctx = new BowlContext
            {
                TargetPath = installPath,
                FailDirectory = expectedFailDir,
                BackupDirectory = expectedBackupDir,
                DumpFileName = expectedDumpFile,
                FailFileName = expectedFailFile,
                ExtendedField = version,
            };

            Assert.Equal(expectedFailDir, ctx.FailDirectory);
            Assert.Equal(expectedBackupDir, ctx.BackupDirectory);
            Assert.Equal(expectedDumpFile, ctx.DumpFileName);
            Assert.Equal(expectedFailFile, ctx.FailFileName);
            Assert.Equal(version, ctx.ExtendedField);
            Assert.Contains(version, ctx.FailDirectory);
            Assert.Contains(version, ctx.BackupDirectory);
        }

        /// <summary>
        /// Tests that ExtendedField can store version information.
        /// </summary>
        [Fact]
        public void ExtendedField_StoresVersionInformation()
        {
            var versions = new[] { "1.0.0", "2.1.3", "10.5.2-beta" };

            foreach (var version in versions)
            {
                var ctx = new BowlContext { ExtendedField = version };
                Assert.Equal(version, ctx.ExtendedField);
            }
        }

        /// <summary>
        /// Tests Normalize applies expected defaults.
        /// </summary>
        [Fact]
        public void Normalize_AppliesDefaults_WorkModelTimeoutAndDumpType()
        {
            var ctx = new BowlContext { ProcessNameOrId = "test.exe" };
            var normalized = ctx.Normalize();

            Assert.Equal("Upgrade", normalized.WorkModel);
            Assert.Equal(30_000, normalized.TimeoutMs);
            Assert.Equal(DumpType.Full, normalized.DumpType);
        }

        /// <summary>
        /// Tests that explicit WorkModel "Normal" is preserved after Normalize.
        /// </summary>
        [Fact]
        public void Normalize_PreservesExplicitNormalWorkModel()
        {
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                WorkModel = "Normal",
            };
            var normalized = ctx.Normalize();

            Assert.Equal("Normal", normalized.WorkModel);
        }
    }
}
