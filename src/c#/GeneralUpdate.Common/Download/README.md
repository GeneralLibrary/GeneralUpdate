# DownloadManager Usage Guide

## Overview

The `DownloadManager` and `DownloadTask` classes provide robust file downloading capabilities with support for:
- **Parallel downloading** of multiple files
- Resume capability for interrupted downloads
- Progress tracking and statistics
- Event-driven architecture for advanced scenarios
- Simplified API for one-time download tasks

## Parallel Downloading

The `DownloadManager` supports parallel downloading out of the box using `Task.WhenAll` in the `LaunchTasksAsync` method. Simply add multiple tasks to the manager, and they will be downloaded concurrently.

### Example: Advanced Usage with Events

```csharp
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Shared.Object;

// Create a download manager
var manager = new DownloadManager(
    path: @"C:\Downloads",
    format: ".zip",
    timeOut: 60
);

// Subscribe to events for progress tracking
manager.MultiDownloadStatistics += (sender, e) =>
{
    Console.WriteLine($"File: {((VersionInfo)e.Version).Name}");
    Console.WriteLine($"Progress: {e.ProgressPercentage:F2}%");
    Console.WriteLine($"Speed: {e.Speed}");
    Console.WriteLine($"Remaining: {e.RemainingTime}");
};

manager.MultiDownloadCompleted += (sender, e) =>
{
    var version = (VersionInfo)e.Version;
    Console.WriteLine($"Download completed: {version.Name} - Success: {e.IsComplated}");
};

manager.MultiAllDownloadCompleted += (sender, e) =>
{
    Console.WriteLine($"All downloads completed: {e.IsAllDownloadCompleted}");
    if (e.FailedVersions.Count > 0)
    {
        Console.WriteLine("Failed downloads:");
        foreach (var (version, error) in e.FailedVersions)
        {
            Console.WriteLine($"  - {version}: {error}");
        }
    }
};

// Add multiple download tasks (will be downloaded in parallel)
manager.Add(new DownloadTask(manager, new VersionInfo
{
    Name = "update-v1.0",
    Url = "https://example.com/updates/v1.0.zip",
    Format = ".zip"
}));

manager.Add(new DownloadTask(manager, new VersionInfo
{
    Name = "update-v1.1",
    Url = "https://example.com/updates/v1.1.zip",
    Format = ".zip"
}));

// Launch all downloads in parallel
await manager.LaunchTasksAsync();
```

## Simplified API for One-Time Downloads

For simple download scenarios where you don't need event handling or progress tracking, use the static helper methods.

### Example: Single File Download

```csharp
using GeneralUpdate.Common.Download;

// Download a single file
bool success = await DownloadManager.DownloadFileAsync(
    url: "https://example.com/file.zip",
    destinationPath: @"C:\Downloads",
    fileName: "myfile",
    format: ".zip",
    timeOut: 60
);

if (success)
{
    Console.WriteLine("Download completed successfully!");
}
else
{
    Console.WriteLine("Download failed.");
}
```

### Example: Multiple Files in Parallel

```csharp
using GeneralUpdate.Common.Download;

// Download multiple files in parallel
var files = new[]
{
    ("https://example.com/file1.zip", "file1"),
    ("https://example.com/file2.zip", "file2"),
    ("https://example.com/file3.zip", "file3")
};

var results = await DownloadManager.DownloadFilesAsync(
    files: files,
    destinationPath: @"C:\Downloads",
    format: ".zip",
    timeOut: 60
);

// Check results
foreach (var (fileName, success) in results)
{
    Console.WriteLine($"{fileName}: {(success ? "Success" : "Failed")}");
}
```

## Features

### Resume Capability
Downloads can be resumed if interrupted. The download manager automatically detects partial downloads and continues from where it left off.

### Progress Tracking
Subscribe to the `MultiDownloadStatistics` event to receive real-time updates on:
- Download progress (percentage)
- Download speed (formatted: B/s, KB/s, MB/s, GB/s)
- Estimated remaining time
- Total and received bytes

### Error Handling
- Individual download errors are captured and reported through the `MultiDownloadError` event
- Failed downloads are tracked in the `FailedVersions` collection
- The simplified API returns boolean success status or a dictionary of results

## Thread Safety

The download manager uses thread-safe operations:
- `Interlocked` operations for atomic updates
- `ImmutableList` for task management
- Parallel execution via `Task.WhenAll`

## Best Practices

1. **Use the simplified API** for simple, one-off downloads
2. **Use the event-driven API** when you need:
   - Real-time progress updates
   - Fine-grained error handling
   - Statistics tracking
3. **Set appropriate timeouts** based on expected file sizes and network conditions
4. **Handle failed downloads** by checking the `FailedVersions` collection or result dictionary
5. **Use parallel downloads** wisely - consider network bandwidth and server limitations
