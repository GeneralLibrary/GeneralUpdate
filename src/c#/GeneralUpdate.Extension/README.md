# GeneralUpdate.Extension

A VS Code-inspired extension system for .NET applications with update, download, installation, rollback, and compatibility management capabilities.

## Overview

This library provides a comprehensive extension management system inspired by VS Code's extension architecture. It includes:

- **Extension Catalog**: Local extension management with JSON persistence
- **Remote Extension Query**: Server-side extension discovery and querying
- **Download Queue**: Managed download queue with concurrent download support
- **Version Compatibility**: Automatic version compatibility checking
- **Platform Support**: Multi-platform support (Windows, Linux, macOS)
- **Dependency Resolution**: Automatic dependency detection and installation
- **Rollback Mechanism**: Safe installation with automatic rollback on failure
- **Event Notifications**: Real-time status updates for extension operations

## Architecture

### Core Components

1. **GeneralExtensionHost**: Main entry point and container for all extension functionality
2. **ExtensionCatalog**: Manages locally installed extensions
3. **ExtensionHttpClient**: Handles HTTP communication with extension server
4. **VersionCompatibilityChecker**: Validates extension compatibility with host version
5. **DownloadQueueManager**: Manages download queue and concurrent downloads
6. **DependencyResolver**: Resolves extension dependencies
7. **PlatformMatcher**: Platform detection and matching

### Models

- **ExtensionMetadata**: Core extension metadata model
- **ExtensionDTO**: Data transfer object for server communication
- **ExtensionQueryDTO**: Query parameters for server requests
- **PagedResultDTO<T>**: Paged result wrapper
- **HttpResponseDTO<T>**: HTTP response wrapper

### Enums

- **TargetPlatform**: Platform flags (Windows, Linux, macOS, All)
- **ExtensionUpdateStatus**: Update status (Queued, Updating, UpdateSuccessful, UpdateFailed)

## Usage

### Initialization

```csharp
using GeneralUpdate.Extension;
using GeneralUpdate.Extension.Enums;
using GeneralUpdate.Extension.Models;

// Initialize the extension host with configuration options
var options = new ExtensionHostOptions
{
    ServerUrl = "https://extensions.example.com/api",
    BearerToken = "your-bearer-token",
    HostVersion = "1.0.0",
    ExtensionsDirectory = "/path/to/extensions",
    CatalogPath = "/path/to/extensions/catalog.json" // Optional
};

var host = new GeneralExtensionHost(options);

// Subscribe to update events
host.ExtensionUpdateStatusChanged += (sender, e) =>
{
    Console.WriteLine($"Extension: {e.ExtensionName}");
    Console.WriteLine($"Status: {e.Status}");
    Console.WriteLine($"Progress: {e.Progress}%");
    if (e.Status == ExtensionUpdateStatus.UpdateFailed)
    {
        Console.WriteLine($"Error: {e.ErrorMessage}");
    }
};
```

### Query Extensions from Server

```csharp
var query = new ExtensionQueryDTO
{
    Name = "my-extension",
    Platform = TargetPlatform.Windows,
    HostVersion = "1.0.0",
    Status = true, // Only enabled extensions
    PageNumber = 1,
    PageSize = 20
};

var result = await host.QueryExtensionsAsync(query);
if (result.Success && result.Data != null)
{
    foreach (var ext in result.Data.Items)
    {
        Console.WriteLine($"{ext.DisplayName} v{ext.Version}");
        Console.WriteLine($"Compatible: {ext.IsCompatible}");
    }
}
```

### List Installed Extensions

```csharp
// Get all installed extensions
var installed = host.ExtensionCatalog.GetInstalledExtensions();

// Get extensions by platform
var windowsExtensions = host.ExtensionCatalog
    .GetInstalledExtensionsByPlatform(TargetPlatform.Windows);

// Get specific extension
var extension = host.ExtensionCatalog
    .GetInstalledExtensionById("extension-guid");
```

### Update Extension

```csharp
// Update a specific extension
var success = await host.UpdateExtensionAsync("extension-guid");

if (success)
{
    Console.WriteLine("Extension updated successfully");
}
```

### Download Extension

```csharp
var savePath = "/path/to/save/extension.zip";
var success = await host.DownloadExtensionAsync("extension-guid", savePath);
```

### Install Extension

```csharp
// Install with automatic rollback on failure
var success = await host.InstallExtensionAsync(
    extensionPath: "/path/to/extension.zip",
    rollbackOnFailure: true
);
```

### Check Compatibility

```csharp
var extension = host.ExtensionCatalog.GetInstalledExtensionById("extension-guid");
if (extension != null)
{
    var isCompatible = host.IsExtensionCompatible(extension);
    Console.WriteLine($"Compatible: {isCompatible}");
}
```

### Auto-Update Settings

```csharp
// Enable auto-update for specific extension
host.SetAutoUpdate("extension-guid", true);

// Enable global auto-update
host.SetGlobalAutoUpdate(true);

// Check if auto-update is enabled
var autoUpdateEnabled = host.IsAutoUpdateEnabled("extension-guid");
```

## Extension Metadata

Each extension must define metadata including:

- **Id**: Unique identifier (GUID)
- **Name**: Unique name (lowercase, no spaces)
- **DisplayName**: Human-readable name
- **Version**: Semantic version
- **Publisher**: Publisher identifier
- **Description**: Extension description
- **SupportedPlatforms**: Platform flags
- **MinHostVersion**: Minimum compatible host version
- **MaxHostVersion**: Maximum compatible host version
- **Dependencies**: Comma-separated list of dependency IDs
- **Format**: File format (.dll, .zip, etc.)
- **Categories**: Comma-separated categories
- **IsPreRelease**: Pre-release flag

## Server API Requirements

The extension system expects the following server endpoints:

### Query Extensions
```
GET /extensions
Content-Type: application/json
Authorization: Bearer {token}

Body: ExtensionQueryDTO
Response: HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
```

### Download Extension
```
GET /extensions/{id}
Authorization: Bearer {token}

Response: File stream with support for range requests (resume capability)
```

## Version Compatibility

The system automatically checks version compatibility:

1. Extension's `MinHostVersion` must be <= Host version
2. Extension's `MaxHostVersion` must be >= Host version
3. Both constraints must be satisfied for compatibility

Example:
- Host version: 1.5.0
- Extension MinHostVersion: 1.0.0
- Extension MaxHostVersion: 2.0.0
- Result: Compatible ✓

## Platform Support

Extensions declare supported platforms using flags:

```csharp
// Windows only
SupportedPlatforms = TargetPlatform.Windows

// Windows and Linux
SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux

// All platforms
SupportedPlatforms = TargetPlatform.All
```

The system automatically detects the current platform and filters extensions accordingly.

## Dependency Resolution

The system automatically:

1. Resolves all transitive dependencies
2. Detects circular dependencies
3. Downloads and installs dependencies in correct order
4. Prevents installation if dependencies are missing

## Event System

Subscribe to `ExtensionUpdateStatusChanged` to receive real-time updates:

```csharp
host.ExtensionUpdateStatusChanged += (sender, e) =>
{
    switch (e.Status)
    {
        case ExtensionUpdateStatus.Queued:
            Console.WriteLine("Extension queued for update");
            break;
        case ExtensionUpdateStatus.Updating:
            Console.WriteLine($"Updating... {e.Progress}%");
            break;
        case ExtensionUpdateStatus.UpdateSuccessful:
            Console.WriteLine("Update successful!");
            break;
        case ExtensionUpdateStatus.UpdateFailed:
            Console.WriteLine($"Update failed: {e.ErrorMessage}");
            break;
    }
};
```

## Rollback Mechanism

When `rollbackOnFailure` is enabled during installation:

1. System creates a backup of existing extension
2. Attempts installation
3. On success: Backup is deleted
4. On failure: Backup is restored automatically

## Download Queue

Downloads are queued and processed with:

- Configurable concurrent download limit (default: 3)
- Automatic status tracking
- Event notifications on status changes
- Support for resume (HTTP range requests)

## Error Handling

All operations return boolean success indicators and fire events with error details on failure. Always check:

1. Return value for operation success
2. Event arguments for detailed error messages
3. HTTP response status and messages

## Best Practices

1. Always subscribe to events before starting operations
2. Check compatibility before installing extensions
3. Enable rollback for production installations
4. Use appropriate page sizes for queries
5. Store bearer tokens securely
6. Validate extension metadata before installation
7. Monitor disk space for downloads and backups

## Thread Safety

The following components are thread-safe:

- ExtensionCatalog (uses internal locking)
- DownloadQueueManager (uses internal locking)
- GeneralExtensionHost (event handlers should be thread-safe)

## License

See LICENSE file in the repository root.
