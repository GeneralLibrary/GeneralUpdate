# GeneralUpdate.Extension

The GeneralUpdate.Extension module provides a comprehensive plugin/extension update system similar to VS Code's extension system. It supports extension management, version compatibility checking, automatic updates, download queuing, and rollback capabilities.

## Features

### Core Capabilities

1. **Extension List Management**
   - Retrieve local and remote extension lists
   - Platform-specific filtering (Windows/Linux/macOS)
   - JSON-based extension metadata

2. **Version Compatibility**
   - Client-extension version compatibility checking
   - Automatic matching of compatible extension versions
   - Support for min/max version ranges

3. **Update Control**
   - Queue-based update system
   - Auto-update settings (global and per-extension)
   - Manual update selection

4. **Download Queue and Events**
   - Asynchronous download queue management
   - Update status tracking: Queued, Updating, UpdateSuccessful, UpdateFailed
   - Event notifications for status changes and progress

5. **Installation and Rollback**
   - Automatic installation from packages
   - Differential patching support using `DifferentialCore.Dirty`
   - Rollback capability on installation failure

6. **Platform Adaptation**
   - Multi-platform support (Windows/Linux/macOS)
   - Platform-specific extension filtering
   - Flags-based platform specification

7. **Extension Content Types**
   - JavaScript, Lua, Python
   - WebAssembly
   - External Executable
   - Native Library
   - Custom/Other types

## Architecture

### Key Components

```
GeneralUpdate.Extension/
├── Models/                        # Data models
│   ├── ExtensionMetadata.cs      # Universal extension metadata structure
│   ├── ExtensionPlatform.cs      # Platform enumeration
│   ├── ExtensionContentType.cs   # Content type enumeration
│   ├── VersionCompatibility.cs   # Version compatibility model
│   ├── LocalExtension.cs         # Local extension model
│   ├── RemoteExtension.cs        # Remote extension model
│   ├── ExtensionUpdateStatus.cs  # Update status enumeration
│   └── ExtensionUpdateQueueItem.cs # Queue item model
├── Events/                        # Event definitions
│   └── ExtensionEventArgs.cs     # All event args classes
├── Services/                      # Core services
│   ├── ExtensionListManager.cs   # Extension list management
│   ├── VersionCompatibilityChecker.cs # Version checking
│   ├── ExtensionDownloader.cs    # Download handling
│   └── ExtensionInstaller.cs     # Installation & rollback
├── Queue/                         # Queue management
│   └── ExtensionUpdateQueue.cs   # Update queue manager
└── ExtensionManager.cs           # Main orchestrator
```

## Usage

### Basic Setup

```csharp
using GeneralUpdate.Extension;
using GeneralUpdate.Extension.Models;
using System;

// Initialize the ExtensionManager
var clientVersion = new Version(1, 0, 0);
var installPath = @"C:\MyApp\Extensions";
var downloadPath = @"C:\MyApp\Downloads";
var currentPlatform = ExtensionPlatform.Windows;

var manager = new ExtensionManager(
    clientVersion, 
    installPath, 
    downloadPath, 
    currentPlatform);

// Subscribe to events
manager.UpdateStatusChanged += (sender, args) =>
{
    Console.WriteLine($"Extension {args.ExtensionName} status: {args.NewStatus}");
};

manager.DownloadProgress += (sender, args) =>
{
    Console.WriteLine($"Download progress: {args.Progress:F2}%");
};

manager.InstallCompleted += (sender, args) =>
{
    Console.WriteLine($"Installation {(args.IsSuccessful ? "succeeded" : "failed")}");
};
```

### Loading Local Extensions

```csharp
// Load locally installed extensions
manager.LoadLocalExtensions();

// Get all local extensions
var localExtensions = manager.GetLocalExtensions();

// Get local extensions for current platform only
var platformExtensions = manager.GetLocalExtensionsForCurrentPlatform();

// Get a specific extension
var extension = manager.GetLocalExtensionById("my-extension-id");
```

### Working with Remote Extensions

```csharp
// Parse remote extensions from JSON
string remoteJson = await FetchRemoteExtensionsJson();
var remoteExtensions = manager.ParseRemoteExtensions(remoteJson);

// Get only compatible extensions
var compatibleExtensions = manager.GetCompatibleRemoteExtensions(remoteExtensions);

// Find the best upgrade version for a specific extension
var bestVersion = manager.FindBestUpgradeVersion("my-extension-id", remoteExtensions);
```

### Managing Updates

```csharp
// Queue a specific extension for update
var queueItem = manager.QueueExtensionUpdate(remoteExtension, enableRollback: true);

// Queue all auto-updates
var queuedUpdates = manager.QueueAutoUpdates(remoteExtensions);

// Process updates one by one
bool updated = await manager.ProcessNextUpdateAsync();

// Or process all queued updates
await manager.ProcessAllUpdatesAsync();

// Check the update queue
var allItems = manager.GetUpdateQueue();
var queuedItems = manager.GetUpdateQueueByStatus(ExtensionUpdateStatus.Queued);
var failedItems = manager.GetUpdateQueueByStatus(ExtensionUpdateStatus.UpdateFailed);

// Clear completed updates from queue
manager.ClearCompletedUpdates();
```

### Auto-Update Configuration

```csharp
// Set global auto-update
manager.GlobalAutoUpdateEnabled = true;

// Enable/disable auto-update for specific extension
manager.SetExtensionAutoUpdate("my-extension-id", true);

// Check auto-update status
bool isEnabled = manager.GetExtensionAutoUpdate("my-extension-id");
```

### Version Compatibility

```csharp
// Check if an extension is compatible
bool compatible = manager.IsExtensionCompatible(metadata);

// Get client version
var version = manager.ClientVersion;

// Get current platform
var platform = manager.CurrentPlatform;
```

## Extension Metadata Structure

```json
{
  "id": "my-extension",
  "name": "My Extension",
  "version": "1.0.0",
  "description": "A sample extension",
  "author": "John Doe",
  "license": "MIT",
  "supportedPlatforms": 7,
  "contentType": 0,
  "compatibility": {
    "minClientVersion": "1.0.0",
    "maxClientVersion": "2.0.0"
  },
  "downloadUrl": "https://example.com/extensions/my-extension-1.0.0.zip",
  "hash": "sha256-hash-value",
  "size": 1048576,
  "releaseDate": "2024-01-01T00:00:00Z",
  "dependencies": ["other-extension-id"],
  "properties": {
    "customKey": "customValue"
  }
}
```

### Platform Values (Flags)

- `None` = 0
- `Windows` = 1
- `Linux` = 2
- `macOS` = 4
- `All` = 7 (Windows | Linux | macOS)

### Content Type Values

- `JavaScript` = 0
- `Lua` = 1
- `Python` = 2
- `WebAssembly` = 3
- `ExternalExecutable` = 4
- `NativeLibrary` = 5
- `Other` = 99

## Events

The extension system provides comprehensive event notifications:

- **UpdateStatusChanged**: Fired when an extension update status changes
- **DownloadProgress**: Fired during download with progress information
- **DownloadCompleted**: Fired when a download completes successfully
- **DownloadFailed**: Fired when a download fails
- **InstallCompleted**: Fired when installation completes (success or failure)
- **RollbackCompleted**: Fired when rollback completes

## Integration with GeneralUpdate Components

### DownloadManager Integration

The extension system uses `GeneralUpdate.Common.Download.DownloadManager` for all downloads:

```csharp
// ExtensionDownloader automatically creates and manages DownloadManager
// No direct usage required - handled internally
```

### DifferentialCore Integration

For patch-based updates, the system uses `GeneralUpdate.Differential.DifferentialCore`:

```csharp
// Apply differential patches during installation
await installer.ApplyPatchAsync(patchPath, metadata, enableRollback: true);
```

## Error Handling and Rollback

The system provides automatic rollback on installation failure:

```csharp
// Rollback is enabled by default
var queueItem = manager.QueueExtensionUpdate(extension, enableRollback: true);

// Or disable rollback if needed
var queueItem = manager.QueueExtensionUpdate(extension, enableRollback: false);
```

## Best Practices

1. **Always check compatibility** before queueing an update
2. **Enable rollback** for production systems
3. **Subscribe to events** to monitor update progress
4. **Handle failures gracefully** by checking update status
5. **Use platform filtering** to show only relevant extensions
6. **Clear completed updates** periodically to manage memory
7. **Validate extension metadata** before installation

## Requirements

- .NET Standard 2.0 or later
- System.Text.Json 10.0.1 or later
- GeneralUpdate.Common (for DownloadManager)
- GeneralUpdate.Differential (for patch support)

## License

This module is part of the GeneralUpdate project.
