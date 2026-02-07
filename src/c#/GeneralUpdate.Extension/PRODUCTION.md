# Production Deployment Guide

This guide explains how to use the GeneralUpdate.Extension system in production environments.

## Quick Start

### 1. Installation

Add the NuGet package to your project:

```bash
dotnet add package GeneralUpdate.Extension
```

Or add to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="GeneralUpdate.Extension" Version="1.0.0" />
</ItemGroup>
```

### 2. Basic Setup

```csharp
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Common.Models;

// Create extension host
var host = new ExtensionHostBuilder()
    .ConfigureOptions(options =>
    {
        options.ServerUrl = "https://your-api-server.com";
        options.BearerToken = "your-bearer-token";
        options.HostVersion = "1.0.0";
        options.ExtensionsDirectory = "./extensions";
        options.EnableAutoUpdate = true;
    })
    .Build();

// Subscribe to events
host.ExtensionUpdateStatusChanged += (sender, e) =>
{
    Console.WriteLine($"{e.ExtensionName}: {e.Status} - {e.Progress}%");
};

// Load local extensions
host.LoadInstalledExtensions();

// Query server for available extensions
var result = await host.QueryExtensionsAsync(new ExtensionQueryDTO
{
    Platform = TargetPlatform.Windows,
    HostVersion = "1.0.0",
    PageNumber = 1,
    PageSize = 20
});

// Update an extension (with automatic dependency resolution)
await host.UpdateExtensionAsync("extension-id");
```

## Production Checklist

### Before Deployment

- [ ] Configure proper server URL and authentication
- [ ] Set up extensions directory with proper permissions
- [ ] Configure logging (using ILogger if available)
- [ ] Test network connectivity to extension server
- [ ] Verify SSL/TLS certificates
- [ ] Test with sample extensions

### Security Considerations

1. **Bearer Token Management**
   - Store tokens securely (Azure Key Vault, AWS Secrets Manager, etc.)
   - Rotate tokens regularly
   - Never hardcode tokens in source code

2. **File System Permissions**
   - Ensure extensions directory has appropriate read/write permissions
   - Validate extension packages before installation
   - Use antivirus scanning on downloaded extensions

3. **Network Security**
   - Use HTTPS for all server communication
   - Validate SSL/TLS certificates
   - Implement request timeout and retry policies
   - Consider rate limiting

### Performance Optimization

1. **Concurrent Downloads**
   - The system uses ConcurrentQueue for parallel downloads
   - Adjust concurrent download limits based on bandwidth
   - Monitor memory usage during large downloads

2. **Caching**
   - Local catalog is cached in JSON format
   - Extension metadata is cached
   - Consider implementing HTTP caching headers

3. **Resource Management**
   - Always dispose IExtensionHost when done
   - Monitor file handles and network connections
   - Clean up old backup files periodically

## Advanced Configuration

### Dependency Injection Integration

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

var host = new ExtensionHostBuilder()
    .ConfigureOptions(options =>
    {
        options.ServerUrl = "https://api.example.com";
        options.BearerToken = "token";
        options.HostVersion = "1.0.0";
        options.ExtensionsDirectory = "./extensions";
    })
    .ConfigureServices(svc =>
    {
        // Add custom services
        svc.AddSingleton<IMyCustomService, MyCustomService>();
    })
    .Build();
```

### Custom Service Implementation

```csharp
// Implement custom services by injecting interfaces
public class MyExtensionManager
{
    private readonly IExtensionHost _host;
    private readonly IVersionCompatibilityChecker _versionChecker;
    
    public MyExtensionManager(
        IExtensionHost host,
        IVersionCompatibilityChecker versionChecker)
    {
        _host = host;
        _versionChecker = versionChecker;
    }
    
    public async Task UpdateAllCompatibleExtensionsAsync()
    {
        var installed = _host.GetInstalledExtensions();
        
        foreach (var ext in installed)
        {
            if (_versionChecker.IsCompatible(_host.GetHostVersion(), ext))
            {
                await _host.UpdateExtensionAsync(ext.Id);
            }
        }
    }
}
```

## Error Handling

### Common Exceptions

```csharp
try
{
    await host.UpdateExtensionAsync("extension-id");
}
catch (HttpRequestException ex)
{
    // Network error
    Console.WriteLine($"Network error: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // Invalid operation (e.g., incompatible version)
    Console.WriteLine($"Operation error: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    // File system permission error
    Console.WriteLine($"Permission error: {ex.Message}");
}
catch (Exception ex)
{
    // General error
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

### Rollback on Failure

The system automatically creates backups before installation:

```csharp
// Rollback is automatic on failure
await host.UpdateExtensionAsync("extension-id", enableRollback: true);

// The system will:
// 1. Create backup of existing extension
// 2. Download and install new version
// 3. If installation fails, automatically restore from backup
// 4. Fire ExtensionUpdateStatusChanged event with status
```

## Monitoring and Logging

### Event Monitoring

```csharp
host.ExtensionUpdateStatusChanged += (sender, e) =>
{
    // Log to your logging system
    logger.LogInformation(
        "Extension {Name} status: {Status}, Progress: {Progress}%",
        e.ExtensionName,
        e.Status,
        e.Progress);
    
    // Update UI
    UpdateProgressBar(e.Progress);
    
    // Send telemetry
    telemetry.TrackEvent("ExtensionUpdate", new Dictionary<string, string>
    {
        ["ExtensionId"] = e.ExtensionId,
        ["Status"] = e.Status.ToString(),
        ["Progress"] = e.Progress.ToString()
    });
};
```

### Health Checks

```csharp
public async Task<bool> CheckSystemHealthAsync(IExtensionHost host)
{
    try
    {
        // Check local catalog
        host.LoadInstalledExtensions();
        var installed = host.GetInstalledExtensions();
        
        // Check server connectivity
        var result = await host.QueryExtensionsAsync(new ExtensionQueryDTO
        {
            PageNumber = 1,
            PageSize = 1
        });
        
        return result?.Success == true;
    }
    catch
    {
        return false;
    }
}
```

## Platform-Specific Considerations

### Windows

- Extensions directory: `C:\ProgramData\YourApp\Extensions`
- Use Windows services for background updates
- Consider UAC for system-level installations

### Linux

- Extensions directory: `/var/lib/yourapp/extensions` or `~/.local/share/yourapp/extensions`
- Use systemd for background services
- Check file permissions (chmod 755)

### macOS

- Extensions directory: `~/Library/Application Support/YourApp/Extensions`
- Use launchd for background services
- Handle Gatekeeper and code signing

## Server API Requirements

Your server must implement these endpoints:

### Query Extensions
```
GET /extensions
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "name": "extension-name",
  "platform": 1,  // Windows
  "hostVersion": "1.0.0",
  "pageNumber": 1,
  "pageSize": 20
}

Response:
{
  "success": true,
  "data": {
    "items": [...],
    "totalCount": 100,
    "pageNumber": 1,
    "pageSize": 20,
    "totalPages": 5
  }
}
```

### Download Extension
```
GET /extensions/{id}
Authorization: Bearer {token}
Range: bytes=0-1023  // Optional, for resume support

Response: 
- Binary stream of extension package
- Supports HTTP Range requests (206 Partial Content)
```

## Testing

### Unit Testing

```csharp
[Fact]
public void VersionCompatibility_ValidRange_ReturnsTrue()
{
    var checker = new VersionCompatibilityChecker();
    var extension = new ExtensionMetadata
    {
        MinHostVersion = "1.0.0",
        MaxHostVersion = "2.0.0"
    };
    
    Assert.True(checker.IsCompatible("1.5.0", extension));
}
```

### Integration Testing

```csharp
[Fact]
public async Task UpdateExtension_ValidId_Succeeds()
{
    var host = new ExtensionHostBuilder()
        .ConfigureOptions(options =>
        {
            options.ServerUrl = "https://test-api.example.com";
            options.BearerToken = "test-token";
            options.HostVersion = "1.0.0";
            options.ExtensionsDirectory = "./test-extensions";
        })
        .Build();
    
    bool success = false;
    host.ExtensionUpdateStatusChanged += (s, e) =>
    {
        if (e.Status == ExtensionUpdateStatus.UpdateSuccessful)
            success = true;
    };
    
    await host.UpdateExtensionAsync("test-extension-id");
    
    Assert.True(success);
}
```

## Troubleshooting

### Common Issues

1. **"Network error" during update**
   - Check server URL is correct
   - Verify Bearer token is valid
   - Check firewall/proxy settings
   - Ensure HTTPS certificates are valid

2. **"Version incompatible" error**
   - Check MinHostVersion/MaxHostVersion in extension metadata
   - Verify host version is set correctly
   - Update host application if needed

3. **"Platform not supported" error**
   - Extension may not support current platform
   - Check TargetPlatform flags in extension metadata

4. **"Permission denied" error**
   - Check extensions directory permissions
   - Run with elevated privileges if needed
   - Verify disk space available

## Support

For issues, questions, or contributions:
- GitHub Issues: https://github.com/JusterZhu/JusterLab
- Documentation: See ARCHITECTURE.md

## License

See LICENSE file in repository.
