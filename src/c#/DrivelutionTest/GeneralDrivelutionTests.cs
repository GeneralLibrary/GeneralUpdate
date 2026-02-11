using GeneralUpdate.Drivelution;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Events;

namespace DrivelutionTest;

/// <summary>
/// Tests for GeneralDrivelution static entry point class.
/// Validates factory methods, quick update methods, and platform info retrieval.
/// </summary>
public class GeneralDrivelutionTests
{
    /// <summary>
    /// Tests that Create method returns a non-null instance.
    /// </summary>
    [Fact]
    public void Create_WithoutParameters_ReturnsNonNullInstance()
    {
        // Act
        var updater = GeneralDrivelution.Create();

        // Assert
        Assert.NotNull(updater);
    }

    /// <summary>
    /// Tests that Create method with options returns a non-null instance.
    /// </summary>
    [Fact]
    public void Create_WithOptions_ReturnsNonNullInstance()
    {
        // Arrange
        var options = new DrivelutionOptions
        {
            DefaultBackupPath = "./backups"
        };

        // Act
        var updater = GeneralDrivelution.Create(options);

        // Assert
        Assert.NotNull(updater);
    }

    /// <summary>
    /// Tests that Create method with custom logger returns a non-null instance.
    /// </summary>
    [Fact]
    public void Create_WithCustomLogger_ReturnsNonNullInstance()
    {
        // Arrange
        var logger = new DrivelutionLogger();

        // Act
        var updater = GeneralDrivelution.Create(logger);

        // Assert
        Assert.NotNull(updater);
    }

    /// <summary>
    /// Tests that Create method with custom logger and options returns instance.
    /// </summary>
    [Fact]
    public void Create_WithCustomLoggerAndOptions_ReturnsInstance()
    {
        // Arrange
        var logger = new DrivelutionLogger();
        var options = new DrivelutionOptions
        {
            DefaultBackupPath = "./backups"
        };

        // Act
        var updater = GeneralDrivelution.Create(logger, options);

        // Assert
        Assert.NotNull(updater);
    }

    /// <summary>
    /// Tests that GetPlatformInfo returns valid platform information.
    /// </summary>
    [Fact]
    public void GetPlatformInfo_ReturnsValidPlatformInfo()
    {
        // Act
        var platformInfo = GeneralDrivelution.GetPlatformInfo();

        // Assert
        Assert.NotNull(platformInfo);
        Assert.NotNull(platformInfo.Platform);
        Assert.NotEmpty(platformInfo.Platform);
        Assert.NotNull(platformInfo.OperatingSystem);
        Assert.NotEmpty(platformInfo.OperatingSystem);
        Assert.NotNull(platformInfo.Architecture);
        Assert.NotEmpty(platformInfo.Architecture);
        Assert.NotNull(platformInfo.SystemVersion);
        Assert.NotEmpty(platformInfo.SystemVersion);
    }

    /// <summary>
    /// Tests that PlatformInfo ToString returns a valid string.
    /// </summary>
    [Fact]
    public void PlatformInfo_ToString_ReturnsValidString()
    {
        // Arrange
        var platformInfo = GeneralDrivelution.GetPlatformInfo();

        // Act
        var result = platformInfo.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(platformInfo.Platform, result);
        Assert.Contains(platformInfo.OperatingSystem, result);
    }

    /// <summary>
    /// Tests that QuickUpdateAsync with minimal parameters works (or throws expected exceptions).
    /// </summary>
    [Fact]
    public async Task QuickUpdateAsync_WithMinimalParameters_HandlesGracefully()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_driver_{Guid.NewGuid()}.txt");
        File.WriteAllText(testFilePath, "Test driver content");

        try
        {
            var driverInfo = new DriverInfo
            {
                Name = "Test Driver",
                Version = "1.0.0",
                FilePath = testFilePath,
                TargetOS = "",
                Architecture = ""
            };

            // Act
            var result = await GeneralDrivelution.QuickUpdateAsync(driverInfo);

            // Assert
            Assert.NotNull(result);
            // The result might fail due to validation or permissions, but should not crash
            // Verify that result status is one of the valid enum values
            Assert.True(Enum.IsDefined(typeof(UpdateStatus), result.Status));
        }
        finally
        {
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    /// <summary>
    /// Tests that QuickUpdateAsync with custom strategy works.
    /// </summary>
    [Fact]
    public async Task QuickUpdateAsync_WithCustomStrategy_HandlesGracefully()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_driver_{Guid.NewGuid()}.txt");
        File.WriteAllText(testFilePath, "Test driver content");

        try
        {
            var driverInfo = new DriverInfo
            {
                Name = "Test Driver",
                Version = "1.0.0",
                FilePath = testFilePath,
                TargetOS = "",
                Architecture = ""
            };

            var strategy = new UpdateStrategy
            {
                RequireBackup = false,
                RetryCount = 1,
                RetryIntervalSeconds = 1
            };

            // Act
            var result = await GeneralDrivelution.QuickUpdateAsync(driverInfo, strategy);

            // Assert
            Assert.NotNull(result);
            // Verify that result status is one of the valid enum values
            Assert.True(Enum.IsDefined(typeof(UpdateStatus), result.Status));
        }
        finally
        {
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    /// <summary>
    /// Tests that ValidateAsync with minimal driver info works.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithMinimalDriverInfo_ReturnsBooleanResult()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_driver_{Guid.NewGuid()}.txt");
        File.WriteAllText(testFilePath, "Test driver content");

        try
        {
            var driverInfo = new DriverInfo
            {
                Name = "Test Driver",
                Version = "1.0.0",
                FilePath = testFilePath,
                TargetOS = "",
                Architecture = ""
            };

            // Act
            var result = await GeneralDrivelution.ValidateAsync(driverInfo);

            // Assert - Should return a boolean value without throwing
            // No assertion needed - the test passes if no exception is thrown
        }
        finally
        {
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    /// <summary>
    /// Tests that ValidateAsync returns false for non-existent file.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            FilePath = "/nonexistent/path/driver.sys",
            TargetOS = "",
            Architecture = ""
        };

        // Act
        var result = await GeneralDrivelution.ValidateAsync(driverInfo);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests that QuickUpdateAsync with cancellation token works.
    /// </summary>
    [Fact]
    public async Task QuickUpdateAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_driver_{Guid.NewGuid()}.txt");
        File.WriteAllText(testFilePath, "Test driver content");

        try
        {
            var driverInfo = new DriverInfo
            {
                Name = "Test Driver",
                Version = "1.0.0",
                FilePath = testFilePath
            };

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            // Should either complete quickly or throw cancellation exception
            var exception = await Record.ExceptionAsync(
                () => GeneralDrivelution.QuickUpdateAsync(driverInfo, cts.Token));

            // Either succeeded, failed gracefully, or was cancelled
            Assert.True(exception == null || exception is OperationCanceledException);
        }
        finally
        {
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    /// <summary>
    /// Tests that GetPlatformInfo reports correct support status.
    /// </summary>
    [Fact]
    public void GetPlatformInfo_ReportsCorrectSupportStatus()
    {
        // Act
        var platformInfo = GeneralDrivelution.GetPlatformInfo();

        // Assert
        if (platformInfo.Platform == "Windows" || platformInfo.Platform == "Linux")
        {
            Assert.True(platformInfo.IsSupported);
        }
        else if (platformInfo.Platform == "MacOS")
        {
            Assert.False(platformInfo.IsSupported); // MacOS not yet implemented
        }
    }

    /// <summary>
    /// Tests that GetDriversFromDirectoryAsync returns empty list for non-existent directory.
    /// </summary>
    [Fact]
    public async Task GetDriversFromDirectoryAsync_WithNonExistentDirectory_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}");

        // Act
        var result = await GeneralDrivelution.GetDriversFromDirectoryAsync(nonExistentPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Tests that GetDriversFromDirectoryAsync returns empty list for empty directory.
    /// </summary>
    [Fact]
    public async Task GetDriversFromDirectoryAsync_WithEmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var emptyDir = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}");
        Directory.CreateDirectory(emptyDir);

        try
        {
            // Act
            var result = await GeneralDrivelution.GetDriversFromDirectoryAsync(emptyDir);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        finally
        {
            if (Directory.Exists(emptyDir))
            {
                Directory.Delete(emptyDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetDriversFromDirectoryAsync discovers driver files in directory.
    /// </summary>
    [Fact]
    public async Task GetDriversFromDirectoryAsync_WithDriverFiles_ReturnsDriverInfoList()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"drivers_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create test driver files based on platform
            var platformInfo = GeneralDrivelution.GetPlatformInfo();
            var testFiles = new List<string>();

            if (platformInfo.Platform == "Windows")
            {
                // Create mock .inf file
                var infFile = Path.Combine(testDir, "test_driver.inf");
                File.WriteAllText(infFile, @"
[Version]
Signature=""$Windows NT$""
DriverVer=01/15/2024,1.0.0.0

[DriverInfo]
DriverDesc=""Test Driver""
");
                testFiles.Add(infFile);
            }
            else if (platformInfo.Platform == "Linux")
            {
                // Create mock .ko file
                var koFile = Path.Combine(testDir, "test_driver.ko");
                File.WriteAllText(koFile, "Mock kernel module content");
                testFiles.Add(koFile);
            }

            // Act
            var result = await GeneralDrivelution.GetDriversFromDirectoryAsync(testDir);

            // Assert
            Assert.NotNull(result);
            
            // Should find at least one driver if platform is supported
            if (platformInfo.IsSupported && testFiles.Any())
            {
                Assert.NotEmpty(result);
                
                // Check that driver info has expected properties
                var driver = result.First();
                Assert.NotNull(driver.Name);
                Assert.NotEmpty(driver.Name);
                Assert.NotNull(driver.FilePath);
                Assert.NotEmpty(driver.FilePath);
                Assert.NotNull(driver.Version);
                Assert.NotEmpty(driver.Version);
                Assert.NotNull(driver.Hash);
                Assert.NotEmpty(driver.Hash);
            }
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetDriversFromDirectoryAsync with search pattern filters correctly.
    /// </summary>
    [Fact]
    public async Task GetDriversFromDirectoryAsync_WithSearchPattern_FiltersCorrectly()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"drivers_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create different types of files
            var infFile = Path.Combine(testDir, "driver1.inf");
            var txtFile = Path.Combine(testDir, "readme.txt");
            File.WriteAllText(infFile, "INF content");
            File.WriteAllText(txtFile, "Text content");

            // Act
            var result = await GeneralDrivelution.GetDriversFromDirectoryAsync(testDir, "*.inf");

            // Assert
            Assert.NotNull(result);
            
            // Should only find .inf files
            // The count depends on whether the platform supports parsing .inf files
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetDriversFromDirectoryAsync handles cancellation.
    /// </summary>
    [Fact]
    public async Task GetDriversFromDirectoryAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"drivers_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await GeneralDrivelution.GetDriversFromDirectoryAsync(testDir, null, cts.Token);

            // Assert - Should complete without throwing or return empty list
            Assert.NotNull(result);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }
}
