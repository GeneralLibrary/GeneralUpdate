# Plugin Update System - Design Document

## Executive Summary

This document provides a comprehensive design for the plugin update functionality in the GeneralUpdate.Extension project, inspired by VS Code's extension system. The system focuses exclusively on plugin update capabilities, providing clear interfaces, event-driven architecture, and seamless integration with existing GeneralUpdate components.

## 1. Architecture Overview

### 1.1 System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                    Client Application                            │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                  PluginUpdateService                             │
│  (Orchestration, Auto-Update Control, Manual Updates)           │
└──────┬──────────────────┬──────────────────┬────────────────────┘
       │                  │                  │
       ▼                  ▼                  ▼
┌─────────────┐   ┌──────────────┐   ┌─────────────┐
│   Plugin    │   │  Download    │   │   Plugin    │
│  Registry   │   │    Queue     │   │  Installer  │
└─────────────┘   └──────────────┘   └─────────────┘
       │                  │                  │
       │                  ▼                  │
       │          ┌──────────────┐           │
       │          │DownloadManager│          │
       │          │(GeneralUpdate │          │
       │          │   .Common)    │          │
       │          └──────────────┘           │
       │                                     ▼
       │                            ┌─────────────────┐
       │                            │ DifferentialCore│
       │                            │(Dirty Function) │
       │                            │(GeneralUpdate   │
       │                            │ .Differential)  │
       │                            └─────────────────┘
       │
       ▼
┌─────────────────────────────────────────────────────────────────┐
│                     UpdateEventBus                               │
│              (Event Distribution & Notifications)                │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Data Flow

```
1. Check for Updates
   Client → PluginUpdateService.CheckAndUpdateAsync()
   → PluginRegistry.CheckForUpdatesAsync()
   → Compare local vs server versions
   → Mark plugins with UpdateAvailable flag

2. Trigger Update (Manual/Auto)
   Client → PluginUpdateService.UpdatePluginAsync(pluginId)
   → DownloadQueue.EnqueueAsync(plugin)
   → Status: Queued → Event Published

3. Download Phase
   DownloadQueue processes queue
   → Create DownloadManager + DownloadTask
   → Status: Downloading → Events with Progress
   → Download completes → Status: Downloaded

4. Installation Phase
   DownloadQueue → PluginInstaller.UpdateAsync()
   → Backup current version (PluginInstaller.BackupAsync)
   → Extract package to temp directory
   → Apply differential patch (DifferentialCore.Dirty)
   → Update plugin metadata
   → Status: UpdateSucceeded → Event Published

5. Rollback (if needed)
   Client → PluginUpdateService.RollbackPluginAsync(pluginId)
   → PluginInstaller.RestoreAsync()
   → Extract backup to install directory
   → Status: Idle → Event Published
```

## 2. Module Responsibilities

### 2.1 PluginRegistry

**Responsibility**: Manages plugin metadata, tracks installation state, and performs version comparison.

**Key Functions**:
- `GetLocalPluginsAsync()`: Returns all installed plugins
- `GetServerPluginsAsync()`: Fetches available plugins from server/marketplace
- `CheckForUpdatesAsync()`: Compares versions and identifies updates
- `RegisterPluginAsync(plugin)`: Adds plugin to registry
- `UnregisterPluginAsync(pluginId)`: Removes plugin from registry
- `EnablePluginAsync(pluginId)`: Enables a plugin
- `DisablePluginAsync(pluginId)`: Disables a plugin

**Data Storage**: JSON file (`plugin-registry.json`) in plugins directory

**Platform Compatibility**: Uses `PlatformResolver` to filter compatible plugins

### 2.2 PluginUpdateService

**Responsibility**: Orchestrates the entire update process, manages auto-update settings, and coordinates between components.

**Key Functions**:
- `UpdatePluginAsync(pluginId)`: Manually triggers update for a plugin
- `UpdateAllPluginsAsync()`: Updates all plugins with available updates
- `CancelUpdateAsync(pluginId)`: Cancels an ongoing update
- `SetPluginAutoUpdateAsync(pluginId, enabled)`: Configures per-plugin auto-update
- `CheckAndUpdateAsync(autoUpdate)`: Checks for updates and optionally starts them
- `RollbackPluginAsync(pluginId)`: Reverts to previous version
- `GetUpdateStatusAsync(pluginId)`: Returns current update status

**Properties**:
- `GlobalAutoUpdateEnabled`: Controls global auto-update behavior

**Events**:
- `PluginUpdateStatusChanged`: Fired for all status changes

### 2.3 DownloadQueue

**Responsibility**: Manages download queue, integrates with DownloadManager, and tracks download progress.

**Key Functions**:
- `EnqueueAsync(plugin)`: Adds plugin to download queue
- `DequeueAsync(pluginId)`: Removes plugin from queue
- `GetStatus(pluginId)`: Returns current download status
- `StartAsync()`: Begins processing queue
- `StopAsync()`: Stops queue processing
- `ClearQueueAsync()`: Clears all queued downloads

**Queue Processing**: Sequential processing with status tracking

**Events**:
- `PluginQueued`: When plugin is added to queue
- `PluginUpdating`: When download starts
- `PluginUpdateSucceeded`: When download completes successfully
- `PluginUpdateFailed`: When download fails
- `PluginUpdateProgress`: Progress updates during download

**Integration**: Uses `GeneralUpdate.Common.Download.DownloadManager` for actual downloads

### 2.4 PluginInstaller

**Responsibility**: Handles plugin installation, updates using differential patching, and backup/restore operations.

**Key Functions**:
- `InstallAsync(plugin, packagePath)`: Fresh plugin installation
- `UpdateAsync(plugin, patchPath)`: Updates using differential patching
- `UninstallAsync(pluginId)`: Removes plugin
- `BackupAsync(pluginId)`: Creates backup before update
- `RestoreAsync(pluginId, backupPath)`: Restores from backup
- `ValidatePackageAsync(packagePath)`: Validates plugin package

**Backup Strategy**: Keeps last 3 backups per plugin

**Integration**: Uses `GeneralUpdate.Differential.DifferentialCore.Dirty()` for patch application

### 2.5 UpdateEventBus

**Responsibility**: Centralized event distribution using observer pattern.

**Key Functions**:
- `Publish(updateEvent)`: Publishes event to all subscribers
- `Subscribe(handler)`: Subscribes to all events
- `Unsubscribe(handler)`: Unsubscribes from all events
- `SubscribeToPlugin(pluginId, handler)`: Plugin-specific subscriptions
- `SubscribeToStatus(status, handler)`: Status-specific subscriptions

**Thread Safety**: All operations are thread-safe using locks

### 2.6 PlatformResolver

**Responsibility**: Platform detection and compatibility checking.

**Key Functions**:
- `GetCurrentPlatform()`: Returns platform identifier (e.g., "win32-x64")
- `GetOperatingSystem()`: Returns OS ("win32", "darwin", "linux")
- `GetArchitecture()`: Returns CPU arch ("x64", "arm64", "x86", "arm")
- `IsCompatible(pluginPlatform)`: Checks plugin compatibility
- `GetPlatformDisplayName()`: Returns human-readable platform name

**Supported Platforms**:
- Windows: win32-x64, win32-x86, win32-arm64
- macOS: darwin-x64, darwin-arm64
- Linux: linux-x64, linux-arm64, linux-arm
- Universal: "any" or "universal"

### 2.7 VersionComparer

**Responsibility**: Semantic version comparison for update detection.

**Key Functions**:
- `Compare(version1, version2)`: Compares two versions
- `IsGreaterThan(version1, version2)`: Version1 > Version2
- `IsLessThan(version1, version2)`: Version1 < Version2
- `IsEqual(version1, version2)`: Version1 == Version2
- `IsUpdateAvailable(currentVersion, newVersion)`: Checks if update exists

**Version Format**: Supports semantic versioning (MAJOR.MINOR.PATCH[-prerelease][+metadata])

**Examples**:
- "1.2.3" vs "1.2.4" → -1 (update available)
- "2.0.0-alpha" vs "2.0.0" → -1 (prerelease < stable)
- "v1.0.0" vs "1.0.0" → 0 (equal, 'v' prefix ignored)

## 3. Core Interfaces

### 3.1 IPluginRegistry

```csharp
public interface IPluginRegistry
{
    Task<List<PluginInfo>> GetLocalPluginsAsync();
    Task<List<PluginInfo>> GetServerPluginsAsync();
    Task<PluginInfo> GetPluginByIdAsync(string pluginId);
    Task<List<PluginInfo>> CheckForUpdatesAsync();
    Task<PluginInfo> CheckForUpdateAsync(string pluginId);
    Task RegisterPluginAsync(PluginInfo plugin);
    Task UnregisterPluginAsync(string pluginId);
    Task UpdatePluginMetadataAsync(PluginInfo plugin);
    Task EnablePluginAsync(string pluginId);
    Task DisablePluginAsync(string pluginId);
    Task<List<PluginInfo>> GetPluginsByPlatformAsync(string platform);
}
```

### 3.2 IPluginUpdateService

```csharp
public interface IPluginUpdateService
{
    bool GlobalAutoUpdateEnabled { get; set; }
    Task<bool> UpdatePluginAsync(string pluginId);
    Task<int> UpdateAllPluginsAsync();
    Task CancelUpdateAsync(string pluginId);
    Task SetPluginAutoUpdateAsync(string pluginId, bool enabled);
    Task<UpdateStatus> GetUpdateStatusAsync(string pluginId);
    Task<int> CheckAndUpdateAsync(bool autoUpdate = false);
    Task<bool> RollbackPluginAsync(string pluginId);
    event EventHandler<PluginUpdateEvent> PluginUpdateStatusChanged;
}
```

### 3.3 IDownloadQueue

```csharp
public interface IDownloadQueue
{
    Task<bool> EnqueueAsync(PluginInfo plugin);
    Task<bool> DequeueAsync(string pluginId);
    UpdateStatus GetStatus(string pluginId);
    int GetQueueSize();
    Task ClearQueueAsync();
    Task StartAsync();
    Task StopAsync();
    
    event EventHandler<PluginUpdateEvent> PluginQueued;
    event EventHandler<PluginUpdateEvent> PluginUpdating;
    event EventHandler<PluginUpdateEvent> PluginUpdateSucceeded;
    event EventHandler<PluginUpdateEvent> PluginUpdateFailed;
    event EventHandler<PluginUpdateEvent> PluginUpdateProgress;
}
```

### 3.4 IPluginInstaller

```csharp
public interface IPluginInstaller
{
    Task<bool> InstallAsync(PluginInfo plugin, string packagePath);
    Task<bool> UninstallAsync(string pluginId);
    Task<bool> UpdateAsync(PluginInfo plugin, string patchPath);
    Task<string> BackupAsync(string pluginId);
    Task<bool> RestoreAsync(string pluginId, string backupPath);
    Task<bool> ValidatePackageAsync(string packagePath);
}
```

### 3.5 IUpdateEventBus

```csharp
public interface IUpdateEventBus
{
    void Publish(PluginUpdateEvent updateEvent);
    void Subscribe(EventHandler<PluginUpdateEvent> handler);
    void Unsubscribe(EventHandler<PluginUpdateEvent> handler);
    void SubscribeToPlugin(string pluginId, EventHandler<PluginUpdateEvent> handler);
    void UnsubscribeFromPlugin(string pluginId, EventHandler<PluginUpdateEvent> handler);
    void SubscribeToStatus(UpdateStatus status, EventHandler<PluginUpdateEvent> handler);
    void UnsubscribeFromStatus(UpdateStatus status, EventHandler<PluginUpdateEvent> handler);
}
```

## 4. Update Status Flow

```
Idle
  │
  ├─> CheckingForUpdates (checking for available updates)
  │     │
  │     ├─> Idle (no updates)
  │     └─> [UpdateAvailable flag set]
  │
  ├─> Queued (plugin added to download queue)
  │     │
  │     └─> Downloading (download in progress)
  │           │
  │           ├─> Downloaded (download complete)
  │           │     │
  │           │     └─> Installing (applying patches)
  │           │           │
  │           │           ├─> UpdateSucceeded ─> Idle
  │           │           └─> UpdateFailed
  │           │                 │
  │           │                 └─> RollingBack
  │           │                       │
  │           │                       ├─> Idle (rollback succeeded)
  │           │                       └─> UpdateFailed (rollback failed)
  │           │
  │           └─> UpdateFailed (download failed)
  │
  └─> Cancelled (user cancelled update)
```

## 5. Event Notifications

### 5.1 Event Structure

```csharp
public class PluginUpdateEvent
{
    public PluginInfo Plugin { get; set; }
    public UpdateStatus Status { get; set; }
    public double Progress { get; set; }        // 0-100
    public string Message { get; set; }
    public Exception Exception { get; set; }
    public DateTime Timestamp { get; set; }
    public string PreviousVersion { get; set; }
    public string NewVersion { get; set; }
}
```

### 5.2 Event Sequence Example

1. **Queued**: Plugin added to queue
   ```
   Status: Queued
   Message: "Plugin TypeScript queued for update"
   ```

2. **Downloading**: Download started
   ```
   Status: Downloading
   Message: "Downloading plugin TypeScript"
   Progress: 0
   ```

3. **Progress Updates**: During download
   ```
   Status: Downloading
   Message: "Downloading: 45.2%"
   Progress: 45.2
   ```

4. **Installing**: After download completes
   ```
   Status: Installing
   Message: "Installing plugin TypeScript"
   ```

5. **Success**: Update completed
   ```
   Status: UpdateSucceeded
   Message: "Plugin TypeScript updated successfully"
   PreviousVersion: "1.0.0"
   NewVersion: "1.1.0"
   ```

6. **Failure**: If update fails
   ```
   Status: UpdateFailed
   Message: "Failed to install plugin TypeScript: ..."
   Exception: [exception details]
   ```

## 6. Integration Points

### 6.1 GeneralUpdate.Common.Download.DownloadManager

**Usage**: DownloadQueue creates DownloadManager instances to handle downloads.

```csharp
// Create VersionInfo for the plugin
var versionInfo = new VersionInfo
{
    Name = $"{plugin.Id}-{plugin.Version}",
    Url = plugin.DownloadUrl,
    Version = plugin.Version
};

// Create DownloadManager
var downloadManager = new DownloadManager(downloadPath, ".zip", timeoutSeconds);

// Create and add DownloadTask
var downloadTask = new DownloadTask(downloadManager, versionInfo);
downloadManager.Add(downloadTask);

// Subscribe to events
downloadManager.MultiDownloadStatistics += OnDownloadProgress;
downloadManager.MultiAllDownloadCompleted += OnDownloadComplete;
downloadManager.MultiDownloadError += OnDownloadError;

// Start download
await downloadManager.LaunchTasksAsync();
```

### 6.2 GeneralUpdate.Differential.DifferentialCore

**Usage**: PluginInstaller uses the Dirty function for differential patching.

```csharp
// Extract patch package to temporary directory
var tempPatchDir = Path.Combine(Path.GetTempPath(), $"plugin-patch-{Guid.NewGuid()}");
ZipFile.ExtractToDirectory(patchPath, tempPatchDir);

// Apply differential patch
await DifferentialCore.Instance.Dirty(installPath, tempPatchDir);

// The Dirty function:
// - Applies binary patches (.patch files)
// - Handles file deletions (generalupdate_delete_files.json)
// - Copies new files that aren't patches
```

## 7. Configuration & Customization

### 7.1 Component Initialization

```csharp
// Initialize with custom paths
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
```

### 7.2 Auto-Update Configuration

```csharp
// Global auto-update
pluginUpdateService.GlobalAutoUpdateEnabled = true;

// Per-plugin auto-update
await pluginUpdateService.SetPluginAutoUpdateAsync("publisher.plugin", true);

// Scheduled check and update
async Task ScheduledUpdateCheck()
{
    while (true)
    {
        await pluginUpdateService.CheckAndUpdateAsync(autoUpdate: true);
        await Task.Delay(TimeSpan.FromHours(24)); // Check daily
    }
}
```

### 7.3 Server Plugin Retrieval

To implement server-side plugin retrieval, extend PluginRegistry:

```csharp
public class CustomPluginRegistry : PluginRegistry
{
    private readonly HttpClient _httpClient;
    
    public CustomPluginRegistry(string localPluginsPath, HttpClient httpClient)
        : base(localPluginsPath)
    {
        _httpClient = httpClient;
    }
    
    public override async Task<List<PluginInfo>> GetServerPluginsAsync()
    {
        var response = await _httpClient.GetAsync("https://marketplace.example.com/api/plugins");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<PluginInfo>>(json);
    }
}
```

## 8. Security Considerations

### 8.1 Package Validation

- ZIP file integrity check in `PluginInstaller.ValidatePackageAsync()`
- Consider adding: Hash verification, signature validation, size limits

### 8.2 Download Security

- HTTPS for all download URLs
- Timeout configuration (default: 300 seconds)
- Failed download tracking in DownloadManager

### 8.3 Backup & Rollback

- Automatic backup before updates
- Keeps last 3 backups per plugin
- Rollback capability for failed updates

## 9. Error Handling Strategy

### 9.1 Download Failures

- Retry logic can be added to DownloadManager integration
- Failed downloads tracked in `DownloadManager.FailedVersions`
- Events published with exception details

### 9.2 Installation Failures

- Automatic rollback attempted
- Backup preserved for manual recovery
- Detailed error information in events

### 9.3 Rollback Failures

- Status set to UpdateFailed
- Backup remains available for manual restoration
- Error logged and reported via events

## 10. Performance Considerations

### 10.1 Queue Processing

- Sequential download processing (one at a time)
- Can be extended to parallel downloads with concurrency control

### 10.2 File Operations

- Asynchronous I/O for downloads
- Synchronous file operations for .NET Standard 2.0 compatibility
- ZIP extraction uses temporary directories

### 10.3 Memory Management

- Streaming downloads (no full file in memory)
- Cleanup of temporary directories after operations
- Regular backup cleanup (keeps last 3)

## 11. Testing Recommendations

### 11.1 Unit Testing

- Test version comparison logic
- Test platform compatibility checks
- Test event publishing and subscription
- Mock file I/O operations

### 11.2 Integration Testing

- Test full update flow end-to-end
- Test rollback scenarios
- Test concurrent update attempts
- Test network failure scenarios

### 11.3 Platform Testing

- Test on Windows x64, x86
- Test on macOS Intel and Apple Silicon
- Test on Linux x64, ARM

## 12. Future Enhancements

1. **Dependency Resolution**: Automatic installation of plugin dependencies
2. **Conflict Detection**: Identify incompatible plugin combinations
3. **Update Channels**: Support for stable/beta/alpha channels
4. **Delta Updates**: Optimize bandwidth for large plugins
5. **Signature Verification**: Add digital signature validation
6. **Network Resilience**: Implement retry logic with exponential backoff
7. **Parallel Downloads**: Support concurrent plugin downloads
8. **Update Scheduling**: Allow users to schedule update times
9. **Bandwidth Throttling**: Control download speeds
10. **Plugin Marketplace Integration**: Full marketplace search and discovery

## Appendix A: File Structure

```
GeneralUpdate.Extension/
├── Models/
│   ├── PluginInfo.cs              # Plugin metadata
│   ├── PluginStatus.cs            # Status enums
│   └── PluginUpdateEvent.cs       # Event data
├── Platform/
│   └── PlatformResolver.cs        # Platform detection
├── Utils/
│   └── VersionComparer.cs         # Version comparison
├── Interfaces/
│   ├── IPluginRegistry.cs
│   ├── IPluginUpdateService.cs
│   ├── IDownloadQueue.cs
│   ├── IUpdateEventBus.cs
│   └── IPluginInstaller.cs
├── Services/
│   ├── PluginRegistry.cs          # Registry implementation
│   ├── PluginUpdateService.cs     # Update orchestration
│   ├── DownloadQueue.cs           # Download management
│   ├── UpdateEventBus.cs          # Event distribution
│   └── PluginInstaller.cs         # Installation logic
└── README.md                       # User documentation
```

## Appendix B: Plugin Package Structure

```
plugin-package.zip
├── plugin.json                     # Plugin manifest
├── bin/                           # Binaries
│   └── PluginName.dll
├── resources/                     # Resources
│   ├── icons/
│   └── templates/
└── README.md                      # Plugin documentation
```

## Appendix C: Platform Identifiers

| OS      | Architecture | Identifier     |
|---------|-------------|----------------|
| Windows | x64         | win32-x64      |
| Windows | x86         | win32-x86      |
| Windows | ARM64       | win32-arm64    |
| macOS   | x64         | darwin-x64     |
| macOS   | ARM64       | darwin-arm64   |
| Linux   | x64         | linux-x64      |
| Linux   | ARM64       | linux-arm64    |
| Linux   | ARM         | linux-arm      |
| All     | All         | any/universal  |
