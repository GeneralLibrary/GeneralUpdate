# Download API Usage Guide

## Overview

GeneralUpdate provides two approaches for downloading files:

1. **DownloadClient** - A simplified, easy-to-use API for common download scenarios
2. **DownloadManager/DownloadTask** - A flexible, event-driven API for advanced scenarios

Both support:
- **Parallel downloading** of multiple files
- Resume capability for interrupted downloads
- Progress tracking and statistics

## DownloadClient (Recommended for Most Use Cases)

The `DownloadClient` class provides a clean, straightforward API for downloading files without managing events or tasks manually.

# Download API Usage Guide

## Overview

GeneralUpdate provides two approaches for downloading files:

1. **DownloadClient** - A simplified, easy-to-use API for common download scenarios
2. **DownloadManager/DownloadTask** - A flexible, event-driven API for advanced scenarios

Both support:
- **Parallel downloading** of multiple files
- Resume capability for interrupted downloads
- Progress tracking and statistics

## DownloadClient (Recommended for Most Use Cases)

The `DownloadClient` class provides a clean, straightforward API for downloading files without managing events or tasks manually.

### Basic Usage - Single File Download

```csharp
using GeneralUpdate.Common.Download;

// Create a download client
var client = new DownloadClient(
    destinationPath: @"C:\Downloads",
    format: ".zip",
    timeoutSeconds: 60
);

// Download a single file
var result = await client.DownloadAsync(
    url: "https://example.com/file.zip",
    fileName: "myfile"
);

if (result.Success)
{
    Console.WriteLine($"Downloaded: {result.FileName}");
}
else
{
    Console.WriteLine($"Failed: {result.Error}");
}
```

### Parallel Downloads - Multiple Files

```csharp
using GeneralUpdate.Common.Download;

var client = new DownloadClient(@"C:\Downloads", ".zip");

// Create download requests
var requests = new[]
{
    new DownloadRequest("https://example.com/file1.zip", "update-v1.0"),
    new DownloadRequest("https://example.com/file2.zip", "update-v1.1"),
    new DownloadRequest("https://example.com/file3.zip", "update-v1.2")
};

// Download all files in parallel
var results = await client.DownloadAsync(requests);

// Check results
foreach (var result in results)
{
    Console.WriteLine($"{result.FileName}: {(result.Success ? "✓" : "✗ " + result.Error)}");
}
```

### Download with Progress Tracking

```csharp
using GeneralUpdate.Common.Download;

var client = new DownloadClient(@"C:\Downloads", ".zip");

var requests = new[]
{
    new DownloadRequest("https://example.com/file1.zip", "file1"),
    new DownloadRequest("https://example.com/file2.zip", "file2")
};

// Download with progress callback
var results = await client.DownloadWithProgressAsync(requests, progress =>
{
    Console.WriteLine($"{progress.FileName}: {progress.ProgressPercentage:F1}% " +
                     $"({progress.Speed}) - ETA: {progress.RemainingTime:mm\\:ss}");
});

// Results available after completion
foreach (var result in results)
{
    Console.WriteLine($"{result.FileName}: {(result.Success ? "Complete" : result.Error)}");
}
```

## DownloadManager/DownloadTask (Advanced Usage)

For scenarios requiring fine-grained control over the download process, use the `DownloadManager` and `DownloadTask` classes directly.

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

## Comparison: DownloadClient vs DownloadManager

| Feature | DownloadClient | DownloadManager |
|---------|---------------|----------------|
| **Ease of Use** | ✓ Simple API | Requires event setup |
| **Single File Download** | ✓ One method call | Need to create tasks |
| **Parallel Downloads** | ✓ Built-in | ✓ Built-in |
| **Progress Tracking** | ✓ Optional callback | ✓ Event-based |
| **Error Handling** | ✓ Return values | Event-based |
| **Fine-grained Control** | Limited | ✓ Full control |

## Recommendation

- **Use DownloadClient** for straightforward download scenarios
- **Use DownloadManager** when you need:
  - Custom event handling
  - Integration with existing event-driven code
  - Maximum control over the download process

## Features

### Resume Capability
Both APIs support resuming interrupted downloads automatically. The download system detects partial downloads and continues from where it left off.

### Progress Tracking
- **DownloadClient**: Use `DownloadWithProgressAsync` with a callback
- **DownloadManager**: Subscribe to `MultiDownloadStatistics` event

Progress information includes:
- Download progress (percentage)
- Download speed (formatted: B/s, KB/s, MB/s, GB/s)
- Estimated remaining time
- Total and received bytes

### Error Handling
- **DownloadClient**: Check `DownloadResult.Success` and `DownloadResult.Error`
- **DownloadManager**: Subscribe to `MultiDownloadError` event and check `FailedVersions`

### Thread Safety
Both APIs use thread-safe operations:
- `Interlocked` operations for atomic updates
- `ImmutableList` for task management
- Parallel execution via `Task.WhenAll`

## Best Practices

1. **Choose the Right API**:
   - Start with `DownloadClient` for simplicity
   - Move to `DownloadManager` only if you need advanced features

2. **Set Appropriate Timeouts**:
   - Consider expected file sizes and network conditions
   - Default is 60 seconds

3. **Handle Failures Gracefully**:
   - Always check download results
   - Implement retry logic for critical downloads

4. **Use Parallel Downloads Wisely**:
   - Consider network bandwidth limitations
   - Be mindful of server rate limits

5. **Monitor Progress for Large Files**:
   - Use progress tracking for downloads > 10MB
   - Provide user feedback during long downloads
