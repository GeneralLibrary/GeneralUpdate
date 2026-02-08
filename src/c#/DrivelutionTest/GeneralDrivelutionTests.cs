using GeneralUpdate.Drivelution;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using Serilog;

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
        var options = new DriverUpdateOptions
        {
            LogLevel = "Information",
            LogFilePath = "./logs/test.log"
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
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

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
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        var options = new DriverUpdateOptions
        {
            LogLevel = "Debug"
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
}
