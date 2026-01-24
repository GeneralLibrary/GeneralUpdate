# GeneralUpdate.Extension

A production-ready VS Code-compliant extension/plugin update system with version compatibility, automatic updates, download queuing, rollback capabilities, and package generation.

## Features

- ✅ **VS Code Standard Compliance** - Extension metadata follows VS Code package.json structure
- ✅ **Dependency Injection** - Full Prism and Microsoft.Extensions.DependencyInjection support
- ✅ **Multi-Platform** - Windows, Linux, macOS with platform-specific filtering
- ✅ **Version Compatibility** - Min/max host version validation and automatic matching
- ✅ **Update Queue** - Thread-safe queue with state tracking and event notifications
- ✅ **Automatic Updates** - Global and per-extension auto-update settings
- ✅ **Rollback Support** - Automatic backup and restoration on installation failure
- ✅ **Package Generation** - Create extension packages from source directories
- ✅ **AOT Compatible** - No reflection, supports Native AOT compilation
- ✅ **Minimal Dependencies** - Only System.Text.Json required (beyond framework)

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

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddExtensionSystem(
    new Version(1, 0, 0),
    @"C:\Extensions",
    @"C:\Downloads",
    Metadata.TargetPlatform.Windows);

var host = provider.GetRequiredService<IExtensionHost>();
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
