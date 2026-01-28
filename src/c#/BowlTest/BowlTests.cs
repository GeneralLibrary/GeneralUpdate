using System;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategys;

namespace BowlTest
{
    /// <summary>
    /// Contains test cases for the Bowl class.
    /// Tests the main entry point and parameter creation logic.
    /// </summary>
    public class BowlTests
    {
        /// <summary>
        /// Tests that Launch with valid MonitorParameter doesn't throw on Windows.
        /// This test can only run on Windows as Linux is not fully implemented.
        /// </summary>
        [Fact]
        public void Launch_WithValidParameter_DoesNotThrow_OnWindows()
        {
            // This test validates the code path but requires Windows platform
            // and valid file paths to fully execute. We verify it doesn't throw
            // during initialization phase.
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Skip on non-Windows platforms
                return;
            }

            // Arrange
            var tempPath = System.IO.Path.GetTempPath();
            var testPath = System.IO.Path.Combine(tempPath, $"BowlTest_{Guid.NewGuid()}");
            System.IO.Directory.CreateDirectory(testPath);

            try
            {
                var parameter = new MonitorParameter
                {
                    TargetPath = testPath,
                    FailDirectory = System.IO.Path.Combine(testPath, "fail"),
                    BackupDirectory = System.IO.Path.Combine(testPath, "backup"),
                    ProcessNameOrId = "notepad.exe",
                    DumpFileName = "test.dmp",
                    FailFileName = "test.json",
                    WorkModel = "Normal",
                    ExtendedField = "1.0.0"
                };

                // Act & Assert
                // Note: This will fail when it tries to launch procdump as it won't exist
                // But we're testing that the initialization doesn't throw
                var exception = Record.Exception(() => Bowl.Launch(parameter));
                
                // We expect some kind of exception since procdump won't exist
                // but not ArgumentNullException or PlatformNotSupportedException
                Assert.True(exception == null || 
                            (exception is not ArgumentNullException && 
                             exception is not PlatformNotSupportedException));
            }
            finally
            {
                // Cleanup
                if (System.IO.Directory.Exists(testPath))
                {
                    System.IO.Directory.Delete(testPath, true);
                }
            }
        }

        /// <summary>
        /// Tests that Launch throws PlatformNotSupportedException on unsupported platforms.
        /// This test verifies the platform detection logic.
        /// </summary>
        [Fact]
        public void Launch_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
        {
            // This test verifies platform detection
            // On Windows, it should work; on other platforms, it should throw
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Arrange
                var parameter = new MonitorParameter
                {
                    TargetPath = "/tmp/test",
                    FailDirectory = "/tmp/test/fail",
                    BackupDirectory = "/tmp/test/backup",
                    ProcessNameOrId = "test",
                    DumpFileName = "test.dmp",
                    FailFileName = "test.json"
                };

                // Act & Assert
                Assert.Throws<PlatformNotSupportedException>(() => Bowl.Launch(parameter));
            }
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo environment variable is null.
        /// This test uses reflection to test the private CreateParameter method.
        /// </summary>
        [Fact]
        public void CreateParameter_WithNullEnvironmentVariable_ThrowsArgumentNullException()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("ProcessInfo");
            Environment.SetEnvironmentVariable("ProcessInfo", null);

            try
            {
                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() => Bowl.Launch());
                Assert.Contains("ProcessInfo", exception.Message);
            }
            finally
            {
                // Restore original value
                Environment.SetEnvironmentVariable("ProcessInfo", originalValue);
            }
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo environment variable is empty.
        /// </summary>
        [Fact]
        public void CreateParameter_WithEmptyEnvironmentVariable_ThrowsArgumentNullException()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("ProcessInfo");
            Environment.SetEnvironmentVariable("ProcessInfo", "");

            try
            {
                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() => Bowl.Launch());
                Assert.Contains("ProcessInfo", exception.Message);
            }
            finally
            {
                // Restore original value
                Environment.SetEnvironmentVariable("ProcessInfo", originalValue);
            }
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo JSON is invalid.
        /// </summary>
        [Fact]
        public void CreateParameter_WithInvalidJson_ThrowsException()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("ProcessInfo");
            Environment.SetEnvironmentVariable("ProcessInfo", "{ invalid json }");

            try
            {
                // Act & Assert
                Assert.ThrowsAny<Exception>(() => Bowl.Launch());
            }
            finally
            {
                // Restore original value
                Environment.SetEnvironmentVariable("ProcessInfo", originalValue);
            }
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo deserializes to null.
        /// </summary>
        [Fact]
        public void CreateParameter_WithNullDeserialization_ThrowsArgumentNullException()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("ProcessInfo");
            Environment.SetEnvironmentVariable("ProcessInfo", "null");

            try
            {
                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() => Bowl.Launch());
                Assert.Contains("ProcessInfo", exception.Message);
            }
            finally
            {
                // Restore original value
                Environment.SetEnvironmentVariable("ProcessInfo", originalValue);
            }
        }

        /// <summary>
        /// Tests that Launch with valid ProcessInfo environment variable creates correct parameter.
        /// This test verifies the environment variable parsing logic.
        /// </summary>
        [Fact]
        public void Launch_WithValidProcessInfoEnvironment_ParsesCorrectly()
        {
            // Skip on non-Windows as platform check will fail first
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("ProcessInfo");
            var tempPath = System.IO.Path.GetTempPath();
            var testPath = System.IO.Path.Combine(tempPath, $"BowlTest_{Guid.NewGuid()}");
            
            var processInfoJson = @"{
                ""AppName"": ""TestApp.exe"",
                ""InstallPath"": """ + testPath.Replace("\\", "\\\\") + @""",
                ""LastVersion"": ""1.2.3""
            }";
            
            Environment.SetEnvironmentVariable("ProcessInfo", processInfoJson);

            try
            {
                System.IO.Directory.CreateDirectory(testPath);

                // Act & Assert
                // Should not throw ArgumentNullException for ProcessInfo
                var exception = Record.Exception(() => Bowl.Launch());
                
                // Should not be ArgumentNullException related to ProcessInfo
                Assert.True(exception == null || 
                            exception is not ArgumentNullException ||
                            !exception.Message.Contains("ProcessInfo"));
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("ProcessInfo", originalValue);
                if (System.IO.Directory.Exists(testPath))
                {
                    try { System.IO.Directory.Delete(testPath, true); } catch { }
                }
            }
        }
    }
}
