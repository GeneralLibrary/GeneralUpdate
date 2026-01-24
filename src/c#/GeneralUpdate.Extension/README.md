# GeneralUpdate.Extension - Plugin Update System

## Overview

GeneralUpdate.Extension provides a comprehensive plugin update system inspired by VS Code's extension architecture. It handles plugin discovery, version management, downloading, installation, and rollback capabilities.

## Architecture

### Core Components

#### 1. **Models** (`GeneralUpdate.Extension.Models`)

- **PluginInfo**: Contains plugin metadata including:
  - Identity (id, name, version, publisher)
  - Platform compatibility (platform, architecture)
  - Status flags (isEnabled, isInstalled, updateAvailable)
  - Dependencies and version constraints

- **PluginStatus**: Enumerates plugin states (NotInstalled, Installed, Disabled, Error, Uninstalling)

- **UpdateStatus**: Tracks update lifecycle states:
  - `Idle` → `CheckingForUpdates` → `Queued` → `Downloading` → `Downloaded` → `Installing` → `UpdateSucceeded`/`UpdateFailed`

- **PluginUpdateEvent**: Event data for status notifications

#### 2. **Platform** (`GeneralUpdate.Extension.Platform`)

- **PlatformResolver**: Detects and validates platform compatibility
  - Supports Windows (win32), macOS (darwin), Linux
  - Handles architecture detection (x64, x86, arm64, arm)
  - Validates plugin compatibility with current platform

#### 3. **Interfaces** (`GeneralUpdate.Extension.Interfaces`)

- **IPluginRegistry**: Plugin metadata and lifecycle management
- **IPluginUpdateService**: Update orchestration and control
- **IDownloadQueue**: Download queue management with status events
- **IUpdateEventBus**: Event distribution system (observer pattern)
- **IPluginInstaller**: Installation, update, and rollback operations

#### 4. **Services** (`GeneralUpdate.Extension.Services`)

Concrete implementations of all interfaces with full functionality.

#### 5. **Utils** (`GeneralUpdate.Extension.Utils`)

- **VersionComparer**: Semantic versioning comparison utilities

## Key Features

### 1. Plugin List Retrieval

```csharp
// Get local plugins
var localPlugins = await pluginRegistry.GetLocalPluginsAsync();

// Get server-side plugins (requires implementation of server API)
var serverPlugins = await pluginRegistry.GetServerPluginsAsync();

// Get plugin by ID
var plugin = await pluginRegistry.GetPluginByIdAsync("publisher.pluginname");

// Filter by platform
var compatiblePlugins = await pluginRegistry.GetPluginsByPlatformAsync("win32-x64");
```

### 2. Update Control

```csharp
// Manual update
await pluginUpdateService.UpdatePluginAsync("publisher.pluginname");

// Update all plugins with updates available
await pluginUpdateService.UpdateAllPluginsAsync();

// Enable global auto-update
pluginUpdateService.GlobalAutoUpdateEnabled = true;

// Enable auto-update for specific plugin
await pluginUpdateService.SetPluginAutoUpdateAsync("publisher.pluginname", true);

// Check for updates and auto-update enabled plugins
await pluginUpdateService.CheckAndUpdateAsync(autoUpdate: true);
```

### 3. Download Queue & Status Events

The download queue processes plugins sequentially with the following status transitions:

**Status Flow**: `Queued` → `Downloading` → `Installing` → `UpdateSucceeded`/`UpdateFailed`

```csharp
// Subscribe to events
pluginUpdateService.PluginUpdateStatusChanged += (sender, e) =>
{
    Console.WriteLine($"Plugin: {e.Plugin.Name}");
    Console.WriteLine($"Status: {e.Status}");
    Console.WriteLine($"Progress: {e.Progress}%");
    Console.WriteLine($"Message: {e.Message}");
};

// Or use the event bus for more granular control
eventBus.SubscribeToPlugin("publisher.pluginname", (sender, e) =>
{
    // Handle events for specific plugin
});

eventBus.SubscribeToStatus(UpdateStatus.UpdateSucceeded, (sender, e) =>
{
    // Handle only success events
});
```

### 4. Download Integration

The system integrates with **GeneralUpdate.Common.Download.DownloadManager** for reliable downloads:

```csharp
// DownloadQueue internally uses DownloadManager
var downloadQueue = new DownloadQueue(downloadPath, timeoutSeconds: 300);
await downloadQueue.StartAsync();

// Subscribe to download events
downloadQueue.PluginQueued += OnPluginQueued;
downloadQueue.PluginUpdating += OnPluginUpdating;
downloadQueue.PluginUpdateSucceeded += OnPluginUpdateSucceeded;
downloadQueue.PluginUpdateFailed += OnPluginUpdateFailed;
downloadQueue.PluginUpdateProgress += OnPluginUpdateProgress;
```

### 5. Installation & Patch Restoration

The installer uses **GeneralUpdate.Differential.Dirty** for efficient patch-based updates:

```csharp
var installer = new PluginInstaller(pluginsBasePath, pluginRegistry);

// Fresh install
await installer.InstallAsync(plugin, packagePath);

// Differential update (uses Dirty function)
await installer.UpdateAsync(plugin, patchPath);

// Backup before update
var backupPath = await installer.BackupAsync(pluginId);

// Rollback to previous version
await installer.RestoreAsync(pluginId, backupPath);

// Uninstall
await installer.UninstallAsync(pluginId);
```

### 6. Platform Adaptation

```csharp
var platformResolver = PlatformResolver.Instance;

// Get current platform
var currentPlatform = platformResolver.GetCurrentPlatform(); // e.g., "win32-x64"

// Check compatibility
var isCompatible = platformResolver.IsCompatible("darwin-arm64"); // false on Windows

// Get display name
var displayName = platformResolver.GetPlatformDisplayName(); // e.g., "Windows 64-bit"
```

### 7. Version Comparison

```csharp
// Compare versions
var result = VersionComparer.Compare("1.2.3", "1.2.4"); // -1 (first is older)

// Check for updates
var hasUpdate = VersionComparer.IsUpdateAvailable("1.0.0", "1.1.0"); // true

// Semantic versioning support
VersionComparer.IsGreaterThan("2.0.0", "1.9.9"); // true
VersionComparer.IsEqual("1.0.0", "v1.0.0"); // true (leading 'v' is ignored)

// Prerelease handling
VersionComparer.Compare("1.0.0-alpha", "1.0.0"); // -1 (prerelease < stable)
```

## Complete Usage Example

```csharp
using GeneralUpdate.Extension.Services;
using GeneralUpdate.Extension.Models;
using GeneralUpdate.Extension.Platform;

// Initialize components
var pluginsBasePath = @"C:\MyApp\plugins";
var downloadPath = @"C:\MyApp\downloads";

var pluginRegistry = new PluginRegistry(pluginsBasePath);
var downloadQueue = new DownloadQueue(downloadPath, timeoutSeconds: 300);
var pluginInstaller = new PluginInstaller(pluginsBasePath, pluginRegistry);
var eventBus = new UpdateEventBus();

var pluginUpdateService = new PluginUpdateService(
    pluginRegistry,
    downloadQueue,
    pluginInstaller,
    eventBus
);

// Start the download queue
await downloadQueue.StartAsync();

// Subscribe to events
pluginUpdateService.PluginUpdateStatusChanged += (sender, e) =>
{
    Console.WriteLine($"[{e.Status}] {e.Plugin.Name}: {e.Message}");
    
    if (e.Status == UpdateStatus.Downloading)
    {
        Console.WriteLine($"Progress: {e.Progress:F1}%");
    }
    else if (e.Status == UpdateStatus.UpdateFailed)
    {
        Console.WriteLine($"Error: {e.Exception?.Message}");
    }
};

// Enable auto-update
pluginUpdateService.GlobalAutoUpdateEnabled = true;

// Check for updates and auto-update
var updateCount = await pluginUpdateService.CheckAndUpdateAsync(autoUpdate: true);
Console.WriteLine($"{updateCount} plugins queued for update");

// Or manually update specific plugin
await pluginUpdateService.UpdatePluginAsync("publisher.pluginname");

// Get update status
var status = await pluginUpdateService.GetUpdateStatusAsync("publisher.pluginname");
Console.WriteLine($"Current status: {status}");

// Rollback if needed
if (status == UpdateStatus.UpdateFailed)
{
    await pluginUpdateService.RollbackPluginAsync("publisher.pluginname");
}

// Clean up
await downloadQueue.StopAsync();
```

## Update Flow Diagram

```
1. Check for Updates
   ├─> GetLocalPluginsAsync()
   ├─> GetServerPluginsAsync()
   └─> Compare versions → Mark UpdateAvailable

2. Manual/Auto Update Trigger
   └─> UpdatePluginAsync(pluginId)

3. Download Phase
   ├─> Enqueue to DownloadQueue
   ├─> Status: Queued → Event
   ├─> Status: Downloading → Events with Progress
   └─> Status: Downloaded → Event

4. Installation Phase
   ├─> Status: Installing → Event
   ├─> BackupAsync() (create backup)
   ├─> UpdateAsync() (apply patch using Dirty)
   └─> Status: UpdateSucceeded/UpdateFailed → Event

5. Post-Update
   ├─> Update plugin metadata
   └─> Cleanup temporary files

6. Rollback (if needed)
   ├─> Status: RollingBack → Event
   ├─> RestoreAsync() (restore from backup)
   └─> Status: Idle/UpdateFailed → Event
```

## Plugin Metadata Example

```json
{
  "id": "microsoft.typescript",
  "name": "TypeScript Support",
  "version": "1.2.3",
  "availableVersion": "1.3.0",
  "publisher": "Microsoft",
  "description": "TypeScript language support",
  "platform": "any",
  "architecture": "any",
  "isEnabled": true,
  "isInstalled": true,
  "updateAvailable": true,
  "autoUpdateEnabled": true,
  "downloadUrl": "https://marketplace.example.com/typescript-1.3.0.zip",
  "installPath": "C:\\MyApp\\plugins\\microsoft.typescript",
  "dependencies": ["microsoft.vscode-api"],
  "minHostVersion": "1.0.0",
  "maxHostVersion": "2.0.0",
  "lastUpdated": "2024-01-15T10:30:00Z"
}
```

## Error Handling

The system provides comprehensive error handling:

- **Download failures**: Tracked via `UpdateStatus.UpdateFailed` events
- **Installation errors**: Automatic rollback attempt
- **Backup failures**: Logged but don't block updates
- **Validation errors**: Package validation before installation

All errors are propagated through events with exception details.

## Thread Safety

All service implementations are thread-safe:
- Registry operations use locking
- Download queue uses thread-safe collections
- Event bus uses synchronized event handling

## Extension Points

To implement server plugin retrieval, override `GetServerPluginsAsync` in `PluginRegistry`:

```csharp
public override async Task<List<PluginInfo>> GetServerPluginsAsync()
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync("https://your-marketplace-api.com/plugins");
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<List<PluginInfo>>(json);
}
```

## Dependencies

- **GeneralUpdate.Common**: Download management
- **GeneralUpdate.Differential**: Patch application (Dirty function)
- **System.Text.Json**: JSON serialization
- **.NET Standard 2.0**: Cross-platform compatibility

## Future Enhancements

- Dependency resolution and installation
- Plugin conflict detection
- Update scheduling
- Network resilience (retry logic)
- Delta updates for large plugins
- Plugin signature verification
- Update channels (stable, beta, alpha)
