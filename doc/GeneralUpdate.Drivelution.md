# GeneralUpdate.Drivelution

A cross-platform driver update library for .NET 8.0+ with comprehensive validation, backup, and rollback capabilities.

## Overview

**GeneralUpdate.Drivelution** is a robust, production-ready library designed to simplify and secure driver update operations across Windows and Linux platforms. It provides automatic platform detection, comprehensive validation (hash, signature, compatibility), backup and rollback mechanisms, and enterprise-grade error handling.

### Key Features

- ✅ **Cross-Platform Support**: Windows (8+) and Linux (Ubuntu 18.04+, CentOS 7+, Debian 10+)
- ✅ **Automatic Platform Detection**: Automatically selects the appropriate driver updater based on the runtime platform
- ✅ **Comprehensive Validation**: 
  - Hash validation (SHA256, MD5)
  - Digital signature verification (Windows Authenticode, Linux GPG)
  - Compatibility checking (OS, architecture, hardware ID)
- ✅ **Backup & Rollback**: Automatic backup creation with rollback on failure
- ✅ **Permission Management**: Automatic privilege elevation checks (Administrator/sudo)
- ✅ **Flexible Update Strategies**: Full/incremental updates with customizable retry logic
- ✅ **Detailed Logging**: Built-in Serilog integration with configurable log levels
- ✅ **AOT Compatible**: Fully compatible with Native AOT compilation
- ✅ **Thread-Safe**: Designed for concurrent operations with proper cancellation support

## Installation

```bash
# Install via NuGet (when published)
dotnet add package GeneralUpdate.Drivelution

# Or add to your .csproj
<PackageReference Include="GeneralUpdate.Drivelution" Version="1.0.0" />
```

## Quick Start

### Basic Usage

```csharp
using GeneralUpdate.Drivelution;
using GeneralUpdate.Drivelution.Abstractions.Models;

// Create driver information
var driverInfo = new DriverInfo
{
    Name = "MyDevice Driver",
    Version = "2.1.0",
    FilePath = @"C:\Drivers\mydevice-v2.1.0.sys",
    TargetOS = "Windows",
    Architecture = "x64",
    HardwareId = "PCI\\VEN_8086&DEV_15B8",
    Hash = "3a5f6c8d9e2b1a4f7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b",
    HashAlgorithm = "SHA256"
};

// Quick update with default settings
var result = await GeneralDrivelution.QuickUpdateAsync(driverInfo);

if (result.Success)
{
    Console.WriteLine($"Driver updated successfully in {result.DurationMs}ms");
}
else
{
    Console.WriteLine($"Update failed: {result.Error?.Message}");
}
```

### Advanced Usage with Custom Configuration

```csharp
using GeneralUpdate.Drivelution;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Models;

// Configure options
var options = new DriverUpdateOptions
{
    LogLevel = "Debug",
    LogFilePath = "./Logs/driver-update-.log",
    EnableConsoleLogging = true,
    EnableFileLogging = true,
    DefaultBackupPath = "./Backups",
    DefaultRetryCount = 5,
    DefaultRetryIntervalSeconds = 10,
    AutoCleanupBackups = true,
    BackupsToKeep = 3
};

// Create updater instance with custom configuration
var updater = GeneralDrivelution.Create(options);

// Define update strategy
var strategy = new UpdateStrategy
{
    Mode = UpdateMode.Full,
    ForceUpdate = false,
    RequireBackup = true,
    BackupPath = @"C:\DriverBackups\MyDevice",
    RetryCount = 3,
    RetryIntervalSeconds = 5,
    TimeoutSeconds = 600,
    RestartMode = RestartMode.Prompt,
    SkipSignatureValidation = false, // Only set true in development
    SkipHashValidation = false        // Only set true in development
};

// Prepare driver info
var driverInfo = new DriverInfo
{
    Name = "High-Performance Network Adapter",
    Version = "3.2.1",
    FilePath = @"C:\Drivers\network-adapter-v3.2.1.sys",
    TargetOS = "Windows",
    Architecture = "x64",
    HardwareId = "PCI\\VEN_8086&DEV_15B8&SUBSYS_12345678",
    Hash = "a1b2c3d4e5f6789...",
    HashAlgorithm = "SHA256",
    Description = "High-Performance 10Gb Ethernet Adapter Driver",
    ReleaseDate = DateTime.Parse("2026-01-15"),
    TrustedPublishers = new List<string> 
    { 
        "CN=MyCompany Inc., O=MyCompany Inc., L=City, S=State, C=US" 
    },
    Metadata = new Dictionary<string, string>
    {
        { "Vendor", "MyCompany" },
        { "Category", "Network" },
        { "MinOSVersion", "10.0.19041" }
    }
};

// Execute update
var result = await updater.UpdateAsync(driverInfo, strategy, cancellationToken);

// Handle result
if (result.Success)
{
    Console.WriteLine($"✓ Update succeeded in {result.DurationMs}ms");
    Console.WriteLine($"Status: {result.Status}");
    
    if (!string.IsNullOrEmpty(result.BackupPath))
    {
        Console.WriteLine($"Backup: {result.BackupPath}");
    }
    
    // Display step logs
    foreach (var log in result.StepLogs)
    {
        Console.WriteLine(log);
    }
}
else
{
    Console.WriteLine($"✗ Update failed: {result.Error?.Message}");
    Console.WriteLine($"Error Type: {result.Error?.Type}");
    Console.WriteLine($"Can Retry: {result.Error?.CanRetry}");
    
    if (result.RolledBack)
    {
        Console.WriteLine("✓ Successfully rolled back to previous version");
    }
}
```

## Core Concepts

### Driver Information Model

The `DriverInfo` class encapsulates all necessary information about a driver:

```csharp
public class DriverInfo
{
    public string Name { get; set; }              // Driver name
    public string Version { get; set; }           // SemVer 2.0 version
    public string FilePath { get; set; }          // Local or network path
    public string TargetOS { get; set; }          // "Windows", "Linux"
    public string Architecture { get; set; }      // "x86", "x64", "ARM", "ARM64"
    public string HardwareId { get; set; }        // Device hardware ID
    public string Hash { get; set; }              // File hash for integrity
    public string HashAlgorithm { get; set; }     // "SHA256", "MD5"
    public List<string> TrustedPublishers { get; set; }
    public string Description { get; set; }
    public DateTime ReleaseDate { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
```

### Update Strategy

The `UpdateStrategy` class defines how the update should be performed:

```csharp
public class UpdateStrategy
{
    public UpdateMode Mode { get; set; }           // Full or Incremental
    public bool ForceUpdate { get; set; }          // Cannot be cancelled
    public bool RequireBackup { get; set; }        // Create backup before update
    public string BackupPath { get; set; }         // Custom backup location
    public int RetryCount { get; set; }            // Number of retries on failure
    public int RetryIntervalSeconds { get; set; }  // Delay between retries
    public int Priority { get; set; }              // For batch updates
    public RestartMode RestartMode { get; set; }   // None, Prompt, Delayed, Immediate
    public bool SkipSignatureValidation { get; set; }  // Debug only
    public bool SkipHashValidation { get; set; }       // Debug only
    public int TimeoutSeconds { get; set; }        // Operation timeout
}
```

### Update Result

The `UpdateResult` class provides detailed information about the update operation:

```csharp
public class UpdateResult
{
    public bool Success { get; set; }              // Overall success status
    public UpdateStatus Status { get; set; }       // Current status
    public ErrorInfo? Error { get; set; }          // Error details if failed
    public DateTime StartTime { get; set; }        // Start timestamp
    public DateTime EndTime { get; set; }          // End timestamp
    public long DurationMs { get; }                // Duration in milliseconds
    public string? BackupPath { get; set; }        // Backup location if created
    public bool RolledBack { get; set; }           // Whether rollback occurred
    public string Message { get; set; }            // Human-readable message
    public List<string> StepLogs { get; set; }     // Detailed step-by-step logs
}
```

## Platform-Specific Features

### Windows Platform

#### Digital Signature Validation
```csharp
// Windows drivers must be signed by a trusted publisher
var driverInfo = new DriverInfo
{
    FilePath = @"C:\Drivers\device.sys",
    TrustedPublishers = new List<string>
    {
        "CN=Microsoft Windows Hardware Compatibility Publisher"
    }
};
```

#### Hardware ID Validation
```csharp
// Windows uses PnP hardware IDs
var driverInfo = new DriverInfo
{
    HardwareId = "PCI\\VEN_8086&DEV_15B8&SUBSYS_12345678&REV_01"
};
```

#### Administrator Privileges
Windows driver updates automatically check for administrator privileges and will throw a `DriverPermissionException` if not running as administrator.

### Linux Platform

#### Kernel Module Installation
```csharp
// Linux drivers are typically kernel modules (.ko files)
var driverInfo = new DriverInfo
{
    FilePath = "/path/to/driver.ko",
    TargetOS = "Linux",
    Architecture = "x64"
};
```

#### GPG Signature Validation
```csharp
// Linux drivers can be validated using GPG signatures
var options = new DriverUpdateOptions
{
    TrustedGpgKeys = new List<string>
    {
        "1234567890ABCDEF1234567890ABCDEF12345678"
    }
};
```

#### Sudo Privileges
Linux driver updates automatically ensure sudo access and will request elevation when needed.

## Validation Features

### Hash Validation

Ensures file integrity by comparing computed hash with expected hash:

```csharp
// Validate driver file integrity before installation
var isValid = await updater.ValidateAsync(driverInfo);

if (!isValid)
{
    Console.WriteLine("Driver file is corrupted or tampered with!");
}
```

Supported algorithms:
- **SHA256** (recommended)
- **MD5** (legacy support)

### Signature Validation

Verifies digital signatures to ensure driver authenticity:

**Windows**: Authenticode digital signatures
**Linux**: GPG signature verification

```csharp
var strategy = new UpdateStrategy
{
    SkipSignatureValidation = false  // Enable signature validation
};
```

### Compatibility Validation

Checks driver compatibility with target system:

```csharp
var driverInfo = new DriverInfo
{
    TargetOS = "Windows",
    Architecture = "x64",
    Metadata = new Dictionary<string, string>
    {
        { "MinOSVersion", "10.0.19041" }  // Minimum Windows version
    }
};
```

## Backup and Rollback

### Automatic Backup

```csharp
var strategy = new UpdateStrategy
{
    RequireBackup = true,
    BackupPath = @"C:\DriverBackups\MyDevice_2026-02-08"
};

var result = await updater.UpdateAsync(driverInfo, strategy);

if (!string.IsNullOrEmpty(result.BackupPath))
{
    Console.WriteLine($"Backup created at: {result.BackupPath}");
}
```

### Manual Backup

```csharp
var backupPath = @"C:\DriverBackups\Manual_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
var success = await updater.BackupAsync(driverInfo, backupPath);

if (success)
{
    Console.WriteLine($"Manual backup created: {backupPath}");
}
```

### Rollback on Failure

Automatic rollback is triggered when installation fails:

```csharp
var result = await updater.UpdateAsync(driverInfo, strategy);

if (result.RolledBack)
{
    Console.WriteLine("Installation failed but successfully rolled back");
    Console.WriteLine($"Previous driver restored from: {result.BackupPath}");
}
```

### Manual Rollback

```csharp
var backupPath = @"C:\DriverBackups\MyDevice_20260208";
var success = await updater.RollbackAsync(backupPath);

if (success)
{
    Console.WriteLine("Successfully restored from backup");
}
```

## Error Handling

### Exception Types

The library provides specific exception types for different failure scenarios:

```csharp
try
{
    var result = await updater.UpdateAsync(driverInfo, strategy);
}
catch (DriverPermissionException ex)
{
    // Insufficient permissions (not admin/sudo)
    Console.WriteLine($"Permission denied: {ex.Message}");
    Console.WriteLine("Please run as administrator/sudo");
}
catch (DriverValidationException ex)
{
    // Validation failed (hash, signature, compatibility)
    Console.WriteLine($"Validation failed: {ex.Message}");
    Console.WriteLine($"Validation type: {ex.ValidationType}");
}
catch (DriverInstallationException ex)
{
    // Installation failed
    Console.WriteLine($"Installation failed: {ex.Message}");
    Console.WriteLine($"Can retry: {ex.CanRetry}");
    Console.WriteLine($"Error code: {ex.ErrorCode}");
}
catch (DriverBackupException ex)
{
    // Backup operation failed
    Console.WriteLine($"Backup failed: {ex.Message}");
}
catch (DriverRollbackException ex)
{
    // Rollback operation failed
    Console.WriteLine($"Rollback failed: {ex.Message}");
}
```

### Error Information

Detailed error information is available in the result:

```csharp
if (!result.Success && result.Error != null)
{
    Console.WriteLine($"Error Type: {result.Error.Type}");
    Console.WriteLine($"Message: {result.Error.Message}");
    Console.WriteLine($"Details: {result.Error.Details}");
    Console.WriteLine($"Can Retry: {result.Error.CanRetry}");
    Console.WriteLine($"Error Code: {result.Error.ErrorCode}");
    Console.WriteLine($"Timestamp: {result.Error.Timestamp}");
}
```

### Error Types

```csharp
public enum ErrorType
{
    PermissionDenied,          // Insufficient privileges
    HashValidationFailed,       // Hash mismatch
    SignatureValidationFailed,  // Invalid signature
    CompatibilityCheckFailed,   // Incompatible driver
    InstallationFailed,         // Installation error
    BackupFailed,              // Backup creation failed
    RollbackFailed,            // Rollback failed
    NetworkError,              // Download/network issue
    TimeoutError,              // Operation timed out
    UnknownError               // Unexpected error
}
```

## Retry Logic

### Automatic Retry on Failure

```csharp
var strategy = new UpdateStrategy
{
    RetryCount = 5,                  // Retry up to 5 times
    RetryIntervalSeconds = 10,       // Wait 10 seconds between retries
    TimeoutSeconds = 600             // 10 minute timeout per attempt
};

var result = await updater.UpdateAsync(driverInfo, strategy);

// Retries are logged in step logs
foreach (var log in result.StepLogs)
{
    Console.WriteLine(log);
}
```

### Manual Retry

```csharp
int maxAttempts = 3;
UpdateResult? result = null;

for (int attempt = 1; attempt <= maxAttempts; attempt++)
{
    Console.WriteLine($"Attempt {attempt}/{maxAttempts}");
    
    result = await updater.UpdateAsync(driverInfo, strategy);
    
    if (result.Success)
    {
        break;
    }
    
    if (result.Error?.CanRetry == false)
    {
        Console.WriteLine("Error is not retryable, aborting");
        break;
    }
    
    if (attempt < maxAttempts)
    {
        Console.WriteLine($"Waiting 10 seconds before retry...");
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}
```

## Logging

### Default Logging Configuration

```csharp
// Default logger writes to console and file
var updater = GeneralDrivelution.Create();
```

### Custom Logging Configuration

```csharp
var options = new DriverUpdateOptions
{
    LogLevel = "Debug",                           // Verbose logging
    LogFilePath = "./Logs/driver-update-.log",    // Rolling file logs
    EnableConsoleLogging = true,                  // Console output
    EnableFileLogging = true                      // File output
};

var updater = GeneralDrivelution.Create(options);
```

### Using Custom Logger

```csharp
using Serilog;

var logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/custom-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var updater = GeneralDrivelution.Create(logger, options);
```

### Log Levels

- **Debug**: Detailed diagnostic information
- **Info**: General informational messages (default)
- **Warn**: Warning messages for potential issues
- **Error**: Error messages for failures
- **Fatal**: Critical errors that cause termination

## Platform Detection

### Get Platform Information

```csharp
var platformInfo = GeneralDrivelution.GetPlatformInfo();

Console.WriteLine($"Platform: {platformInfo.Platform}");
Console.WriteLine($"OS: {platformInfo.OperatingSystem}");
Console.WriteLine($"Architecture: {platformInfo.Architecture}");
Console.WriteLine($"Version: {platformInfo.SystemVersion}");
Console.WriteLine($"Supported: {platformInfo.IsSupported}");
```

Output example:
```
Platform: Windows
OS: Microsoft Windows 10.0.19045
Architecture: X64
Version: 10.0.19045.0
Supported: Yes
```

## Restart Management

### Restart Modes

```csharp
public enum RestartMode
{
    None,       // No restart required
    Prompt,     // Prompt user to restart
    Delayed,    // Delayed restart
    Immediate   // Immediate restart
}
```

### Handling Restarts

```csharp
var strategy = new UpdateStrategy
{
    RestartMode = RestartMode.Prompt
};

var result = await updater.UpdateAsync(driverInfo, strategy);

if (result.Success)
{
    // Check if restart is required
    if (result.Message.Contains("restart"))
    {
        Console.WriteLine("System restart required for changes to take effect");
        Console.Write("Restart now? (y/n): ");
        
        if (Console.ReadLine()?.ToLower() == "y")
        {
            // Implement restart logic
            Process.Start("shutdown", "/r /t 0");
        }
    }
}
```

## Version Comparison

The library includes SemVer 2.0 compliant version comparison:

```csharp
using GeneralUpdate.Drivelution.Core.Utilities;

// Compare versions
int comparison = VersionComparer.Compare("2.1.0", "2.0.3");
// Returns: 1 (2.1.0 > 2.0.3)

// Check if update is available
bool updateAvailable = VersionComparer.IsGreaterThan("2.1.0", "2.0.3");
// Returns: true

// Semantic versioning with prerelease
bool isNewer = VersionComparer.IsGreaterThan("2.1.0", "2.1.0-beta.1");
// Returns: true (release is greater than prerelease)
```

## Best Practices

### 1. Always Validate Before Update

```csharp
// Validate driver before attempting update
var isValid = await updater.ValidateAsync(driverInfo);

if (!isValid)
{
    Console.WriteLine("Validation failed. Update aborted.");
    return;
}

var result = await updater.UpdateAsync(driverInfo, strategy);
```

### 2. Enable Backups for Production

```csharp
var strategy = new UpdateStrategy
{
    RequireBackup = true,                          // Always backup in production
    BackupPath = GenerateUniqueBackupPath(),       // Use unique paths
    SkipSignatureValidation = false,               // Never skip in production
    SkipHashValidation = false                     // Never skip in production
};
```

### 3. Use Appropriate Retry Settings

```csharp
var strategy = new UpdateStrategy
{
    RetryCount = 3,                    // Reasonable retry count
    RetryIntervalSeconds = 5,          // Short interval for fast recovery
    TimeoutSeconds = 300               // 5 minute timeout
};
```

### 4. Handle Cancellation Properly

```csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMinutes(10));  // Maximum 10 minutes

try
{
    var result = await updater.UpdateAsync(driverInfo, strategy, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Update cancelled by user or timeout");
}
```

### 5. Log Everything

```csharp
var options = new DriverUpdateOptions
{
    LogLevel = "Info",                 // Use Info level in production
    EnableFileLogging = true,          // Keep file logs for auditing
    EnableConsoleLogging = false       // Disable console in services
};
```

### 6. Clean Up Old Backups

```csharp
var options = new DriverUpdateOptions
{
    AutoCleanupBackups = true,         // Enable automatic cleanup
    BackupsToKeep = 5                  // Keep last 5 backups
};
```

### 7. Test with Skip Flags First

```csharp
// Development/Testing only
#if DEBUG
var strategy = new UpdateStrategy
{
    SkipSignatureValidation = true,    // Skip for unsigned test drivers
    SkipHashValidation = false         // Still validate hash
};
#else
var strategy = new UpdateStrategy
{
    SkipSignatureValidation = false,   // Always validate in production
    SkipHashValidation = false
};
#endif
```

## Common Scenarios

### Scenario 1: Silent Background Update

```csharp
public async Task<bool> PerformSilentUpdateAsync(DriverInfo driverInfo)
{
    var options = new DriverUpdateOptions
    {
        EnableConsoleLogging = false,   // No console output
        EnableFileLogging = true,       // Log to file
        LogLevel = "Info"
    };

    var updater = GeneralDrivelution.Create(options);
    
    var strategy = new UpdateStrategy
    {
        RequireBackup = true,
        RetryCount = 3,
        RestartMode = RestartMode.Delayed  // Don't restart immediately
    };

    var result = await updater.UpdateAsync(driverInfo, strategy);
    
    return result.Success;
}
```

### Scenario 2: Interactive Update with Progress

```csharp
public async Task<UpdateResult> InteractiveUpdateAsync(DriverInfo driverInfo)
{
    Console.WriteLine("Starting driver update...");
    
    var strategy = new UpdateStrategy
    {
        RequireBackup = true,
        RetryCount = 3,
        RestartMode = RestartMode.Prompt
    };

    var updater = GeneralDrivelution.Create();
    var result = await updater.UpdateAsync(driverInfo, strategy);

    // Display detailed logs
    Console.WriteLine("\n=== Update Log ===");
    foreach (var log in result.StepLogs)
    {
        Console.WriteLine(log);
    }

    if (result.Success)
    {
        Console.WriteLine($"\n✓ Update completed successfully in {result.DurationMs}ms");
    }
    else
    {
        Console.WriteLine($"\n✗ Update failed: {result.Error?.Message}");
    }

    return result;
}
```

### Scenario 3: Batch Driver Updates

```csharp
public async Task<List<UpdateResult>> BatchUpdateAsync(List<DriverInfo> drivers)
{
    var results = new List<UpdateResult>();
    var updater = GeneralDrivelution.Create();

    foreach (var driver in drivers.OrderByDescending(d => d.Metadata.GetValueOrDefault("Priority", "0")))
    {
        Console.WriteLine($"\nUpdating: {driver.Name} v{driver.Version}");
        
        var strategy = new UpdateStrategy
        {
            RequireBackup = true,
            RetryCount = 2,
            RestartMode = RestartMode.None  // Restart once at the end
        };

        var result = await updater.UpdateAsync(driver, strategy);
        results.Add(result);

        if (!result.Success)
        {
            Console.WriteLine($"Failed to update {driver.Name}, continuing with next driver...");
        }
    }

    // Check if any update requires restart
    if (results.Any(r => r.Success))
    {
        Console.WriteLine("\nAll updates completed. System restart recommended.");
    }

    return results;
}
```

### Scenario 4: Update with Download

```csharp
public async Task<UpdateResult> DownloadAndUpdateAsync(string downloadUrl, DriverInfo driverInfo)
{
    var httpClient = new HttpClient();
    
    // Download driver file
    Console.WriteLine($"Downloading driver from {downloadUrl}...");
    var downloadPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(downloadUrl));
    
    using (var response = await httpClient.GetAsync(downloadUrl))
    {
        response.EnsureSuccessStatusCode();
        await using var fs = new FileStream(downloadPath, FileMode.Create);
        await response.Content.CopyToAsync(fs);
    }
    
    Console.WriteLine("Download complete. Verifying...");
    
    // Update driver info with downloaded file
    driverInfo.FilePath = downloadPath;
    
    // Perform update
    var updater = GeneralDrivelution.Create();
    var strategy = new UpdateStrategy
    {
        RequireBackup = true,
        SkipHashValidation = false  // Verify downloaded file
    };
    
    var result = await updater.UpdateAsync(driverInfo, strategy);
    
    // Clean up downloaded file if update succeeded
    if (result.Success)
    {
        try { File.Delete(downloadPath); } catch { }
    }
    
    return result;
}
```

## Troubleshooting

### Issue: "Administrator privileges are required"

**Solution**: Run your application as Administrator (Windows) or with sudo (Linux).

Windows:
```bash
# Run as administrator
runas /user:Administrator "YourApp.exe"
```

Linux:
```bash
# Run with sudo
sudo ./YourApp
```

### Issue: "Driver validation failed"

**Possible causes**:
1. Hash mismatch - file is corrupted or tampered with
2. Invalid signature - driver is not properly signed
3. Incompatible driver - wrong OS or architecture

**Solution**:
```csharp
// Enable detailed logging to identify the specific validation failure
var options = new DriverUpdateOptions { LogLevel = "Debug" };
var updater = GeneralDrivelution.Create(options);

// Try validation separately
var isValid = await updater.ValidateAsync(driverInfo);
```

### Issue: "Installation failed"

**Solution**: Check the error details and retry if possible:

```csharp
if (!result.Success)
{
    Console.WriteLine($"Error: {result.Error?.Message}");
    Console.WriteLine($"Details: {result.Error?.Details}");
    
    if (result.Error?.CanRetry == true)
    {
        // Retry the operation
        result = await updater.UpdateAsync(driverInfo, strategy);
    }
}
```

### Issue: "Backup failed"

**Solution**: Ensure sufficient disk space and write permissions:

```csharp
var backupPath = @"C:\DriverBackups";

// Ensure directory exists and is writable
Directory.CreateDirectory(backupPath);

// Check available space
var drive = new DriveInfo(Path.GetPathRoot(backupPath));
if (drive.AvailableFreeSpace < 100_000_000)  // 100 MB
{
    Console.WriteLine("Insufficient disk space for backup");
}
```

### Issue: "Platform not supported"

**Solution**: Check platform compatibility:

```csharp
var platformInfo = GeneralDrivelution.GetPlatformInfo();

if (!platformInfo.IsSupported)
{
    Console.WriteLine($"Platform {platformInfo.Platform} is not supported");
    Console.WriteLine("Supported platforms: Windows (8+), Linux (Ubuntu 18.04+)");
}
```

## Performance Considerations

### 1. File I/O Optimization

The library uses async I/O for all file operations to avoid blocking:

```csharp
// All operations are async
var result = await updater.UpdateAsync(driverInfo, strategy);
var isValid = await updater.ValidateAsync(driverInfo);
var backed = await updater.BackupAsync(driverInfo, backupPath);
```

### 2. Hash Computation

Hash validation is performed asynchronously in chunks to handle large files efficiently:

```csharp
// Efficient for large driver files (100+ MB)
var hash = await HashValidator.ComputeHashAsync(filePath, "SHA256");
```

### 3. Timeout Settings

Configure appropriate timeouts based on file size and system performance:

```csharp
var strategy = new UpdateStrategy
{
    TimeoutSeconds = 300  // 5 minutes for typical drivers
    // Increase for large drivers (500+ MB)
};
```

## Security Considerations

### 1. Always Validate Signatures

```csharp
// Production configuration
var strategy = new UpdateStrategy
{
    SkipSignatureValidation = false,  // Never skip in production
    SkipHashValidation = false
};
```

### 2. Use Strong Hash Algorithms

```csharp
var driverInfo = new DriverInfo
{
    HashAlgorithm = "SHA256"  // Preferred over MD5
};
```

### 3. Verify Publisher Trust

```csharp
var driverInfo = new DriverInfo
{
    TrustedPublishers = new List<string>
    {
        "CN=YourCompany, O=YourCompany, C=US"
    }
};
```

### 4. Secure Backup Storage

```csharp
// Store backups in a secure location
var strategy = new UpdateStrategy
{
    BackupPath = @"C:\SecureBackups\Drivers"  // Use appropriate ACLs
};
```

## API Reference

### GeneralDrivelution (Static Class)

```csharp
// Create updater with default options
IGeneralDrivelution Create(DriverUpdateOptions? options = null)

// Create updater with custom logger
IGeneralDrivelution Create(ILogger logger, DriverUpdateOptions? options = null)

// Quick update with default settings
Task<UpdateResult> QuickUpdateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default)

// Quick update with custom strategy
Task<UpdateResult> QuickUpdateAsync(DriverInfo driverInfo, UpdateStrategy strategy, CancellationToken cancellationToken = default)

// Validate driver
Task<bool> ValidateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default)

// Get platform information
PlatformInfo GetPlatformInfo()
```

### IGeneralDrivelution Interface

```csharp
// Update driver
Task<UpdateResult> UpdateAsync(DriverInfo driverInfo, UpdateStrategy strategy, CancellationToken cancellationToken = default)

// Validate driver
Task<bool> ValidateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default)

// Backup driver
Task<bool> BackupAsync(DriverInfo driverInfo, string backupPath, CancellationToken cancellationToken = default)

// Rollback driver
Task<bool> RollbackAsync(string backupPath, CancellationToken cancellationToken = default)
```

## Contributing

Contributions are welcome! Please see the main repository for contribution guidelines.

## License

This project is licensed under the Apache 2.0 License. See the LICENSE file for details.

## Support

- **Documentation**: [https://www.justerzhu.cn/](https://www.justerzhu.cn/)
- **Issues**: [GitHub Issues](https://github.com/GeneralLibrary/GeneralUpdate/issues)
- **Discussions**: Join the discussion group (contact information in main README)

## Related Projects

- **GeneralUpdate**: Main automatic update framework
- **GeneralUpdate.Tools**: Update patch creation tool
- **GeneralUpdate.Maui**: Mobile update support (Android)
- **GeneralUpdate-Samples**: Usage examples

## Changelog

### Version 1.0.0 (2026-02-08)
- Initial release
- Windows and Linux platform support
- Comprehensive validation (hash, signature, compatibility)
- Backup and rollback mechanisms
- Configurable retry logic
- Detailed logging with Serilog
- AOT compatibility

## Roadmap

### Upcoming Features
- [ ] macOS platform support
- [ ] Progress reporting callbacks
- [ ] Concurrent driver updates
- [ ] Advanced dependency management
- [ ] Remote driver repository support
- [ ] Automatic driver discovery
- [ ] Web-based management dashboard

---

**Note**: This library is part of the GeneralUpdate ecosystem. For complete application update solutions, see the main GeneralUpdate framework.
