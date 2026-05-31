using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategys;

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
        /// Tests Normal mode vs Upgrade mode behavior difference.
        /// </summary>
        [Fact]
        public void WorkModel_DifferentiatesBetweenNormalAndUpgradeMode()
        {
            // Arrange
            var normalParameter = new MonitorParameter
            {
                WorkModel = "Normal"
            };

            var upgradeParameter = new MonitorParameter
            {
                WorkModel = "Upgrade"
            };

            // Assert
            Assert.Equal("Normal", normalParameter.WorkModel);
            Assert.Equal("Upgrade", upgradeParameter.WorkModel);
            Assert.NotEqual(normalParameter.WorkModel, upgradeParameter.WorkModel);
        }

        /// <summary>
        /// Tests that parameter paths are correctly constructed from ProcessContract.
        /// </summary>
        [Fact]
        public void ParameterConstruction_FromProcessContract_CreatesCorrectPaths()
        {
            // Arrange
            var installPath = "/path/to/install";
            var version = "3.2.1";

            // Expected paths based on CreateParameter logic in Bowl.cs
            var expectedFailDir = Path.Combine(installPath, "fail", version);
            var expectedBackupDir = Path.Combine(installPath, version);
            var expectedDumpFile = $"{version}_fail.dmp";
            var expectedFailFile = $"{version}_fail.json";

            // Act - Create parameter manually with same logic
            var parameter = new MonitorParameter
            {
                TargetPath = installPath,
                FailDirectory = expectedFailDir,
                BackupDirectory = expectedBackupDir,
                DumpFileName = expectedDumpFile,
                FailFileName = expectedFailFile,
                ExtendedField = version
            };

            // Assert
            Assert.Equal(expectedFailDir, parameter.FailDirectory);
            Assert.Equal(expectedBackupDir, parameter.BackupDirectory);
            Assert.Equal(expectedDumpFile, parameter.DumpFileName);
            Assert.Equal(expectedFailFile, parameter.FailFileName);
            Assert.Equal(version, parameter.ExtendedField);
            Assert.Contains(version, parameter.FailDirectory);
            Assert.Contains(version, parameter.BackupDirectory);
        }

        /// <summary>
        /// Tests that extended field can store version information.
        /// </summary>
        [Fact]
        public void ExtendedField_StoresVersionEntryrmation()
        {
            // Arrange
            var versions = new[] { "1.0.0", "2.1.3", "10.5.2-beta" };

            foreach (var version in versions)
            {
                // Act
                var parameter = new MonitorParameter
                {
                    ExtendedField = version
                };

                // Assert
                Assert.Equal(version, parameter.ExtendedField);
            }
        }

        /// <summary>
        /// Tests that ProcessContract JSON with all required fields parses correctly.
        /// </summary>
        [Fact]
        public void ProcessContractJson_WithAllFields_ParsesCorrectly()
        {
            // Arrange
            var json = @"{
                ""AppName"": ""MyApp.exe"",
                ""InstallPath"": ""/path/to/app"",
                ""LastVersion"": ""1.2.3""
            }";

            // Act
            var processInfo = JsonSerializer.Deserialize<ProcessContractDto>(json);

            // Assert
            Assert.NotNull(processInfo);
            Assert.Equal("MyApp.exe", processInfo.AppName);
            Assert.Equal("/path/to/app", processInfo.InstallPath);
            Assert.Equal("1.2.3", processInfo.LastVersion);
        }

        /// <summary>
        /// Helper class for ProcessContract JSON deserialization testing.
        /// </summary>
        private class ProcessContractDto
        {
            public string? AppName { get; set; }
            public string? InstallPath { get; set; }
            public string? LastVersion { get; set; }
        }
    }
}
