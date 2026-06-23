using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Download;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating the use of DownloadQueueManager for managing extension downloads.
/// Shows how to queue downloads, track progress, and handle download events.
/// </summary>
public class DownloadQueueExample
{
    /// <summary>
    /// Run the download queue management example.
    /// </summary>
    public static async Task RunExample()
    {
        Console.WriteLine("=== Download Queue Manager Example ===\n");

        // Create a download queue manager
        var downloadQueue = new DownloadQueueManager();

        // Subscribe to download status change events
        downloadQueue.DownloadStatusChanged += (sender, e) =>
        {
            Console.WriteLine($"[EVENT] Download Status Changed:");
            Console.WriteLine($"  Extension: {e.Task.Extension.Name} v{e.Task.Extension.Version}");
            Console.WriteLine($"  Status: {e.Task.Status}");
            Console.WriteLine($"  Progress: {e.Task.Progress}%");
            
            if (!string.IsNullOrEmpty(e.Task.ErrorMessage))
            {
                Console.WriteLine($"  Error: {e.Task.ErrorMessage}");
            }
            Console.WriteLine();
        };

        // ========================================
        // Example 1: Create download tasks
        // ========================================
        Console.WriteLine("=== Creating Download Tasks ===\n");

        var extension1 = new ExtensionMetadata
        {
            Id = "ext-001",
            Name = "extension-one",
            DisplayName = "Extension One",
            Version = "1.0.0",
            Description = "First demo extension",
            Publisher = "Demo Publisher",
            Format = ".zip",
            FileSize = 1024 * 500, // 500 KB
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows,
            DownloadUrl = "http://127.0.0.1:7391/Extension/download/ext-001"
        };

        var extension2 = new ExtensionMetadata
        {
            Id = "ext-002",
            Name = "extension-two",
            DisplayName = "Extension Two",
            Version = "2.0.0",
            Description = "Second demo extension",
            Publisher = "Demo Publisher",
            Format = ".zip",
            FileSize = 1024 * 750, // 750 KB
            Status = true,
            SupportedPlatforms = TargetPlatform.Linux,
            DownloadUrl = "http://127.0.0.1:7391/Extension/download/ext-002"
        };

        var task1 = new DownloadTask
        {
            Extension = extension1,
            SavePath = "./downloads/extension-one_1.0.0.zip",
            Status = ExtensionUpdateStatus.Queued,
            Progress = 0
        };

        var task2 = new DownloadTask
        {
            Extension = extension2,
            SavePath = "./downloads/extension-two_2.0.0.zip",
            Status = ExtensionUpdateStatus.Queued,
            Progress = 0
        };

        Console.WriteLine("Created download tasks:");
        Console.WriteLine($"  Task 1: {task1.Extension.DisplayName} -> {task1.SavePath}");
        Console.WriteLine($"  Task 2: {task2.Extension.DisplayName} -> {task2.SavePath}");
        Console.WriteLine();

        // ========================================
        // Example 2: Queue management operations
        // ========================================
        Console.WriteLine("=== Queue Management Operations ===\n");

        Console.WriteLine("Note: DownloadQueueManager provides infrastructure for managing download tasks.");
        Console.WriteLine("In production, it works with the ExtensionHost and HttpClient to handle actual downloads.");
        Console.WriteLine();

        Console.WriteLine("Key features of DownloadQueueManager:");
        Console.WriteLine("  • Queue download tasks for sequential or parallel execution");
        Console.WriteLine("  • Track download progress for each task");
        Console.WriteLine("  • Handle download status changes via events");
        Console.WriteLine("  • Support cancellation and retry logic");
        Console.WriteLine("  • Manage download priorities");
        Console.WriteLine();

        // ========================================
        // Example 3: Simulated download workflow
        // ========================================
        Console.WriteLine("=== Simulated Download Workflow ===\n");

        Console.WriteLine("Step 1: Task queued");
        task1.Status = ExtensionUpdateStatus.Queued;
        Console.WriteLine($"  {task1.Extension.DisplayName}: {task1.Status}");
        Console.WriteLine();

        await Task.Delay(500); // Simulate delay

        Console.WriteLine("Step 2: Download starting");
        task1.Status = ExtensionUpdateStatus.Updating;
        task1.Progress = 0;
        Console.WriteLine($"  {task1.Extension.DisplayName}: {task1.Status} - {task1.Progress}%");
        Console.WriteLine();

        // Simulate download progress
        for (int i = 0; i <= 100; i += 20)
        {
            await Task.Delay(300);
            task1.Progress = i;
            Console.WriteLine($"  Downloading: {task1.Progress}%");
        }
        Console.WriteLine();

        Console.WriteLine("Step 3: Download completed");
        task1.Status = ExtensionUpdateStatus.UpdateSuccessful;
        task1.Progress = 100;
        Console.WriteLine($"  {task1.Extension.DisplayName}: {task1.Status}");
        Console.WriteLine();

        // ========================================
        // Example 4: Error handling
        // ========================================
        Console.WriteLine("=== Error Handling Example ===\n");

        Console.WriteLine("Simulating download failure...");
        task2.Status = ExtensionUpdateStatus.Updating;
        task2.Progress = 30;
        Console.WriteLine($"  {task2.Extension.DisplayName}: Progress {task2.Progress}%");
        
        await Task.Delay(500);

        task2.Status = ExtensionUpdateStatus.UpdateFailed;
        task2.ErrorMessage = "Network connection lost";
        Console.WriteLine($"  {task2.Extension.DisplayName}: {task2.Status}");
        Console.WriteLine($"  Error: {task2.ErrorMessage}");
        Console.WriteLine();

        // ========================================
        // Best Practices
        // ========================================
        Console.WriteLine("=== Best Practices ===\n");
        Console.WriteLine("1. Always subscribe to DownloadStatusChanged event for progress tracking");
        Console.WriteLine("2. Handle download failures gracefully with retry logic");
        Console.WriteLine("3. Verify file integrity after download completion");
        Console.WriteLine("4. Use appropriate timeouts for network operations");
        Console.WriteLine("5. Clean up failed downloads to free disk space");
        Console.WriteLine();

        Console.WriteLine("=== Download Queue Example Completed ===");
    }
}
