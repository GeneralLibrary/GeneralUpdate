using System;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategys;
using GeneralUpdate.Common.Internal.Bootstrap;

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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

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

                var exception = Record.Exception(() => Bowl.Launch(parameter));
                
                // We expect some kind of exception since procdump won't exist
                // but not ArgumentNullException or PlatformNotSupportedException
                Assert.True(exception == null || 
                            (exception is not ArgumentNullException && 
                             exception is not PlatformNotSupportedException));
            }
            finally
            {
                if (System.IO.Directory.Exists(testPath))
                    System.IO.Directory.Delete(testPath, true);
            }
        }

        /// <summary>
        /// Tests that Launch throws PlatformNotSupportedException on unsupported platforms.
        /// </summary>
        [Fact]
        public void Launch_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var parameter = new MonitorParameter
                {
                    TargetPath = "/tmp/test",
                    FailDirectory = "/tmp/test/fail",
                    BackupDirectory = "/tmp/test/backup",
                    ProcessNameOrId = "test",
                    DumpFileName = "test.dmp",
                    FailFileName = "test.json"
                };

                Assert.Throws<PlatformNotSupportedException>(() => Bowl.Launch(parameter));
            }
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo temp-file is empty/missing.
        /// Note: Environments uses temp-file based storage, not system environment variables.
        /// </summary>
        [Fact]
        public void CreateParameter_WithMissingTempFile_ThrowsArgumentNullException()
        {
            // The Environments API reads from %TEMP%/ProcessInfo.txt
            // If the file doesn't exist, GetEnvironmentVariable returns empty string
            // which causes CreateParameter to throw
            var exception = Assert.Throws<ArgumentNullException>(() => Bowl.Launch());
            Assert.Contains("ProcessInfo", exception.Message);
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo JSON is invalid.
        /// </summary>
        [Fact]
        public void CreateParameter_WithInvalidJson_ThrowsException()
        {
            // Set valid-looking but invalid JSON via the correct temp-file API
            Environments.SetEnvironmentVariable("ProcessInfo", "{ invalid json }");

            Assert.ThrowsAny<Exception>(() => Bowl.Launch());
        }

        /// <summary>
        /// Tests that CreateParameter throws when ProcessInfo deserializes to null.
        /// </summary>
        [Fact]
        public void CreateParameter_WithNullDeserialization_ThrowsArgumentNullException()
        {
            Environments.SetEnvironmentVariable("ProcessInfo", "null");

            var exception = Assert.Throws<ArgumentNullException>(() => Bowl.Launch());
            Assert.Contains("ProcessInfo", exception.Message);
        }

        /// <summary>
        /// Tests that Launch with valid ProcessInfo correctly parses the JSON
        /// and does NOT throw ArgumentNullException about ProcessInfo.
        /// </summary>
        [Fact]
        public void Launch_WithValidProcessInfoEnvironment_ParsesCorrectly()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            var tempPath = System.IO.Path.GetTempPath();
            var testPath = System.IO.Path.Combine(tempPath, $"BowlTest_{Guid.NewGuid()}");
            
            var processInfoJson = @"{""AppName"": ""TestApp.exe"", ""InstallPath"": """ + testPath.Replace("\\", "\\\\") + @""", ""LastVersion"": ""1.2.3""}";

            // Use the correct API: Environments stores to temp file
            Environments.SetEnvironmentVariable("ProcessInfo", processInfoJson);

            try
            {
                System.IO.Directory.CreateDirectory(testPath);

                var exception = Record.Exception(() => Bowl.Launch());
                
                // Should NOT get ArgumentNullException about ProcessInfo
                // (It may fail later due to missing procdump, but that's expected)
                Assert.True(exception == null || 
                            exception is not ArgumentNullException ||
                            !exception.Message.Contains("ProcessInfo"));
            }
            finally
            {
                if (System.IO.Directory.Exists(testPath))
                {
                    try { System.IO.Directory.Delete(testPath, true); } catch { }
                }
            }
        }
    }
}
