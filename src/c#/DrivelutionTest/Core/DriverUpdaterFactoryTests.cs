using GeneralUpdate.Drivelution.Core;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using Serilog;
using Serilog.Core;

namespace DrivelutionTest.Core;

/// <summary>
/// Tests for DrivelutionFactory class.
/// Validates platform detection, factory creation, and platform-specific implementations.
/// </summary>
public class DrivelutionFactoryTests
{
    /// <summary>
    /// Tests that Create method returns a non-null instance.
    /// </summary>
    [Fact]
    public void Create_WithoutParameters_ReturnsNonNullInstance()
    {
        // Arrange & Act
        var updater = DrivelutionFactory.Create();

        // Assert
        Assert.NotNull(updater);
        Assert.IsAssignableFrom<IGeneralDrivelution>(updater);
    }

    /// <summary>
    /// Tests that Create method accepts custom logger.
    /// </summary>
    [Fact]
    public void Create_WithCustomLogger_ReturnsInstance()
    {
        // Arrange
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Act
        var updater = DrivelutionFactory.Create(logger);

        // Assert
        Assert.NotNull(updater);
        Assert.IsAssignableFrom<IGeneralDrivelution>(updater);
    }

    /// <summary>
    /// Tests that Create method accepts custom options.
    /// </summary>
    [Fact]
    public void Create_WithCustomOptions_ReturnsInstance()
    {
        // Arrange
        var options = new DrivelutionOptions
        {
            LogLevel = "Debug",
            LogFilePath = "./logs/test.log"
        };

        // Act
        var updater = DrivelutionFactory.Create(null, options);

        // Assert
        Assert.NotNull(updater);
    }

    /// <summary>
    /// Tests that GetCurrentPlatform returns a valid platform name.
    /// </summary>
    [Fact]
    public void GetCurrentPlatform_ReturnsValidPlatformName()
    {
        // Act
        var platform = DrivelutionFactory.GetCurrentPlatform();

        // Assert
        Assert.NotNull(platform);
        Assert.Contains(platform, new[] { "Windows", "Linux", "MacOS", "Unknown" });
    }

    /// <summary>
    /// Tests that IsPlatformSupported returns a boolean value.
    /// </summary>
    [Fact]
    public void IsPlatformSupported_ReturnsBooleanValue()
    {
        // Act
        var isSupported = DrivelutionFactory.IsPlatformSupported();

        // Assert
        // Windows and Linux should be supported
        Assert.True(isSupported || DrivelutionFactory.GetCurrentPlatform() == "MacOS" || DrivelutionFactory.GetCurrentPlatform() == "Unknown");
    }

    /// <summary>
    /// Tests that CreateValidator returns a non-null instance.
    /// </summary>
    [Fact]
    public void CreateValidator_WithoutLogger_ReturnsNonNullInstance()
    {
        // Skip on MacOS and Unknown platforms
        var platform = DrivelutionFactory.GetCurrentPlatform();
        if (platform == "MacOS" || platform == "Unknown")
        {
            return;
        }

        // Act
        var validator = DrivelutionFactory.CreateValidator();

        // Assert
        Assert.NotNull(validator);
        Assert.IsAssignableFrom<IDriverValidator>(validator);
    }

    /// <summary>
    /// Tests that CreateBackup returns a non-null instance.
    /// </summary>
    [Fact]
    public void CreateBackup_WithoutLogger_ReturnsNonNullInstance()
    {
        // Skip on MacOS and Unknown platforms
        var platform = DrivelutionFactory.GetCurrentPlatform();
        if (platform == "MacOS" || platform == "Unknown")
        {
            return;
        }

        // Act
        var backup = DrivelutionFactory.CreateBackup();

        // Assert
        Assert.NotNull(backup);
        Assert.IsAssignableFrom<IDriverBackup>(backup);
    }

    /// <summary>
    /// Tests that CreateValidator with custom logger works correctly.
    /// </summary>
    [Fact]
    public void CreateValidator_WithCustomLogger_ReturnsInstance()
    {
        // Skip on MacOS and Unknown platforms
        var platform = DrivelutionFactory.GetCurrentPlatform();
        if (platform == "MacOS" || platform == "Unknown")
        {
            return;
        }

        // Arrange
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Act
        var validator = DrivelutionFactory.CreateValidator(logger);

        // Assert
        Assert.NotNull(validator);
    }

    /// <summary>
    /// Tests that CreateBackup with custom logger works correctly.
    /// </summary>
    [Fact]
    public void CreateBackup_WithCustomLogger_ReturnsInstance()
    {
        // Skip on MacOS and Unknown platforms
        var platform = DrivelutionFactory.GetCurrentPlatform();
        if (platform == "MacOS" || platform == "Unknown")
        {
            return;
        }

        // Arrange
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Act
        var backup = DrivelutionFactory.CreateBackup(logger);

        // Assert
        Assert.NotNull(backup);
    }

    /// <summary>
    /// Tests that Create throws PlatformNotSupportedException on unsupported platforms.
    /// This test documents expected behavior but cannot be easily tested on supported platforms.
    /// </summary>
    [Fact]
    public void Create_OnSupportedPlatform_DoesNotThrow()
    {
        // Skip on MacOS as it's not yet implemented
        var platform = DrivelutionFactory.GetCurrentPlatform();
        
        if (platform == "MacOS")
        {
            // MacOS should throw PlatformNotSupportedException
            Assert.Throws<PlatformNotSupportedException>(() => DrivelutionFactory.Create());
            return;
        }

        // Act & Assert - should not throw on Windows/Linux
        var exception = Record.Exception(() => DrivelutionFactory.Create());
        Assert.Null(exception);
    }
}
