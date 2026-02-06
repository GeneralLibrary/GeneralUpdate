# GeneralUpdate.Extension

A production-ready VS Code-compliant extension/plugin update system with version compatibility, automatic updates, download queuing, rollback capabilities, and package generation.

## Features

- ✅ **VS Code Standard Compliance** - Extension metadata follows VS Code package.json structure
- ✅ **Dependency Injection Ready** - Interfaces for all services, easy Prism/DI integration
- ✅ **Multi-Platform** - Windows, Linux, macOS with platform-specific filtering
- ✅ **Version Compatibility** - Min/max host version validation and automatic matching
- ✅ **Update Queue** - Thread-safe queue with state tracking and event notifications
- ✅ **Automatic Updates** - Global and per-extension auto-update settings
- ✅ **Rollback Support** - Automatic backup and restoration on installation failure
- ✅ **Package Generation** - Create extension packages from source directories
- ✅ **Differential Patching** - Efficient updates using GeneralUpdate.Differential
- ✅ **AOT Compatible** - No reflection, supports Native AOT compilation
- ✅ **Minimal Dependencies** - Only System.Text.Json required

## Quick Start

### Installation

Add as a project reference:

```xml
<ProjectReference Include="path\to\GeneralUpdate.Extension\GeneralUpdate.Extension.csproj" />
```

Note: This library is currently distributed as source. A NuGet package may be available in the future.

### Basic Usage

```csharp
using GeneralUpdate.Extension;
using GeneralUpdate.Extension.Metadata;

// Create extension host with configuration
var config = new ExtensionHostConfig
{
    HostVersion = new Version(1, 0, 0),
    InstallBasePath = @"C:\MyApp\Extensions",
    DownloadPath = @"C:\MyApp\Downloads",
    ServerUrl = "https://your-server.com/api/extensions",
    TargetPlatform = TargetPlatform.Windows
};

var host = new GeneralExtensionHost(config);

// Load installed extensions
host.LoadInstalledExtensions();

// Subscribe to events
host.UpdateStateChanged += (sender, args) =>
{
    Console.WriteLine($"{args.ExtensionName}: {args.CurrentState}");
};

// Get installed extensions
var installed = host.GetInstalledExtensions();
```

## Complete Usage Guide

### 1. Dependency Injection Setup

The extension system provides interfaces for all core services, making it easy to register with any DI container.

#### With Prism

```csharp
using Prism.Ioc;
using GeneralUpdate.Extension;

public class YourModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var config = new ExtensionHostConfig
        {
            HostVersion = new Version(1, 0, 0),
            InstallBasePath = @"C:\MyApp\Extensions",
            DownloadPath = @"C:\MyApp\Downloads",
            ServerUrl = "https://your-server.com/api/extensions",
            TargetPlatform = Metadata.TargetPlatform.Windows
        };

        // Register as singletons
        containerRegistry.RegisterSingleton<Core.IExtensionCatalog>(() => 
            new Core.ExtensionCatalog(config.InstallBasePath));
        
        containerRegistry.RegisterSingleton<Compatibility.ICompatibilityValidator>(() => 
            new Compatibility.CompatibilityValidator(config.HostVersion));
        
        containerRegistry.RegisterSingleton<Download.IUpdateQueue, Download.UpdateQueue>();
        
        containerRegistry.RegisterSingleton<PackageGeneration.IExtensionPackageGenerator, 
            PackageGeneration.ExtensionPackageGenerator>();
        
        containerRegistry.RegisterSingleton<IExtensionHost>(() => 
            new GeneralExtensionHost(config));
    }
}

// Resolve services
var host = container.Resolve<IExtensionHost>();
```

#### With Microsoft.Extensions.DependencyInjection

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var config = new ExtensionHostConfig
{
    HostVersion = new Version(1, 0, 0),
    InstallBasePath = @"C:\Extensions",
    DownloadPath = @"C:\Downloads",
    ServerUrl = "https://your-server.com/api/extensions",
    TargetPlatform = Metadata.TargetPlatform.Windows
};

services.AddSingleton<Core.IExtensionCatalog>(sp => 
    new Core.ExtensionCatalog(config.InstallBasePath));

services.AddSingleton<Compatibility.ICompatibilityValidator>(sp => 
    new Compatibility.CompatibilityValidator(config.HostVersion));

services.AddSingleton<Download.IUpdateQueue, Download.UpdateQueue>();

services.AddSingleton<PackageGeneration.IExtensionPackageGenerator, 
    PackageGeneration.ExtensionPackageGenerator>();

services.AddSingleton<IExtensionHost>(sp => 
    new GeneralExtensionHost(config));

var provider = services.BuildServiceProvider();
var host = provider.GetRequiredService<IExtensionHost>();
```

#### Without DI (Direct Instantiation)

```csharp
var config = new ExtensionHostConfig
{
    HostVersion = new Version(1, 0, 0),
    InstallBasePath = @"C:\Extensions",
    DownloadPath = @"C:\Downloads",
    ServerUrl = "https://your-server.com/api/extensions",
    TargetPlatform = Metadata.TargetPlatform.Windows
};

var host = new GeneralExtensionHost(config);
```

### 2. Loading and Managing Extensions

```csharp
// Load installed
host.LoadInstalledExtensions();
var installed = host.GetInstalledExtensions();

// Parse remote extensions
var available = host.ParseAvailableExtensions(jsonFromServer);
var compatible = host.GetCompatibleExtensions(available);
```

### 3. Queuing and Processing Updates

```csharp
// Queue updates
var operations = host.QueueAutoUpdates(availableExtensions);

// Process all
await host.ProcessAllUpdatesAsync();

// Monitor progress
host.UpdateStateChanged += (s, e) => Console.WriteLine($"{e.ExtensionName}: {e.CurrentState}");
host.DownloadProgress += (s, e) => Console.WriteLine($"Progress: {e.ProgressPercentage:F1}%");
```

### 4. Package Generation

```csharp
var generator = new ExtensionPackageGenerator();

await generator.GeneratePackageAsync(
    sourceDirectory: @"C:\MyExtension",
    descriptor: myDescriptor,
    outputPath: @"C:\Output\extension.zip");
```

### 5. Version Compatibility Checking

```csharp
// Initialize validator with host version
var hostVersion = new Version(1, 5, 0);
var validator = new Compatibility.CompatibilityValidator(hostVersion);

// Check if an extension is compatible
bool isCompatible = validator.IsCompatible(extensionDescriptor);

// Filter compatible extensions from a list
var allExtensions = host.ParseAvailableExtensions(jsonFromServer);
var compatible = validator.FilterCompatible(allExtensions);

// Find the best version to install
var versions = new[] { new Version(1, 0, 0), new Version(1, 5, 0), new Version(2, 0, 0) };
var bestVersion = validator.FindMinimumSupportedLatest(versions);
```

### 6. Platform-Specific Operations

```csharp
// Get available extensions
var availableExtensions = host.GetCompatibleExtensions(remoteExtensions);

// Filter extensions by platform using bitwise AND
var windowsExtensions = availableExtensions
    .Where(e => (e.Descriptor.SupportedPlatforms & Metadata.TargetPlatform.Windows) != 0)
    .ToList();

// Check multi-platform support
var descriptor = new Metadata.ExtensionDescriptor
{
    Name = "cross-platform-ext",
    SupportedPlatforms = Metadata.TargetPlatform.Windows | 
                         Metadata.TargetPlatform.Linux | 
                         Metadata.TargetPlatform.MacOS
};
```

### 7. Event Monitoring

```csharp
// Monitor all extension events
host.UpdateStateChanged += (s, e) =>
{
    Console.WriteLine($"[{e.CurrentState}] {e.ExtensionName}");
    if (e.ErrorMessage != null)
        Console.WriteLine($"Error: {e.ErrorMessage}");
};

host.DownloadProgress += (s, e) =>
{
    Console.WriteLine($"Downloading: {e.ProgressPercentage:F1}% " +
                     $"({e.BytesReceived}/{e.TotalBytes} bytes) " +
                     $"Speed: {e.BytesPerSecond / 1024:F1} KB/s");
};

host.InstallationCompleted += (s, e) =>
{
    if (e.Success)
        Console.WriteLine($"Installed: {e.ExtensionName} v{e.Version}");
    else
        Console.WriteLine($"Installation failed: {e.ErrorMessage}");
};

host.RollbackCompleted += (s, e) =>
{
    Console.WriteLine($"Rollback {(e.Success ? "succeeded" : "failed")}: {e.ExtensionName}");
};
```

### 8. Auto-Update Configuration

```csharp
// Enable global auto-update
host.GlobalAutoUpdateEnabled = true;

// Enable/disable auto-update for specific extensions
host.SetAutoUpdate("extension-name", true);
host.SetAutoUpdate("another-extension", false);

// Check auto-update status
var installed = host.GetInstalledExtensions();
foreach (var ext in installed)
{
    Console.WriteLine($"{ext.Descriptor.Name}: AutoUpdate={ext.AutoUpdateEnabled}");
}
```

### 9. Manual Update Control

```csharp
// Get an available extension to update
var availableExtension = host.GetCompatibleExtensions(remoteExtensions).FirstOrDefault();

// Queue specific extension for update
var operation = host.QueueUpdate(availableExtension, enableRollback: true);

// Check queue status
var queuedOps = host.GetQueuedOperations();
Console.WriteLine($"Queued updates: {queuedOps.Count}");

// Process updates one at a time
await host.ProcessNextUpdateAsync();

// Or process all at once
await host.ProcessAllUpdatesAsync();

// Cancel pending updates
host.ClearQueue();
```

### 10. Error Handling and Cleanup

```csharp
try
{
    await host.ProcessAllUpdatesAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Update failed: {ex.Message}");
}
finally
{
    // Cleanup if needed
    host.ClearQueue();
}

// Disable/enable specific extensions
host.SetExtensionEnabled("extension-name", false);
host.SetExtensionEnabled("extension-name", true);

// Uninstall extension
bool success = host.UninstallExtension("extension-name");
```

## Extension Metadata (VS Code Compatible)

Extensions use a descriptor structure aligned with VS Code's package.json format:

```json
{
  "name": "my-extension",
  "displayName": "My Extension",
  "version": "1.0.0",
  "description": "Extension description",
  "publisher": "publisher-name",
  "license": "MIT",
  "engines": {
    "minHostVersion": "1.0.0",
    "maxHostVersion": "2.0.0"
  },
  "categories": ["Programming Languages", "Debuggers"],
  "supportedPlatforms": 7
}
```

### Platform Flags

The `supportedPlatforms` field uses bitwise flags:

| Platform | Value | Flag |
|----------|-------|------|
| Windows  | 1     | `TargetPlatform.Windows` |
| Linux    | 2     | `TargetPlatform.Linux` |
| MacOS    | 4     | `TargetPlatform.MacOS` |
| All      | 7     | `TargetPlatform.All` |

Examples:
- Windows only: `1`
- Linux only: `2`
- Windows + Linux: `3`
- All platforms: `7`

### Creating Extension Descriptors

```csharp
var descriptor = new Metadata.ExtensionDescriptor
{
    Name = "my-extension",
    DisplayName = "My Extension",
    Version = "1.0.0",
    Description = "A sample extension",
    Publisher = "publisher-name",
    License = "MIT",
    Categories = new List<string> { "Programming Languages" },
    SupportedPlatforms = Metadata.TargetPlatform.All,
    Compatibility = new Metadata.VersionCompatibility
    {
        MinHostVersion = new Version(1, 0, 0),
        MaxHostVersion = new Version(2, 0, 0)
    },
    DownloadUrl = "https://server.com/extension.zip",
    PackageHash = "sha256-hash",
    PackageSize = 1048576
};
```

## AOT Compatibility

Fully compatible with Native AOT:
- No reflection
- No dynamic code generation
- Statically resolvable types

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

## Architecture

```
Metadata/           - Extension descriptors (VS Code compliant)
Installation/       - Installed extension management
Core/              - Extension catalog
Compatibility/     - Version validation
Download/          - Update queue and downloads
PackageGeneration/ - ZIP package creator
```

## License

Part of the GeneralUpdate project.
