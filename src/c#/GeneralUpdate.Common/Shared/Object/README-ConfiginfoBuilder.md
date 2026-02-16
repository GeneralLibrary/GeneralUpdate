# ConfiginfoBuilder - Universal Configuration Builder

## Overview

The `ConfiginfoBuilder` class provides a simple, fluent API for creating `Configinfo` objects with minimal effort. It automatically handles platform-specific defaults and only requires three essential parameters.

**NEW**: ConfiginfoBuilder now supports loading configuration from a JSON file (`update_config.json`) placed in the running directory. When this file exists, it takes the highest priority, overriding any parameters passed to the builder.

**Design Inspiration**: The zero-configuration approach is inspired by projects like [Velopack](https://github.com/velopack/velopack), focusing on sensible defaults extracted from the running application with minimal user input required.

## Configuration Priority

The ConfiginfoBuilder follows this priority order:

1. **JSON Configuration File** (`update_config.json`) - **HIGHEST PRIORITY**
2. **Builder Setter Methods** (via `.SetXXX()` methods)
3. **Default Values** (platform-specific defaults)

## JSON Configuration File Support

Place an `update_config.json` file in your application's running directory to configure all update settings. When this file is present, ConfiginfoBuilder will automatically load settings from it, ignoring parameters passed to the `Create()` method.

**Example `update_config.json`:**
```json
{
  "UpdateUrl": "https://api.example.com/updates",
  "Token": "your-authentication-token",
  "Scheme": "https",
  "AppName": "Update.exe",
  "MainAppName": "MyApplication.exe",
  "ClientVersion": "1.0.0",
  "InstallPath": "/path/to/installation"
}
```

See [update_config.example.json](update_config.example.json) for a complete example with all available options.

## Auto-Configuration Features

🔍 **Application Name Detection**: Automatically reads `<AssemblyName>` from `.csproj`  
📊 **Version Extraction**: Reads `<Version>` field for `ClientVersion`  
🏢 **Publisher Info**: Extracts `<Company>` or `<Authors>` for `ProductId`  
📂 **Path Extraction**: Uses host program's base directory  
🖥️ **Platform Detection**: Adapts to Windows, Linux, and macOS  

## Quick Start

```csharp
using GeneralUpdate.Common.Shared.Object;

// Method 1: With JSON configuration file (RECOMMENDED)
// Place update_config.json in your app's running directory
// The Create method will automatically load from the file
var config = ConfiginfoBuilder
    .Create("https://fallback.com/updates", "fallback-token", "https")
    .Build();
// If update_config.json exists, those values are used instead of the parameters

// Method 2: Using code configuration only (no JSON file)
var config2 = ConfiginfoBuilder
    .Create("https://api.example.com/updates", "your-auth-token", "https")
    .Build();

// Method 3: Code configuration with custom overrides
var config3 = ConfiginfoBuilder
    .Create("https://api.example.com/updates", "your-auth-token", "https")
    .SetAppName("MyApp.exe")
    .SetInstallPath("/custom/path")
    .Build();
```

## Key Features

✅ **JSON Configuration Support**: Load settings from `update_config.json` with highest priority  
✅ **Minimal Parameters**: Only 3 required parameters (UpdateUrl, Token, Scheme)  
✅ **Cross-Platform**: Automatically detects and adapts to Windows/Linux/macOS  
✅ **Smart Defaults**: Platform-appropriate paths, separators, and configurations  
✅ **Fluent API**: Clean, readable method chaining  
✅ **Type-Safe**: Compile-time parameter validation  
✅ **Well-Tested**: 39 comprehensive unit tests including JSON configuration scenarios

## Platform Detection

The builder automatically adapts based on your runtime environment:

| Aspect | Windows | Linux | macOS |
|--------|---------|-------|-------|
| Install Path | Current app directory | Current app directory | Current app directory |
| App Names | Auto from csproj + `.exe` | Auto from csproj | Auto from csproj |
| Script | Empty | chmod script | chmod script |
| Path Separator | `\` (automatic) | `/` (automatic) | `/` (automatic) |

## Documentation

- **[Usage Guide](ConfiginfoBuilder-Usage.md)** - Comprehensive documentation with examples
- **[Example Code](ConfiginfoBuilder-Example.cs)** - Runnable examples demonstrating all features

## Examples

### Using JSON Configuration File
```csharp
// Create update_config.json in your app directory:
// {
//   "UpdateUrl": "https://api.example.com/updates",
//   "Token": "my-token",
//   "Scheme": "https",
//   "AppName": "MyApp.exe",
//   "ClientVersion": "2.0.0"
// }

// The builder will automatically load from the file
var config = ConfiginfoBuilder
    .Create("ignored", "ignored", "ignored")
    .Build();
// Values come from update_config.json!
```

### Basic Usage (No JSON File)
```csharp
var config = ConfiginfoBuilder
    .Create(updateUrl, token, scheme)
    .Build();
```

### Custom Configuration
```csharp
var config = ConfiginfoBuilder
    .Create(updateUrl, token, scheme)
    .SetAppName("MyApp.exe")
    .SetClientVersion("2.0.0")
    .SetInstallPath("/opt/myapp")
    .Build();
```

### With File Filters
```csharp
var config = ConfiginfoBuilder
    .Create(updateUrl, token, scheme)
    .SetBlackFormats(new List<string> { ".log", ".tmp", ".cache" })
    .SetSkipDirectorys(new List<string> { "/temp", "/logs" })
    .Build();
```

## Default Values

The builder and `Configinfo` class provide these defaults:

### Configinfo Class Defaults (Property Initializers)
- **AppName**: "Update.exe"
- **InstallPath**: Current program's running directory (`AppDomain.CurrentDomain.BaseDirectory`)

### ConfiginfoBuilder Defaults (for Builder Pattern)
- **ClientVersion**: "1.0.0"
- **UpgradeClientVersion**: "1.0.0"
- **AppSecretKey**: "default-secret-key"
- **ProductId**: "default-product-id"
- **MainAppName**: "App.exe"
- **BlackFormats**: `.log`, `.tmp`, `.cache`, `.bak` (via `ConfiginfoBuilder.DefaultBlackFormats`)
- **BlackFiles**: Empty list
- **SkipDirectorys**: Empty list

All defaults can be overridden using:
1. JSON configuration file (`update_config.json`) - highest priority
2. Builder setter methods (`.SetXXX()`)
3. Direct property assignment on `Configinfo` objects

## Error Handling

The builder validates parameters at two stages:

1. **Construction Time**: Validates required parameters (UpdateUrl, Token, Scheme)
2. **Build Time**: Validates the complete configuration before returning

```csharp
try {
    var config = new ConfiginfoBuilder(invalidUrl, token, scheme).Build();
} catch (ArgumentException ex) {
    Console.WriteLine($"Invalid parameter: {ex.Message}");
} catch (InvalidOperationException ex) {
    Console.WriteLine($"Build failed: {ex.Message}");
}
```

## Testing

The implementation includes 39 comprehensive unit tests covering:
- Constructor validation
- Default value generation
- Platform-specific behavior
- Method chaining
- Error handling
- JSON configuration file loading
- Fallback behavior when JSON is invalid
- Complete integration scenarios

Run tests with:
```bash
dotnet test CoreTest/CoreTest.csproj --filter "FullyQualifiedName~ConfiginfoBuilderTests"
```

## Security

✅ No security vulnerabilities detected by CodeQL  
✅ Input validation on all parameters  
✅ No hardcoded secrets or credentials  

## Compatibility

- **.NET Standard 2.0** and above
- **.NET 10.0** and above
- **Cross-platform**: Windows, Linux (and other Unix-like systems)

## Migration from Manual Construction

**Before:**
```csharp
var config = new Configinfo
{
    UpdateUrl = url,
    Token = token,
    Scheme = scheme,
    AppName = "App.exe",
    MainAppName = "App.exe",
    ClientVersion = "1.0.0",
    // ... 15+ more properties
};
```

**After (with JSON configuration):**
```json
// update_config.json
{
  "UpdateUrl": "https://api.example.com/updates",
  "Token": "my-token",
  "Scheme": "https",
  "AppName": "MyApp.exe",
  "ClientVersion": "1.0.0"
}
```
```csharp
var config = ConfiginfoBuilder
    .Create("fallback", "fallback", "https")
    .Build();
```

**After (with code only):**
```csharp
var config = ConfiginfoBuilder
    .Create(url, token, scheme)
    .Build();
```

## Contributing

This implementation follows the repository's coding standards and includes:
- Comprehensive XML documentation
- Extensive unit tests
- Usage examples
- Error handling

## License

Part of the GeneralUpdate project. See the main LICENSE file for details.
