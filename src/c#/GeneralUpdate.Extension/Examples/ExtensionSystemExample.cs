using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Examples
{
    /// <summary>
    /// Example usage of the GeneralUpdate.Extension system.
    /// Demonstrates all key features of the extension update system with the refactored architecture.
    /// </summary>
    public class ExtensionSystemExample
    {
        private IExtensionHost? _host;

        /// <summary>
        /// Initialize the extension host with typical settings.
        /// </summary>
        public void Initialize()
        {
            // Set up paths for your application
            var hostVersion = new Version(1, 5, 0);
            var installPath = @"C:\MyApp\Extensions";
            var downloadPath = @"C:\MyApp\Temp\Downloads";

            // Detect current platform
            var currentPlatform = DetectCurrentPlatform();

            // Create the extension host
            _host = new GeneralExtensionHost(
                hostVersion,
                installPath,
                downloadPath,
                currentPlatform,
                downloadTimeout: 300 // 5 minutes
            );

            // Subscribe to events for monitoring
            SubscribeToEvents();

            // Load existing installed extensions
            _host.LoadInstalledExtensions();

            Console.WriteLine($"Extension Host initialized for version {hostVersion}");
            Console.WriteLine($"Platform: {currentPlatform}");
        }

        /// <summary>
        /// Subscribe to all extension events for monitoring and logging.
        /// </summary>
        private void SubscribeToEvents()
        {
            if (_host == null) return;

            _host.UpdateStateChanged += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Extension '{args.ExtensionName}' state changed: {args.PreviousState} -> {args.CurrentState}");

                if (args.CurrentState == Download.UpdateState.UpdateFailed)
                {
                    Console.WriteLine($"  Error: {args.Operation.ErrorMessage}");
                }
            };

            _host.DownloadProgress += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Downloading '{args.ExtensionName}': {args.ProgressPercentage:F1}% ({args.Speed})");
            };

            _host.DownloadCompleted += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Download completed for '{args.ExtensionName}'");
            };

            _host.DownloadFailed += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Download failed for '{args.ExtensionName}'");
            };

            _host.InstallationCompleted += (sender, args) =>
            {
                if (args.Success)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Installation successful: '{args.ExtensionName}' at {args.InstallPath}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Installation failed: '{args.ExtensionName}' - {args.ErrorMessage}");
                }
            };

            _host.RollbackCompleted += (sender, args) =>
            {
                if (args.Success)
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
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return;
            }

            var extensions = _host.GetInstalledExtensions();

            Console.WriteLine($"\nInstalled Extensions ({extensions.Count}):");
            Console.WriteLine("".PadRight(80, '='));

            foreach (var ext in extensions)
            {
                Console.WriteLine($"Name: {ext.Descriptor.DisplayName}");
                Console.WriteLine($"  ID: {ext.Descriptor.Name}");
                Console.WriteLine($"  Version: {ext.Descriptor.Version}");
                Console.WriteLine($"  Installed: {ext.InstallDate:yyyy-MM-dd}");
                Console.WriteLine($"  Auto-Update: {ext.AutoUpdateEnabled}");
                Console.WriteLine($"  Enabled: {ext.IsEnabled}");
                Console.WriteLine($"  Platform: {ext.Descriptor.SupportedPlatforms}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Example: Fetch and display compatible remote extensions.
        /// </summary>
        public async Task<List<Metadata.AvailableExtension>> FetchCompatibleExtensions()
        {
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return new List<Metadata.AvailableExtension>();
            }

            // In a real application, fetch this from your server
            string remoteJson = await FetchExtensionsFromServer();

            // Parse available extensions
            var allExtensions = _host.ParseAvailableExtensions(remoteJson);

            // Filter to only compatible extensions
            var compatibleExtensions = _host.GetCompatibleExtensions(allExtensions);

            Console.WriteLine($"\nCompatible Extensions ({compatibleExtensions.Count}):");
            Console.WriteLine("".PadRight(80, '='));

            foreach (var ext in compatibleExtensions)
            {
                Console.WriteLine($"Name: {ext.Descriptor.DisplayName}");
                Console.WriteLine($"  Version: {ext.Descriptor.Version}");
                Console.WriteLine($"  Description: {ext.Descriptor.Description}");
                Console.WriteLine($"  Author: {ext.Descriptor.Publisher}");
                Console.WriteLine();
            }

            return compatibleExtensions;
        }

        /// <summary>
        /// Example: Queue a specific extension for update.
        /// </summary>
        public void QueueExtensionUpdate(string extensionName, List<Metadata.AvailableExtension> availableExtensions)
        {
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return;
            }

            // Find the best version for this extension
            var bestVersion = _host.FindBestUpgrade(extensionName, availableExtensions);

            if (bestVersion == null)
            {
                Console.WriteLine($"No compatible version found for extension '{extensionName}'");
                return;
            }

            Console.WriteLine($"Queueing update for '{bestVersion.Descriptor.DisplayName}' to version {bestVersion.Descriptor.Version}");

            try
            {
                var operation = _host.QueueUpdate(bestVersion, enableRollback: true);
                Console.WriteLine($"Successfully queued. Operation ID: {operation.OperationId}");
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
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return 0;
            }

            Console.WriteLine("Checking for updates...");

            // Fetch available extensions
            var availableExtensions = await FetchCompatibleExtensions();

            // Queue all auto-updates
            var queuedOperations = _host.QueueAutoUpdates(availableExtensions);

            Console.WriteLine($"Queued {queuedOperations.Count} extension(s) for update");

            return queuedOperations.Count;
        }

        /// <summary>
        /// Example: Process all queued updates.
        /// </summary>
        public async Task ProcessAllQueuedUpdates()
        {
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return;
            }

            var operations = _host.GetUpdateQueue();

            if (operations.Count == 0)
            {
                Console.WriteLine("No updates in queue");
                return;
            }

            Console.WriteLine($"Processing {operations.Count} queued update(s)...");

            await _host.ProcessAllUpdatesAsync();

            Console.WriteLine("All updates processed");

            // Check results
            var successful = _host.GetUpdatesByState(Download.UpdateState.UpdateSuccessful);
            var failed = _host.GetUpdatesByState(Download.UpdateState.UpdateFailed);

            Console.WriteLine($"Successful: {successful.Count}, Failed: {failed.Count}");

            // Clean up completed items
            _host.ClearCompletedUpdates();
        }

        /// <summary>
        /// Example: Configure auto-update settings.
        /// </summary>
        public void ConfigureAutoUpdate()
        {
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return;
            }

            // Enable global auto-update
            _host.GlobalAutoUpdateEnabled = true;
            Console.WriteLine("Global auto-update enabled");

            // Enable auto-update for specific extension
            _host.SetAutoUpdate("my-extension-id", true);
            Console.WriteLine("Auto-update enabled for 'my-extension-id'");

            // Disable auto-update for another extension
            _host.SetAutoUpdate("another-extension-id", false);
            Console.WriteLine("Auto-update disabled for 'another-extension-id'");
        }

        /// <summary>
        /// Example: Check version compatibility.
        /// </summary>
        public void CheckVersionCompatibility(Metadata.ExtensionDescriptor descriptor)
        {
            if (_host == null)
            {
                Console.WriteLine("Host not initialized");
                return;
            }

            bool compatible = _host.IsCompatible(descriptor);

            Console.WriteLine($"Extension '{descriptor.DisplayName}' version {descriptor.Version}:");
            Console.WriteLine($"  Host version: {_host.HostVersion}");
            Console.WriteLine($"  Required range: {descriptor.Compatibility.MinHostVersion} - {descriptor.Compatibility.MaxHostVersion}");
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
        private Metadata.TargetPlatform DetectCurrentPlatform()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return Metadata.TargetPlatform.Windows;

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                return Metadata.TargetPlatform.Linux;

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                return Metadata.TargetPlatform.MacOS;

            return Metadata.TargetPlatform.None;
        }

        /// <summary>
        /// Simulates fetching remote extensions from a server.
        /// In a real application, this would make an HTTP request to your extension server.
        /// </summary>
        private async Task<string> FetchExtensionsFromServer()
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
                    ""descriptor"": {
                        ""id"": ""sample-extension"",
                        ""name"": ""Sample Extension"",
                        ""version"": ""1.0.0"",
                        ""description"": ""A sample extension for demonstration"",
                        ""author"": ""Extension Developer"",
                        ""license"": ""MIT"",
                        ""supportedPlatforms"": 7,
                        ""compatibility"": {
                            ""minHostVersion"": ""1.0.0"",
                            ""maxHostVersion"": ""2.0.0""
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
