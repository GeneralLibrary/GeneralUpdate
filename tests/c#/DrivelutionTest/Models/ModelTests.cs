using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// Tests for DriverInfo model class.
/// Validates driver information structure and properties.
/// </summary>
public class DriverInfoTests
{
    /// <summary>
    /// Tests that DriverInfo can be instantiated with default values.
    /// </summary>
    [Fact]
    public void Constructor_CreatesInstanceWithDefaultValues()
    {
        // Act
        var driverInfo = new DriverInfo();

        // Assert
        Assert.NotNull(driverInfo);
        Assert.Equal(string.Empty, driverInfo.Name);
        Assert.Equal(string.Empty, driverInfo.Version);
        Assert.Equal(string.Empty, driverInfo.FilePath);
        Assert.Equal("SHA256", driverInfo.HashAlgorithm);
        Assert.NotNull(driverInfo.TrustedPublishers);
        Assert.Empty(driverInfo.TrustedPublishers);
        Assert.NotNull(driverInfo.Metadata);
        Assert.Empty(driverInfo.Metadata);
    }

    /// <summary>
    /// Tests that DriverInfo properties can be set and retrieved.
    /// </summary>
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.2.3",
            FilePath = "/path/to/driver.sys",
            TargetOS = "Windows",
            Architecture = "X64",
            HardwareId = "PCI\\VEN_1234&DEV_5678",
            Hash = "abc123",
            HashAlgorithm = "SHA256",
            Description = "Test driver description",
            ReleaseDate = new DateTime(2024, 1, 1)
        };

        // Act & Assert
        Assert.Equal("Test Driver", driverInfo.Name);
        Assert.Equal("1.2.3", driverInfo.Version);
        Assert.Equal("/path/to/driver.sys", driverInfo.FilePath);
        Assert.Equal("Windows", driverInfo.TargetOS);
        Assert.Equal("X64", driverInfo.Architecture);
        Assert.Equal("PCI\\VEN_1234&DEV_5678", driverInfo.HardwareId);
        Assert.Equal("abc123", driverInfo.Hash);
        Assert.Equal("SHA256", driverInfo.HashAlgorithm);
        Assert.Equal("Test driver description", driverInfo.Description);
        Assert.Equal(new DateTime(2024, 1, 1), driverInfo.ReleaseDate);
    }

    /// <summary>
    /// Tests that TrustedPublishers list can be modified.
    /// </summary>
    [Fact]
    public void TrustedPublishers_CanBeModified()
    {
        // Arrange
        var driverInfo = new DriverInfo();

        // Act
        driverInfo.TrustedPublishers.Add("Microsoft Corporation");
        driverInfo.TrustedPublishers.Add("NVIDIA Corporation");

        // Assert
        Assert.Equal(2, driverInfo.TrustedPublishers.Count);
        Assert.Contains("Microsoft Corporation", driverInfo.TrustedPublishers);
        Assert.Contains("NVIDIA Corporation", driverInfo.TrustedPublishers);
    }

    /// <summary>
    /// Tests that Metadata dictionary can be modified.
    /// </summary>
    [Fact]
    public void Metadata_CanBeModified()
    {
        // Arrange
        var driverInfo = new DriverInfo();

        // Act
        driverInfo.Metadata["Author"] = "Test Author";
        driverInfo.Metadata["License"] = "MIT";
        driverInfo.Metadata["Website"] = "https://example.com";

        // Assert
        Assert.Equal(3, driverInfo.Metadata.Count);
        Assert.Equal("Test Author", driverInfo.Metadata["Author"]);
        Assert.Equal("MIT", driverInfo.Metadata["License"]);
        Assert.Equal("https://example.com", driverInfo.Metadata["Website"]);
    }

    /// <summary>
    /// Tests that HashAlgorithm defaults to SHA256.
    /// </summary>
    [Fact]
    public void HashAlgorithm_DefaultsToSHA256()
    {
        // Arrange & Act
        var driverInfo = new DriverInfo();

        // Assert
        Assert.Equal("SHA256", driverInfo.HashAlgorithm);
    }
}

/// <summary>
/// Tests for UpdateStrategy model class.
/// Validates update strategy configuration and properties.
/// </summary>
public class UpdateStrategyTests
{
    /// <summary>
    /// Tests that UpdateStrategy can be instantiated with default values.
    /// </summary>
    [Fact]
    public void Constructor_CreatesInstanceWithDefaultValues()
    {
        // Act
        var strategy = new UpdateStrategy();

        // Assert
        Assert.NotNull(strategy);
        Assert.Equal(UpdateMode.Full, strategy.Mode);
        Assert.False(strategy.ForceUpdate);
        Assert.True(strategy.RequireBackup);
        Assert.Equal(3, strategy.RetryCount);
        Assert.Equal(5, strategy.RetryIntervalSeconds);
        Assert.Equal(0, strategy.Priority);
        Assert.Equal(RestartMode.Prompt, strategy.RestartMode);
        Assert.False(strategy.SkipSignatureValidation);
        Assert.False(strategy.SkipHashValidation);
        Assert.Equal(300, strategy.TimeoutSeconds);
    }

    /// <summary>
    /// Tests that UpdateStrategy properties can be set and retrieved.
    /// </summary>
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var strategy = new UpdateStrategy
        {
            Mode = UpdateMode.Incremental,
            ForceUpdate = true,
            RequireBackup = false,
            BackupPath = "/backup",
            RetryCount = 5,
            RetryIntervalSeconds = 10,
            Priority = 1,
            RestartMode = RestartMode.Immediate,
            SkipSignatureValidation = true,
            SkipHashValidation = true,
            TimeoutSeconds = 600
        };

        // Act & Assert
        Assert.Equal(UpdateMode.Incremental, strategy.Mode);
        Assert.True(strategy.ForceUpdate);
        Assert.False(strategy.RequireBackup);
        Assert.Equal("/backup", strategy.BackupPath);
        Assert.Equal(5, strategy.RetryCount);
        Assert.Equal(10, strategy.RetryIntervalSeconds);
        Assert.Equal(1, strategy.Priority);
        Assert.Equal(RestartMode.Immediate, strategy.RestartMode);
        Assert.True(strategy.SkipSignatureValidation);
        Assert.True(strategy.SkipHashValidation);
        Assert.Equal(600, strategy.TimeoutSeconds);
    }

    /// <summary>
    /// Tests UpdateMode enum values.
    /// </summary>
    [Fact]
    public void UpdateMode_HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)UpdateMode.Full);
        Assert.Equal(1, (int)UpdateMode.Incremental);
    }

    /// <summary>
    /// Tests RestartMode enum values.
    /// </summary>
    [Fact]
    public void RestartMode_HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)RestartMode.None);
        Assert.Equal(1, (int)RestartMode.Prompt);
        Assert.Equal(2, (int)RestartMode.Delayed);
        Assert.Equal(3, (int)RestartMode.Immediate);
    }
}

/// <summary>
/// Tests for UpdateResult model class.
/// Validates update result tracking and properties.
/// </summary>
public class UpdateResultTests
{
    /// <summary>
    /// Tests that UpdateResult can be instantiated.
    /// </summary>
    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var result = new UpdateResult();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(UpdateStatus.NotStarted, result.Status);
        Assert.NotNull(result.StepLogs);
        Assert.Empty(result.StepLogs);
        Assert.Equal(string.Empty, result.Message);
    }

    /// <summary>
    /// Tests that UpdateResult properties can be set and retrieved.
    /// </summary>
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMinutes(5);
        var error = new ErrorInfo
        {
            Code = "TEST_ERROR",
            Message = "Test error message"
        };

        var result = new UpdateResult
        {
            Success = true,
            Status = UpdateStatus.Succeeded,
            Error = error,
            StartTime = startTime,
            EndTime = endTime,
            BackupPath = "/backup/path",
            RolledBack = false,
            Message = "Update completed"
        };

        // Act & Assert
        Assert.True(result.Success);
        Assert.Equal(UpdateStatus.Succeeded, result.Status);
        Assert.Equal(error, result.Error);
        Assert.Equal(startTime, result.StartTime);
        Assert.Equal(endTime, result.EndTime);
        Assert.Equal("/backup/path", result.BackupPath);
        Assert.False(result.RolledBack);
        Assert.Equal("Update completed", result.Message);
    }

    /// <summary>
    /// Tests that DurationMs is calculated correctly.
    /// </summary>
    [Fact]
    public void DurationMs_CalculatesCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddSeconds(30);
        
        var result = new UpdateResult
        {
            StartTime = startTime,
            EndTime = endTime
        };

        // Act
        var duration = result.DurationMs;

        // Assert
        Assert.InRange(duration, 29900, 30100); // Allow 100ms tolerance
    }

    /// <summary>
    /// Tests that StepLogs can be modified.
    /// </summary>
    [Fact]
    public void StepLogs_CanBeModified()
    {
        // Arrange
        var result = new UpdateResult();

        // Act
        result.StepLogs.Add("Step 1: Started");
        result.StepLogs.Add("Step 2: Validation");
        result.StepLogs.Add("Step 3: Completed");

        // Assert
        Assert.Equal(3, result.StepLogs.Count);
        Assert.Contains("Step 1: Started", result.StepLogs);
        Assert.Contains("Step 2: Validation", result.StepLogs);
        Assert.Contains("Step 3: Completed", result.StepLogs);
    }

    /// <summary>
    /// Tests UpdateStatus enum values.
    /// </summary>
    [Fact]
    public void UpdateStatus_HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)UpdateStatus.NotStarted);
        Assert.Equal(1, (int)UpdateStatus.Validating);
        Assert.Equal(2, (int)UpdateStatus.BackingUp);
        Assert.Equal(3, (int)UpdateStatus.Updating);
        Assert.Equal(4, (int)UpdateStatus.Verifying);
        Assert.Equal(5, (int)UpdateStatus.Succeeded);
        Assert.Equal(6, (int)UpdateStatus.Failed);
        Assert.Equal(7, (int)UpdateStatus.RolledBack);
    }
}

/// <summary>
/// Tests for ErrorInfo model class.
/// Validates error information structure.
/// </summary>
public class ErrorInfoTests
{
    /// <summary>
    /// Tests that ErrorInfo can be instantiated.
    /// </summary>
    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var errorInfo = new ErrorInfo();

        // Assert
        Assert.NotNull(errorInfo);
    }

    /// <summary>
    /// Tests that ErrorInfo properties can be set and retrieved.
    /// </summary>
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner exception");
        var errorInfo = new ErrorInfo
        {
            Code = "ERR001",
            Type = ErrorType.InstallationFailed,
            Message = "Installation failed",
            Details = "Detailed error information",
            StackTrace = "Stack trace here",
            InnerException = innerException,
            CanRetry = true,
            SuggestedResolution = "Try again"
        };

        // Act & Assert
        Assert.Equal("ERR001", errorInfo.Code);
        Assert.Equal(ErrorType.InstallationFailed, errorInfo.Type);
        Assert.Equal("Installation failed", errorInfo.Message);
        Assert.Equal("Detailed error information", errorInfo.Details);
        Assert.Equal("Stack trace here", errorInfo.StackTrace);
        Assert.Equal(innerException, errorInfo.InnerException);
        Assert.True(errorInfo.CanRetry);
        Assert.Equal("Try again", errorInfo.SuggestedResolution);
    }
}
