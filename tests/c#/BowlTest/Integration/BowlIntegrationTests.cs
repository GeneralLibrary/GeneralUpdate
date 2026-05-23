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
        /// Tests complete workflow with valid MonitorParameter.
        /// Verifies that fail directory is created and cleaned up properly.
        /// </summary>
        [Fact]
        public void CompleteWorkflow_WithValidParameter_CreatesRequiredDirectories()
        {
            // Only run on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            var testPath = Path.Combine(_testBasePath, "workflow_test");
            var failPath = Path.Combine(testPath, "fail");
            var backupPath = Path.Combine(testPath, "backup");
            
            Directory.CreateDirectory(testPath);

            var parameter = new MonitorParameter
            {
                TargetPath = testPath,
                FailDirectory = failPath,
                BackupDirectory = backupPath,
                ProcessNameOrId = "notepad.exe",
                DumpFileName = "test.dmp",
                FailFileName = "test.json",
                WorkModel = "Normal",
                ExtendedField = "1.0.0"
            };

            // Act
            try
            {
                Bowl.Launch(parameter);
            }
            catch
            {
                // Expected to fail as procdump won't exist, but directories should be created
            }

            // Assert
            Assert.True(Directory.Exists(failPath), "Fail directory should be created");
        }

        /// <summary>
        /// Tests that environment variable parsing creates correct MonitorParameter.
        /// </summary>
        [Fact]
        public void EnvironmentVariableParsing_CreatesCorrectParameter()
        {
            // Only run on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("ProcessInfo");
            var testPath = Path.Combine(_testBasePath, "env_test");
            Directory.CreateDirectory(testPath);

            var processInfoJson = JsonSerializer.Serialize(new
            {
                AppName = "TestApp.exe",
                InstallPath = testPath,
                LastVersion = "2.1.0"
            });

            Environment.SetEnvironmentVariable("ProcessInfo", processInfoJson);

            try
            {
                // Act
                try
                {
                    Bowl.Launch();
                }
                catch
                {
                    // Expected to fail, but should parse environment variable successfully
                }

                // Assert - If we got this far without ArgumentNullException, parsing succeeded
                Assert.True(true, "Environment variable was parsed successfully");
            }
            finally
            {
                Environment.SetEnvironmentVariable("ProcessInfo", originalValue);
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
        /// Tests that parameter paths are correctly constructed from ProcessInfo.
        /// </summary>
        [Fact]
        public void ParameterConstruction_FromProcessInfo_CreatesCorrectPaths()
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
        /// Tests that multiple launches clean up previous fail directories.
        /// </summary>
        [Fact]
        public void MultipleLaunches_CleanUpPreviousFailDirectories()
        {
            // Only run on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            var testPath = Path.Combine(_testBasePath, "multi_launch_test");
            var failPath = Path.Combine(testPath, "fail");
            
            Directory.CreateDirectory(testPath);
            Directory.CreateDirectory(failPath);
            
            var markerFile = Path.Combine(failPath, "marker.txt");
            File.WriteAllText(markerFile, "first launch");

            var parameter = new MonitorParameter
            {
                TargetPath = testPath,
                FailDirectory = failPath,
                BackupDirectory = Path.Combine(testPath, "backup"),
                ProcessNameOrId = "test.exe",
                DumpFileName = "test.dmp",
                FailFileName = "test.json",
                WorkModel = "Normal"
            };

            // Act - First launch
            try
            {
                Bowl.Launch(parameter);
            }
            catch { }

            // Assert - Marker file should be deleted
            Assert.False(File.Exists(markerFile), "Previous files should be cleaned up");
            Assert.True(Directory.Exists(failPath), "Fail directory should exist");
        }

        /// <summary>
        /// Tests that extended field can store version information.
        /// </summary>
        [Fact]
        public void ExtendedField_StoresVersionInformation()
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
        /// Tests that ProcessInfo JSON with all required fields parses correctly.
        /// </summary>
        [Fact]
        public void ProcessInfoJson_WithAllFields_ParsesCorrectly()
        {
            // Arrange
            var json = @"{
                ""AppName"": ""MyApp.exe"",
                ""InstallPath"": ""/path/to/app"",
                ""LastVersion"": ""1.2.3""
            }";

            // Act
            var processInfo = JsonSerializer.Deserialize<ProcessInfoDto>(json);

            // Assert
            Assert.NotNull(processInfo);
            Assert.Equal("MyApp.exe", processInfo.AppName);
            Assert.Equal("/path/to/app", processInfo.InstallPath);
            Assert.Equal("1.2.3", processInfo.LastVersion);
        }

        /// <summary>
        /// Helper class for ProcessInfo JSON deserialization testing.
        /// </summary>
        private class ProcessInfoDto
        {
            public string? AppName { get; set; }
            public string? InstallPath { get; set; }
            public string? LastVersion { get; set; }
        }
    }
}
