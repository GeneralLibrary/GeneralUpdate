# ConfiginfoBuilder Usage Guide

The `ConfiginfoBuilder` class provides a simple and convenient way to create `Configinfo` objects for the GeneralUpdate system. It only requires three essential parameters while automatically generating platform-appropriate defaults for all other configuration items.

**Design Philosophy**: Inspired by zero-configuration patterns from projects like [Velopack](https://github.com/velopack/velopack), this builder minimizes required configuration while maintaining flexibility through optional fluent setters.

## Automatic Configuration Detection

The ConfiginfoBuilder implements intelligent zero-configuration by automatically extracting information from your project:

### Comprehensive Project Metadata Extraction

The builder automatically attempts to read multiple fields from your project file (`.csproj`):

1. **Application Name** (maps to `AppName`, `MainAppName`)
   - **Extracts** the `<AssemblyName>` property if specified
   - **Falls back** to the csproj file name if `AssemblyName` is not defined
   - **Applies** platform-specific extensions (`.exe` on Windows, none on Linux/macOS)

2. **Version Number** (maps to `ClientVersion`, `UpgradeClientVersion`)
   - **Reads** the `<Version>` property from csproj
   - **Falls back** to default "1.0.0" if not specified

3. **Publisher Information** (maps to `ProductId`)
   - **Prioritizes** the `<Company>` property
   - **Falls back** to `<Authors>` property if Company not specified
   - **Uses** default value if neither is available

**Example:**
```xml
<!-- MyApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>MyApplication</AssemblyName>
    <Version>2.1.5</Version>
    <Company>MyCompany</Company>
    <Authors>Development Team</Authors>
  </PropertyGroup>
</Project>
```

When the builder runs, it will automatically extract:
- **App Name**: `MyApplication.exe` (Windows) or `MyApplication` (Linux/macOS)
- **Version**: `2.1.5`
- **Product ID**: `MyCompany`

All automatic with zero configuration needed!

## Basic Usage

### Minimal Configuration

The simplest way to create a `Configinfo` object is to provide just the three required parameters:

```csharp
using GeneralUpdate.Common.Shared.Object;

// Method 1: Direct constructor (traditional)
var config = new ConfiginfoBuilder(
    updateUrl: "https://api.example.com/updates",
    token: "your-auth-token",
    scheme: "https"
).Build();

// Method 2: Factory method (recommended, more fluent)
var config2 = ConfiginfoBuilder
    .Create("https://api.example.com/updates", "your-auth-token", "https")
    .Build();

// The config object now has all necessary defaults set based on the platform
// Application name is automatically detected from your project file!
```

## Platform-Specific Defaults

The builder automatically detects the runtime platform and sets appropriate defaults:

### Windows Platform
- **Install Path**: Current application's base directory (via `AppDomain.CurrentDomain.BaseDirectory`)
- **App Names**: Auto-detected from csproj + `.exe` extension (e.g., `MyApp.exe`)
- **Script**: Empty (Windows doesn't typically need permission scripts)
- **Path Separator**: Backslash (`\`) - handled automatically by .NET
- **Black Formats**: `.log`, `.tmp` (from `ConfiginfoBuilder.DefaultBlackFormats`)

### Linux Platform
- **Install Path**: Current application's base directory (via `AppDomain.CurrentDomain.BaseDirectory`)
- **App Names**: Auto-detected from csproj, no `.exe` extension (e.g., `myapp`)
- **Script**: Default chmod script for granting execution permissions
- **Path Separator**: Forward slash (`/`) - handled automatically by .NET
- **Black Formats**: `.log`, `.tmp` (from `ConfiginfoBuilder.DefaultBlackFormats`)

### macOS Platform
- **Install Path**: Current application's base directory (via `AppDomain.CurrentDomain.BaseDirectory`)
- **App Names**: Auto-detected from csproj, no `.exe` extension (e.g., `myapp`)
- **Script**: Default chmod script for granting execution permissions
- **Path Separator**: Forward slash (`/`) - handled automatically by .NET
- **Black Formats**: `.log`, `.tmp` (from `ConfiginfoBuilder.DefaultBlackFormats`)

**Note**: The install path defaults to the current application's running directory, which does not require administrator privileges and automatically extracts the location from the host program.

## Customizing Configuration

You can override any default value using the fluent API:

```csharp
var config = new ConfiginfoBuilder(
    "https://api.example.com/updates",
    "your-auth-token",
    "https"
)
.SetAppName("MyApplication.exe")
.SetMainAppName("MyApplication.exe")
.SetClientVersion("1.5.0")
.SetInstallPath("/opt/myapp")
.SetAppSecretKey("my-secret-key")
.SetProductId("product-123")
.Build();
```

## Advanced Configuration

### Setting Update URLs

```csharp
var config = new ConfiginfoBuilder(updateUrl, token, scheme)
    .SetUpdateLogUrl("https://example.com/changelog")
    .SetReportUrl("https://api.example.com/report")
    .Build();
```

### Configuring File Filters

You can customize which files should be excluded from updates. By default, `.log` and `.tmp` files are excluded:

```csharp
// Use default black formats
var config1 = new ConfiginfoBuilder(updateUrl, token, scheme).Build();
// config1.BlackFormats will contain ConfiginfoBuilder.DefaultBlackFormats (.log, .tmp)

// Override with custom filters
var config2 = new ConfiginfoBuilder(updateUrl, token, scheme)
    .SetBlackFiles(new List<string> { "config.json", "user.dat" })
    .SetBlackFormats(new List<string> { ".log", ".tmp", ".cache", ".bak" })
    .SetSkipDirectorys(new List<string> { "/temp", "/logs" })
    .Build();
```

### Setting Process and Script Options

```csharp
var config = new ConfiginfoBuilder(updateUrl, token, scheme)
    .SetBowl("Bowl.exe")  // Process to terminate before update
    .SetScript("#!/bin/bash\nchmod +x \"$1\"")  // Custom permission script
    .SetDriverDirectory("/opt/myapp/drivers")
    .Build();
```

## Complete Example

Here's a comprehensive example showing all available options:

```csharp
using GeneralUpdate.Common.Shared.Object;
using System.Collections.Generic;

public class UpdateConfigExample
{
    public static Configinfo CreateUpdateConfig()
    {
        var config = new ConfiginfoBuilder(
            updateUrl: "https://api.example.com/updates",
            token: "Bearer abc123xyz",
            scheme: "https"
        )
        // Application Info
        .SetAppName("MyApp.exe")
        .SetMainAppName("MyApp.exe")
        .SetClientVersion("2.1.0")
        .SetUpgradeClientVersion("1.0.0")
        .SetProductId("myapp-001")
        .SetAppSecretKey("secure-secret-key-789")
        
        // Paths
        .SetInstallPath("/opt/myapp")
        .SetDriverDirectory("/opt/myapp/drivers")
        
        // URLs
        .SetUpdateLogUrl("https://myapp.example.com/changelog")
        .SetReportUrl("https://api.example.com/report")
        
        // File Filters
        .SetBlackFiles(new List<string> 
        { 
            "config.json", 
            "user-settings.dat" 
        })
        .SetBlackFormats(new List<string> 
        { 
            ".log", 
            ".tmp", 
            ".cache",
            ".bak"
        })
        .SetSkipDirectorys(new List<string> 
        { 
            "/temp", 
            "/logs",
            "/cache"
        })
        
        // Process Options
        .SetBowl("Bowl.exe")
        .SetScript("#!/bin/bash\nchmod +x \"$1\"\n")
        
        .Build();
        
        // Validate the configuration
        config.Validate();
        
        return config;
    }
}
```

## Error Handling

The builder performs validation at two stages:

### Construction Time
The constructor validates the three required parameters:

```csharp
try
{
    var builder = new ConfiginfoBuilder(
        null,  // Invalid: null UpdateUrl
        "token",
        "https"
    );
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    // Output: UpdateUrl cannot be null or empty.
}
```

### Build Time
The `Build()` method validates the complete configuration:

```csharp
try
{
    var config = new ConfiginfoBuilder(updateUrl, token, scheme)
        .SetAppName("")  // Invalid: empty app name
        .Build();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Build Error: {ex.Message}");
    // Output: Failed to build valid Configinfo: AppName cannot be empty
}
```

## Best Practices

1. **Use Minimal Configuration**: Only override defaults when necessary
2. **Validate Early**: Let the builder validate parameters immediately
3. **Use Method Chaining**: Take advantage of the fluent API for clean code
4. **Platform Awareness**: Let the builder handle platform-specific defaults
5. **Secure Tokens**: Never hardcode authentication tokens in production code

## Migration from Manual Construction

### Before (Manual Construction)
```csharp
var config = new Configinfo
{
    UpdateUrl = "https://api.example.com/updates",
    Token = "token",
    Scheme = "https",
    AppName = "App.exe",
    MainAppName = "App.exe",
    ClientVersion = "1.0.0",
    UpgradeClientVersion = "1.0.0",
    InstallPath = Thread.GetDomain().BaseDirectory,
    AppSecretKey = "secret",
    ProductId = "product",
    BlackFiles = new List<string>(),
    BlackFormats = new List<string> { ".log", ".tmp" },
    SkipDirectorys = new List<string>()
};
```

### After (Using Builder)
```csharp
var config = new ConfiginfoBuilder(
    "https://api.example.com/updates",
    "token",
    "https"
).Build();  // All defaults are set automatically!
```

## Platform Detection Example

The builder automatically adapts to the runtime environment:

```csharp
var config = new ConfiginfoBuilder(updateUrl, token, scheme).Build();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Console.WriteLine($"Windows config: {config.InstallPath}");
    // Output: Windows config: C:\MyApp\ (current application directory)
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    Console.WriteLine($"Linux config: {config.InstallPath}");
    // Output: Linux config: /opt/myapp/ (current application directory)
}
```

## See Also

- `Configinfo` class documentation
- `BaseConfigInfo` class documentation
- GeneralUpdate main documentation
