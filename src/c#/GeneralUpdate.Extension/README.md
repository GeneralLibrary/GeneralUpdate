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

// Create extension host
var host = new ExtensionHost(
    hostVersion: new Version(1, 0, 0),
    installPath: @"C:\MyApp\Extensions",
    downloadPath: @"C:\MyApp\Downloads",
    targetPlatform: TargetPlatform.Windows);

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
        var hostVersion = new Version(1, 0, 0);
        var installPath = @"C:\MyApp\Extensions";
        var downloadPath = @"C:\MyApp\Downloads";
        var platform = Metadata.TargetPlatform.Windows;

        // Register as singletons
        containerRegistry.RegisterSingleton<Core.IExtensionCatalog>(() => 
            new Core.ExtensionCatalog(installPath));
        
        containerRegistry.RegisterSingleton<Compatibility.ICompatibilityValidator>(() => 
            new Compatibility.CompatibilityValidator(hostVersion));
        
        containerRegistry.RegisterSingleton<Download.IUpdateQueue, Download.UpdateQueue>();
        
        containerRegistry.RegisterSingleton<PackageGeneration.IExtensionPackageGenerator, 
            PackageGeneration.ExtensionPackageGenerator>();
        
        containerRegistry.RegisterSingleton<IExtensionHost>(() => 
            new ExtensionHost(hostVersion, installPath, downloadPath, platform));
    }
}

// Resolve services
var host = container.Resolve<IExtensionHost>();
```

#### With Microsoft.Extensions.DependencyInjection

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var hostVersion = new Version(1, 0, 0);
var installPath = @"C:\Extensions";
var downloadPath = @"C:\Downloads";

services.AddSingleton<Core.IExtensionCatalog>(sp => 
    new Core.ExtensionCatalog(installPath));

services.AddSingleton<Compatibility.ICompatibilityValidator>(sp => 
    new Compatibility.CompatibilityValidator(hostVersion));

services.AddSingleton<Download.IUpdateQueue, Download.UpdateQueue>();

services.AddSingleton<PackageGeneration.IExtensionPackageGenerator, 
    PackageGeneration.ExtensionPackageGenerator>();

services.AddSingleton<IExtensionHost>(sp => 
    new ExtensionHost(hostVersion, installPath, downloadPath, 
        Metadata.TargetPlatform.Windows));

var provider = services.BuildServiceProvider();
var host = provider.GetRequiredService<IExtensionHost>();
```

#### Without DI (Direct Instantiation)

```csharp
var host = new ExtensionHost(
    new Version(1, 0, 0),
    @"C:\Extensions",
    @"C:\Downloads",
    Metadata.TargetPlatform.Windows);
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

## Extension Metadata (VS Code Compatible)

```json
{
  "name": "my-extension",
  "displayName": "My Extension",
  "version": "1.0.0",
  "publisher": "publisher-name",
  "engines": {
    "minHostVersion": "1.0.0",
    "maxHostVersion": "2.0.0"
  },
  "categories": ["Programming Languages"],
  "supportedPlatforms": 7,
  "contentType": 0
}
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
