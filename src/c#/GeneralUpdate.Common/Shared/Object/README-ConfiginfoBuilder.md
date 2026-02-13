# ConfiginfoBuilder - Universal Configuration Builder

## Overview

The `ConfiginfoBuilder` class provides a simple, fluent API for creating `Configinfo` objects with minimal effort. It automatically handles platform-specific defaults and only requires three essential parameters.

## Quick Start

```csharp
using GeneralUpdate.Common.Shared.Object;

// Create a configuration with just 3 parameters
var config = new ConfiginfoBuilder(
    updateUrl: "https://api.example.com/updates",
    token: "your-auth-token",
    scheme: "https"
).Build();

// That's it! All defaults are set automatically based on your platform.
```

## Key Features

✅ **Minimal Parameters**: Only 3 required parameters (UpdateUrl, Token, Scheme)  
✅ **Cross-Platform**: Automatically detects and adapts to Windows/Linux  
✅ **Smart Defaults**: Platform-appropriate paths, separators, and configurations  
✅ **Fluent API**: Clean, readable method chaining  
✅ **Type-Safe**: Compile-time parameter validation  
✅ **Well-Tested**: 32 comprehensive unit tests

## Platform Detection

The builder automatically adapts based on your runtime environment:

| Aspect | Windows | Linux |
|--------|---------|-------|
| Install Path | Current app directory | Current app directory |
| App Names | `App.exe` | `app` |
| Script | Empty | chmod script |
| Path Separator | `\` (automatic) | `/` (automatic) |

## Documentation

- **[Usage Guide](ConfiginfoBuilder-Usage.md)** - Comprehensive documentation with examples
- **[Example Code](ConfiginfoBuilder-Example.cs)** - Runnable examples demonstrating all features

## Examples

### Basic Usage
```csharp
var config = new ConfiginfoBuilder(updateUrl, token, scheme).Build();
```

### Custom Configuration
```csharp
var config = new ConfiginfoBuilder(updateUrl, token, scheme)
    .SetAppName("MyApp.exe")
    .SetClientVersion("2.0.0")
    .SetInstallPath("/opt/myapp")
    .Build();
```

### With File Filters
```csharp
var config = new ConfiginfoBuilder(updateUrl, token, scheme)
    .SetBlackFormats(new List<string> { ".log", ".tmp", ".cache" })
    .SetSkipDirectorys(new List<string> { "/temp", "/logs" })
    .Build();
```

## Default Values

The builder provides these defaults automatically:

- **ClientVersion**: "1.0.0"
- **UpgradeClientVersion**: "1.0.0"
- **AppSecretKey**: "default-secret-key"
- **ProductId**: "default-product-id"
- **BlackFormats**: `.log`, `.tmp` (via `ConfiginfoBuilder.DefaultBlackFormats`)
- **BlackFiles**: Empty list
- **SkipDirectorys**: Empty list

All defaults can be overridden using the setter methods.

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

The implementation includes 32 comprehensive unit tests covering:
- Constructor validation
- Default value generation
- Platform-specific behavior
- Method chaining
- Error handling
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

**After:**
```csharp
var config = new ConfiginfoBuilder(url, token, scheme).Build();
```

## Contributing

This implementation follows the repository's coding standards and includes:
- Comprehensive XML documentation
- Extensive unit tests
- Usage examples
- Error handling

## License

Part of the GeneralUpdate project. See the main LICENSE file for details.
