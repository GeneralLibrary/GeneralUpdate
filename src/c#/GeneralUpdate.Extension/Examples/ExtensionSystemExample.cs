using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Extension;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Examples
{
    /// <summary>
    /// Example usage of the GeneralUpdate.Extension system.
    /// This demonstrates all key features of the extension update system.
    /// </summary>
    public class ExtensionSystemExample
    {
        private ExtensionManager? _manager;

        /// <summary>
        /// Initialize the extension manager with typical settings.
        /// </summary>
        public void Initialize()
        {
            // Set up paths for your application
            var clientVersion = new Version(1, 5, 0);
            var installPath = @"C:\MyApp\Extensions";
            var downloadPath = @"C:\MyApp\Temp\Downloads";
            
            // Detect current platform
            var currentPlatform = DetectCurrentPlatform();

            // Create the extension manager
            _manager = new ExtensionManager(
                clientVersion,
                installPath,
                downloadPath,
                currentPlatform,
                downloadTimeout: 300 // 5 minutes
            );

            // Subscribe to events for monitoring
            SubscribeToEvents();

            // Load existing local extensions
            _manager.LoadLocalExtensions();

            Console.WriteLine($"Extension Manager initialized for client version {clientVersion}");
            Console.WriteLine($"Platform: {currentPlatform}");
        }

        /// <summary>
        /// Subscribe to all extension events for monitoring and logging.
        /// </summary>
        private void SubscribeToEvents()
        {
            if (_manager == null) return;

            _manager.UpdateStatusChanged += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Extension '{args.ExtensionName}' status changed: {args.OldStatus} -> {args.NewStatus}");
                
                if (args.NewStatus == ExtensionUpdateStatus.UpdateFailed)
                {
                    Console.WriteLine($"  Error: {args.QueueItem.ErrorMessage}");
                }
            };

            _manager.DownloadProgress += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Downloading '{args.ExtensionName}': {args.Progress:F1}% ({args.Speed})");
            };

            _manager.DownloadCompleted += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Download completed for '{args.ExtensionName}'");
            };

            _manager.DownloadFailed += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Download failed for '{args.ExtensionName}'");
            };

            _manager.InstallCompleted += (sender, args) =>
            {
                if (args.IsSuccessful)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Installation successful: '{args.ExtensionName}' at {args.InstallPath}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Installation failed: '{args.ExtensionName}' - {args.ErrorMessage}");
                }
            };

            _manager.RollbackCompleted += (sender, args) =>
            {
                if (args.IsSuccessful)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rollback successful for '{args.ExtensionName}'");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rollback failed for '{args.ExtensionName}': {args.ErrorMessage}");
                }
            };
        }

        /// <summary>
        /// Example: List all installed extensions.
        /// </summary>
        public void ListInstalledExtensions()
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return;
            }

            var extensions = _manager.GetLocalExtensions();
            
            Console.WriteLine($"\nInstalled Extensions ({extensions.Count}):");
            Console.WriteLine("".PadRight(80, '='));
            
            foreach (var ext in extensions)
            {
                Console.WriteLine($"Name: {ext.Metadata.Name}");
                Console.WriteLine($"  ID: {ext.Metadata.Id}");
                Console.WriteLine($"  Version: {ext.Metadata.Version}");
                Console.WriteLine($"  Installed: {ext.InstallDate:yyyy-MM-dd}");
                Console.WriteLine($"  Auto-Update: {ext.AutoUpdateEnabled}");
                Console.WriteLine($"  Enabled: {ext.IsEnabled}");
                Console.WriteLine($"  Platform: {ext.Metadata.SupportedPlatforms}");
                Console.WriteLine($"  Type: {ext.Metadata.ContentType}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Example: Fetch and display compatible remote extensions.
        /// </summary>
        public async Task<List<RemoteExtension>> FetchCompatibleRemoteExtensions()
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return new List<RemoteExtension>();
            }

            // In a real application, fetch this from your server
            string remoteJson = await FetchRemoteExtensionsFromServer();
            
            // Parse remote extensions
            var allRemoteExtensions = _manager.ParseRemoteExtensions(remoteJson);
            
            // Filter to only compatible extensions
            var compatibleExtensions = _manager.GetCompatibleRemoteExtensions(allRemoteExtensions);
            
            Console.WriteLine($"\nCompatible Remote Extensions ({compatibleExtensions.Count}):");
            Console.WriteLine("".PadRight(80, '='));
            
            foreach (var ext in compatibleExtensions)
            {
                Console.WriteLine($"Name: {ext.Metadata.Name}");
                Console.WriteLine($"  Version: {ext.Metadata.Version}");
                Console.WriteLine($"  Description: {ext.Metadata.Description}");
                Console.WriteLine($"  Author: {ext.Metadata.Author}");
                Console.WriteLine();
            }

            return compatibleExtensions;
        }

        /// <summary>
        /// Example: Queue a specific extension for update.
        /// </summary>
        public void QueueExtensionUpdate(string extensionId, List<RemoteExtension> remoteExtensions)
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return;
            }

            // Find the best version for this extension
            var bestVersion = _manager.FindBestUpgradeVersion(extensionId, remoteExtensions);
            
            if (bestVersion == null)
            {
                Console.WriteLine($"No compatible version found for extension '{extensionId}'");
                return;
            }

            Console.WriteLine($"Queueing update for '{bestVersion.Metadata.Name}' to version {bestVersion.Metadata.Version}");
            
            try
            {
                var queueItem = _manager.QueueExtensionUpdate(bestVersion, enableRollback: true);
                Console.WriteLine($"Successfully queued. Queue ID: {queueItem.QueueId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to queue extension: {ex.Message}");
            }
        }

        /// <summary>
        /// Example: Check for updates and queue them automatically.
        /// </summary>
        public async Task<int> CheckAndQueueAutoUpdates()
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return 0;
            }

            Console.WriteLine("Checking for updates...");
            
            // Fetch remote extensions
            var remoteExtensions = await FetchCompatibleRemoteExtensions();
            
            // Queue all auto-updates
            var queuedItems = _manager.QueueAutoUpdates(remoteExtensions);
            
            Console.WriteLine($"Queued {queuedItems.Count} extension(s) for update");
            
            return queuedItems.Count;
        }

        /// <summary>
        /// Example: Process all queued updates.
        /// </summary>
        public async Task ProcessAllQueuedUpdates()
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return;
            }

            var queueItems = _manager.GetUpdateQueue();
            
            if (queueItems.Count == 0)
            {
                Console.WriteLine("No updates in queue");
                return;
            }

            Console.WriteLine($"Processing {queueItems.Count} queued update(s)...");
            
            await _manager.ProcessAllUpdatesAsync();
            
            Console.WriteLine("All updates processed");
            
            // Check results
            var successful = _manager.GetUpdateQueueByStatus(ExtensionUpdateStatus.UpdateSuccessful);
            var failed = _manager.GetUpdateQueueByStatus(ExtensionUpdateStatus.UpdateFailed);
            
            Console.WriteLine($"Successful: {successful.Count}, Failed: {failed.Count}");
            
            // Clean up completed items
            _manager.ClearCompletedUpdates();
        }

        /// <summary>
        /// Example: Configure auto-update settings.
        /// </summary>
        public void ConfigureAutoUpdate()
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return;
            }

            // Enable global auto-update
            _manager.GlobalAutoUpdateEnabled = true;
            Console.WriteLine("Global auto-update enabled");

            // Enable auto-update for specific extension
            _manager.SetExtensionAutoUpdate("my-extension-id", true);
            Console.WriteLine("Auto-update enabled for 'my-extension-id'");

            // Disable auto-update for another extension
            _manager.SetExtensionAutoUpdate("another-extension-id", false);
            Console.WriteLine("Auto-update disabled for 'another-extension-id'");
        }

        /// <summary>
        /// Example: Check version compatibility.
        /// </summary>
        public void CheckVersionCompatibility(ExtensionMetadata metadata)
        {
            if (_manager == null)
            {
                Console.WriteLine("Manager not initialized");
                return;
            }

            bool compatible = _manager.IsExtensionCompatible(metadata);
            
            Console.WriteLine($"Extension '{metadata.Name}' version {metadata.Version}:");
            Console.WriteLine($"  Client version: {_manager.ClientVersion}");
            Console.WriteLine($"  Required range: {metadata.Compatibility.MinClientVersion} - {metadata.Compatibility.MaxClientVersion}");
            Console.WriteLine($"  Compatible: {(compatible ? "Yes" : "No")}");
        }

        /// <summary>
        /// Complete workflow example: Check for updates and install them.
        /// </summary>
        public async Task RunCompleteUpdateWorkflow()
        {
            Console.WriteLine("=== Extension Update Workflow ===\n");

            // Step 1: Initialize
            Initialize();

            // Step 2: List installed extensions
            ListInstalledExtensions();

            // Step 3: Check for updates
            int updateCount = await CheckAndQueueAutoUpdates();

            // Step 4: Process updates if any
            if (updateCount > 0)
            {
                await ProcessAllQueuedUpdates();
            }
            else
            {
                Console.WriteLine("All extensions are up to date");
            }

            Console.WriteLine("\n=== Update Workflow Complete ===");
        }

        #region Helper Methods

        /// <summary>
        /// Detect the current platform at runtime.
        /// </summary>
        private ExtensionPlatform DetectCurrentPlatform()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return ExtensionPlatform.Windows;
            
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                return ExtensionPlatform.Linux;
            
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                return ExtensionPlatform.macOS;

            return ExtensionPlatform.None;
        }

        /// <summary>
        /// Simulates fetching remote extensions from a server.
        /// In a real application, this would make an HTTP request to your extension server.
        /// </summary>
        private async Task<string> FetchRemoteExtensionsFromServer()
        {
            // Simulate network delay
            await Task.Delay(100);

            // In a real application, you would fetch this from your server:
            // using (var client = new HttpClient())
            // {
            //     return await client.GetStringAsync("https://your-server.com/api/extensions");
            // }

            // Sample JSON response
            return @"[
                {
                    ""metadata"": {
                        ""id"": ""sample-extension"",
                        ""name"": ""Sample Extension"",
                        ""version"": ""1.0.0"",
                        ""description"": ""A sample extension for demonstration"",
                        ""author"": ""Extension Developer"",
                        ""license"": ""MIT"",
                        ""supportedPlatforms"": 7,
                        ""contentType"": 0,
                        ""compatibility"": {
                            ""minClientVersion"": ""1.0.0"",
                            ""maxClientVersion"": ""2.0.0""
                        },
                        ""downloadUrl"": ""https://example.com/extensions/sample-1.0.0.zip"",
                        ""hash"": ""sha256-example-hash"",
                        ""size"": 1048576,
                        ""releaseDate"": ""2024-01-01T00:00:00Z""
                    },
                    ""isPreRelease"": false
                }
            ]";
        }

        #endregion
    }

    /// <summary>
    /// Entry point for running the example.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var example = new ExtensionSystemExample();
            
            try
            {
                await example.RunCompleteUpdateWorkflow();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
