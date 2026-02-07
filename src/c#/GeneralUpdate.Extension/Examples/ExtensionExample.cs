using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Core;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating core extension operations including querying, updating, and managing installed extensions.
/// This example uses the API endpoint http://127.0.0.1:7391/Extension
/// </summary>
public class ExtensionExample
{
    /// <summary>
    /// Run a complete demonstration of extension operations.
    /// </summary>
    public static async Task RunExample()
    {
        Console.WriteLine("=== GeneralUpdate.Extension Example ===\n");

        // Initialize the extension host with configuration
        var options = new ExtensionHostOptions
        {
            ServerUrl = "http://127.0.0.1:7391/Extension",
            //Scheme = "Bearer",
            //Token = "your-token-here",
            HostVersion = "1.0.0.0",
            ExtensionsDirectory = "./extensions"
        };

        var host = new GeneralExtensionHost(options);

        // Subscribe to extension update events
        host.ExtensionUpdateStatusChanged += (sender, e) =>
        {
            Console.WriteLine($"[EVENT] Extension: {e.ExtensionName ?? e.ExtensionId}");
            Console.WriteLine($"        Status: {e.Status}");
            if (e.Status == ExtensionUpdateStatus.Updating)
            {
                Console.WriteLine($"        Progress: {e.Progress}%");
            }
            if (e.Status == ExtensionUpdateStatus.UpdateFailed)
            {
                Console.WriteLine($"        Error: {e.ErrorMessage}");
            }
            Console.WriteLine();
        };

        // ========================================
        // 1. Query Remote Extension List (QueryExtensionsAsync)
        // ========================================
        Console.WriteLine("=== 1. Querying Remote Extensions ===");
        Console.WriteLine($"API Endpoint: {options.ServerUrl}/Extension\n");

        var query = new ExtensionQueryDTO
        {
            BeginDate = DateTime.Now.AddDays(-30),
            EndDate = DateTime.Now
        };

        string tempId = string.Empty;
        
        try
        {
            var queryResult = await host.QueryExtensionsAsync(query);
            if (queryResult.Body != null)
            {
                Console.WriteLine($"✓ Found {queryResult.Body.TotalCount} extensions:");
                foreach (var ext in queryResult.Body.Items)
                {
                    Console.WriteLine($"  • {ext.DisplayName} v{ext.Version}");
                    Console.WriteLine($"    ID: {ext.Id}");
                    Console.WriteLine($"    Publisher: {ext.Publisher}");
                    Console.WriteLine($"    Compatible: {ext.IsCompatible}");
                    Console.WriteLine($"    Platform: {ext.SupportedPlatforms}");
                    Console.WriteLine();
                    tempId = ext.Id;
                }
            }
            else
            {
                Console.WriteLine($"✗ Query failed: {queryResult.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error querying extensions: {ex.Message}");
        }

        Console.WriteLine("\n" + new string('=', 50) + "\n");

        // ========================================
        // 2. Update Extension (UpdateExtensionAsync)
        // ========================================
        Console.WriteLine("=== 2. Updating Extension ===");
        Console.WriteLine($"API Endpoint: {options.ServerUrl}/Extension\n");

        // Example extension ID - replace with actual ID from query result
        Console.WriteLine($"Extension ID: {tempId}");

        try
        {
            var updateSuccess = await host.UpdateExtensionAsync(tempId);
            if (updateSuccess)
            {
                Console.WriteLine("✓ Extension updated successfully!");
                Console.WriteLine("  The UpdateExtensionAsync method automatically:");
                Console.WriteLine("  - Queries the extension from server");
                Console.WriteLine("  - Checks compatibility");
                Console.WriteLine("  - Checks platform support");
                Console.WriteLine("  - Downloads the extension");
                Console.WriteLine("  - Installs the extension");
                Console.WriteLine("  - Updates the catalog");
            }
            else
            {
                Console.WriteLine("✗ Extension update failed!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error updating extension: {ex.Message}");
        }

        Console.WriteLine("\n" + new string('=', 50) + "\n");

        // ========================================
        // 3. Load and Manage Installed Extensions
        // ========================================
        Console.WriteLine("=== 3. Managing Installed Extensions ===\n");

        // LoadInstalledExtensions - Load extensions from catalog
        Console.WriteLine("--- LoadInstalledExtensions ---");
        try
        {
            host.ExtensionCatalog.LoadInstalledExtensions();
            Console.WriteLine("✓ Installed extensions loaded from catalog\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error loading extensions: {ex.Message}\n");
        }

        // GetInstalledExtensions - Get all installed extensions
        Console.WriteLine("--- GetInstalledExtensions ---");
        try
        {
            var installedExtensions = host.ExtensionCatalog.GetInstalledExtensions();
            Console.WriteLine($"✓ Total installed extensions: {installedExtensions.Count}");
            
            if (installedExtensions.Count > 0)
            {
                foreach (var ext in installedExtensions)
                {
                    Console.WriteLine($"  • {ext.DisplayName} v{ext.Version}");
                    Console.WriteLine($"    ID: {ext.Id}");
                    Console.WriteLine($"    Name: {ext.Name}");
                    Console.WriteLine($"    Status: {(ext.Status == true ? "Enabled" : "Disabled")}");
                    Console.WriteLine($"    Platform: {ext.SupportedPlatforms}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("  No extensions installed yet\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting installed extensions: {ex.Message}\n");
        }

        // GetInstalledExtensionById - Get specific extension by ID
        Console.WriteLine("--- GetInstalledExtensionById ---");
        try
        {
            var extensionId = "sample-extension-guid";
            var extension = host.ExtensionCatalog.GetInstalledExtensionById(extensionId);
            
            if (extension != null)
            {
                Console.WriteLine($"✓ Found extension:");
                Console.WriteLine($"  ID: {extension.Id}");
                Console.WriteLine($"  Name: {extension.DisplayName}");
                Console.WriteLine($"  Version: {extension.Version}");
                Console.WriteLine($"  Publisher: {extension.Publisher}");
                Console.WriteLine($"  Status: {(extension.Status == true ? "Enabled" : "Disabled")}");
            }
            else
            {
                Console.WriteLine($"✗ Extension with ID '{extensionId}' not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting extension by ID: {ex.Message}");
        }

        Console.WriteLine();

        // GetInstalledExtensionsByPlatform - Get extensions for specific platform
        Console.WriteLine("--- GetInstalledExtensionsByPlatform ---");
        try
        {
            var platform = TargetPlatform.Windows;
            var platformExtensions = host.ExtensionCatalog.GetInstalledExtensionsByPlatform(platform);
            
            Console.WriteLine($"✓ Extensions for platform '{platform}': {platformExtensions.Count}");
            
            if (platformExtensions.Count > 0)
            {
                foreach (var ext in platformExtensions)
                {
                    Console.WriteLine($"  • {ext.DisplayName} v{ext.Version}");
                    Console.WriteLine($"    Supported Platforms: {ext.SupportedPlatforms}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting extensions by platform: {ex.Message}");
        }

        Console.WriteLine("\n=== Example Completed ===");
    }
}
