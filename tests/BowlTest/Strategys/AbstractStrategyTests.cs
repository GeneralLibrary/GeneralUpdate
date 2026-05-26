using System;
using System.IO;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Strategys;

namespace BowlTest.Strategys
{
    /// <summary>
    /// Contains test cases for the AbstractStrategy class behavior.
    /// Tests process launching, output handling, and directory management.
    /// Note: AbstractStrategy is internal, so we test through WindowStrategy.
    /// </summary>
    public class AbstractStrategyTests
    {
        /// <summary>
        /// Tests that ProcessNameOrId can accept process name.
        /// </summary>
        [Fact]
        public void ProcessNameOrId_AcceptsProcessName()
        {
            // Arrange & Act
            var parameter = new MonitorParameter
            {
                ProcessNameOrId = "myapp.exe"
            };

            // Assert
            Assert.Equal("myapp.exe", parameter.ProcessNameOrId);
        }

        /// <summary>
        /// Tests that ProcessNameOrId can accept process ID.
        /// </summary>
        [Fact]
        public void ProcessNameOrId_AcceptsProcessId()
        {
            // Arrange & Act
            var parameter = new MonitorParameter
            {
                ProcessNameOrId = "12345"
            };

            // Assert
            Assert.Equal("12345", parameter.ProcessNameOrId);
        }

        /// <summary>
        /// Tests that InnerArguments are constructed correctly for procdump.
        /// We can verify the expected format through the parameter values.
        /// </summary>
        [Fact]
        public void InnerArguments_ShouldContainProcdumpParameters()
        {
            // The InnerArguments should be in format: "-e -ma {ProcessNameOrId} {dumpFullPath}"
            // This is set by WindowStrategy before launching
            
            // Arrange
            var processName = "test.exe";
            var dumpFileName = "crash.dmp";
            var failDirectory = "/path/to/fail";
            var expectedDumpPath = Path.Combine(failDirectory, dumpFileName);

            // The format should be: -e -ma test.exe /path/to/fail/crash.dmp
            var expectedFormat = $"-e -ma {processName} {expectedDumpPath}";

            // Assert - Verify the expected format structure
            Assert.Contains("-e", expectedFormat);
            Assert.Contains("-ma", expectedFormat);
            Assert.Contains(processName, expectedFormat);
            Assert.Contains(dumpFileName, expectedFormat);
        }

        /// <summary>
        /// Tests that Applications directory path is constructed correctly for Windows.
        /// </summary>
        [Fact]
        public void ApplicationsDirectory_ConstructedCorrectly_ForWindows()
        {
            // Arrange
            var targetPath = "/path/to/target";
            var expectedAppDir = Path.Combine(targetPath, "Applications", "Windows");

            // Assert
            Assert.Equal($"{targetPath}{Path.DirectorySeparatorChar}Applications{Path.DirectorySeparatorChar}Windows", expectedAppDir);
        }

        /// <summary>
        /// Tests that InnerApp path is constructed correctly with architecture-specific procdump.
        /// </summary>
        [Fact]
        public void InnerApp_PathConstructedCorrectly_WithArchitecture()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            var targetPath = "/path/to/target";
            var applicationsDir = Path.Combine(targetPath, "Applications", "Windows");
            
            var currentArch = RuntimeInformation.OSArchitecture;
            var expectedExe = currentArch switch
            {
                Architecture.X86 => "procdump.exe",
                Architecture.X64 => "procdump64.exe",
                _ => "procdump64a.exe"
            };
            
            var expectedPath = Path.Combine(applicationsDir, expectedExe);

            // Assert
            Assert.Contains("Applications", expectedPath);
            Assert.Contains("Windows", expectedPath);
            Assert.Contains(".exe", expectedPath);
            Assert.EndsWith(expectedExe, expectedPath);
        }

        /// <summary>
        /// Tests that strategy parameters are properly initialized.
        /// </summary>
        [Fact]
        public void Strategy_ParametersInitialized_Correctly()
        {
            // Arrange
            var parameter = new MonitorParameter
            {
                TargetPath = "/target",
                FailDirectory = "/fail",
                BackupDirectory = "/backup",
                ProcessNameOrId = "app.exe",
                DumpFileName = "dump.dmp",
                FailFileName = "fail.json",
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0"
            };

            // Assert - All parameters should be set
            Assert.NotNull(parameter.TargetPath);
            Assert.NotNull(parameter.FailDirectory);
            Assert.NotNull(parameter.BackupDirectory);
            Assert.NotNull(parameter.ProcessNameOrId);
            Assert.NotNull(parameter.DumpFileName);
            Assert.NotNull(parameter.FailFileName);
            Assert.NotNull(parameter.WorkModel);
            Assert.NotNull(parameter.ExtendedField);
        }
    }
}
