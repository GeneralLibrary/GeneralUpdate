using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using System;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating the use of ExtensionCatalog for managing installed extensions.
/// Shows how to load, query, add, update, and remove extensions from the catalog.
/// </summary>
public class ExtensionCatalogExample
{
    /// <summary>
    /// Run the extension catalog example.
    /// </summary>
    public static void RunExample()
    {
        Console.WriteLine("=== Extension Catalog Example ===\n");

        // Create a temporary catalog directory for demonstration
        var catalogPath = Path.Combine(Path.GetTempPath(), $"demo-catalog-{Guid.NewGuid()}");
        Console.WriteLine($"Catalog Path: {catalogPath}\n");

        // Create an extension catalog
        var catalog = new ExtensionCatalog(catalogPath);

        // ========================================
        // Example 1: Load installed extensions
        // ========================================
        Console.WriteLine("=== Example 1: Loading Installed Extensions ===\n");

        Console.WriteLine("Loading extensions from catalog...");
        catalog.LoadInstalledExtensions();
        Console.WriteLine("✓ Extensions loaded\n");

        // ========================================
        // Example 2: Add extensions to catalog
        // ========================================
        Console.WriteLine("=== Example 2: Adding Extensions ===\n");

        var extension1 = new ExtensionMetadata
        {
            Id = "ext-001",
            Name = "logger-extension",
            DisplayName = "Logger Extension",
            Version = "1.0.0",
            Description = "Provides advanced logging capabilities",
            Publisher = "Core Team",
            Format = ".zip",
            FileSize = 1024 * 250,
            Hash = "abc123hash",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0",
            IsPreRelease = false,
            ReleaseDate = DateTime.UtcNow.AddDays(-30),
            UploadTime = DateTime.UtcNow.AddDays(-30),
            Categories = "Logging,Utilities",
            License = "MIT",
            Dependencies = null
        };

        var extension2 = new ExtensionMetadata
        {
            Id = "ext-002",
            Name = "authentication-extension",
            DisplayName = "Authentication Extension",
            Version = "2.1.0",
            Description = "Provides OAuth2 and JWT authentication",
            Publisher = "Security Team",
            Format = ".zip",
            FileSize = 1024 * 500,
            Hash = "def456hash",
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux,
            MinHostVersion = "1.5.0",
            MaxHostVersion = "3.0.0",
            IsPreRelease = false,
            ReleaseDate = DateTime.UtcNow.AddDays(-15),
            UploadTime = DateTime.UtcNow.AddDays(-15),
            Categories = "Security,Authentication",
            License = "Apache-2.0",
            Dependencies = null
        };

        var extension3 = new ExtensionMetadata
        {
            Id = "ext-003",
            Name = "reporting-extension",
            DisplayName = "Reporting Extension",
            Version = "1.5.0",
            Description = "Generate PDF and Excel reports",
            Publisher = "Reporting Team",
            Format = ".zip",
            FileSize = 1024 * 1024, // 1 MB
            Hash = "ghi789hash",
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows,
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.5.0",
            IsPreRelease = false,
            ReleaseDate = DateTime.UtcNow.AddDays(-7),
            UploadTime = DateTime.UtcNow.AddDays(-7),
            Categories = "Reporting,Export",
            License = "MIT",
            Dependencies = "ext-001" // Depends on logger
        };

        Console.WriteLine("Adding extensions to catalog...");
        catalog.AddOrUpdateInstalledExtension(extension1);
        catalog.AddOrUpdateInstalledExtension(extension2);
        catalog.AddOrUpdateInstalledExtension(extension3);
        
        Console.WriteLine($"✓ Added {extension1.DisplayName}");
        Console.WriteLine($"✓ Added {extension2.DisplayName}");
        Console.WriteLine($"✓ Added {extension3.DisplayName}");
        Console.WriteLine();

        // ========================================
        // Example 3: Get all installed extensions
        // ========================================
        Console.WriteLine("=== Example 3: Retrieving All Extensions ===\n");

        var allExtensions = catalog.GetInstalledExtensions();
        Console.WriteLine($"Total installed extensions: {allExtensions.Count}\n");

        foreach (var ext in allExtensions)
        {
            Console.WriteLine($"• {ext.DisplayName} v{ext.Version}");
            Console.WriteLine($"  ID: {ext.Id}");
            Console.WriteLine($"  Publisher: {ext.Publisher}");
            Console.WriteLine($"  Status: {(ext.Status == true ? "Enabled" : "Disabled")}");
            Console.WriteLine($"  Platforms: {ext.SupportedPlatforms}");
            Console.WriteLine($"  Categories: {ext.Categories}");
            Console.WriteLine($"  License: {ext.License}");
            
            if (!string.IsNullOrEmpty(ext.Dependencies))
            {
                Console.WriteLine($"  Dependencies: {ext.Dependencies}");
            }
            
            Console.WriteLine();
        }

        // ========================================
        // Example 4: Get extension by ID
        // ========================================
        Console.WriteLine("=== Example 4: Get Extension by ID ===\n");

        var extensionId = "ext-002";
        Console.WriteLine($"Searching for extension ID: {extensionId}");
        
        var foundExtension = catalog.GetInstalledExtensionById(extensionId);
        
        if (foundExtension != null)
        {
            Console.WriteLine("\n✓ Extension found:");
            Console.WriteLine($"  Name: {foundExtension.DisplayName}");
            Console.WriteLine($"  Version: {foundExtension.Version}");
            Console.WriteLine($"  Description: {foundExtension.Description}");
            Console.WriteLine($"  File Size: {foundExtension.FileSize / 1024.0:F2} KB");
            Console.WriteLine($"  Release Date: {foundExtension.ReleaseDate:yyyy-MM-dd}");
        }
        else
        {
            Console.WriteLine("\n✗ Extension not found");
        }
        Console.WriteLine();

        // ========================================
        // Example 5: Get extensions by platform
        // ========================================
        Console.WriteLine("=== Example 5: Get Extensions by Platform ===\n");

        Console.WriteLine("Windows Extensions:");
        var windowsExtensions = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.Windows);
        Console.WriteLine($"  Count: {windowsExtensions.Count}");
        foreach (var ext in windowsExtensions)
        {
            Console.WriteLine($"  • {ext.DisplayName} v{ext.Version}");
        }
        Console.WriteLine();

        Console.WriteLine("Linux Extensions:");
        var linuxExtensions = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.Linux);
        Console.WriteLine($"  Count: {linuxExtensions.Count}");
        foreach (var ext in linuxExtensions)
        {
            Console.WriteLine($"  • {ext.DisplayName} v{ext.Version}");
        }
        Console.WriteLine();

        // ========================================
        // Example 6: Update an extension
        // ========================================
        Console.WriteLine("=== Example 6: Updating an Extension ===\n");

        Console.WriteLine($"Current version of {extension1.DisplayName}: {extension1.Version}");
        
        // Update the extension version
        extension1.Version = "1.1.0";
        extension1.Description = "Provides advanced logging capabilities with new features";
        extension1.UploadTime = DateTime.UtcNow;
        
        catalog.AddOrUpdateInstalledExtension(extension1);
        
        Console.WriteLine($"✓ Updated to version: {extension1.Version}");
        Console.WriteLine($"  New description: {extension1.Description}");
        Console.WriteLine();

        // Verify the update
        var updatedExt = catalog.GetInstalledExtensionById(extension1.Id);
        if (updatedExt != null && updatedExt.Version == "1.1.0")
        {
            Console.WriteLine("✓ Update verified in catalog");
        }
        Console.WriteLine();

        // ========================================
        // Example 7: Remove an extension
        // ========================================
        Console.WriteLine("=== Example 7: Removing an Extension ===\n");

        var extensionToRemove = "ext-003";
        Console.WriteLine($"Removing extension: {extensionToRemove}");
        
        var beforeCount = catalog.GetInstalledExtensions().Count;
        catalog.RemoveInstalledExtension(extensionToRemove);
        var afterCount = catalog.GetInstalledExtensions().Count;
        
        Console.WriteLine($"✓ Extension removed");
        Console.WriteLine($"  Extensions before: {beforeCount}");
        Console.WriteLine($"  Extensions after: {afterCount}");
        Console.WriteLine();

        // Verify removal
        var removedExt = catalog.GetInstalledExtensionById(extensionToRemove);
        if (removedExt == null)
        {
            Console.WriteLine("✓ Removal verified - extension no longer in catalog");
        }
        else
        {
            Console.WriteLine("✗ Extension still in catalog");
        }
        Console.WriteLine();

        // ========================================
        // Example 8: Query and filter extensions
        // ========================================
        Console.WriteLine("=== Example 8: Querying and Filtering ===\n");

        var allRemainingExts = catalog.GetInstalledExtensions();

        Console.WriteLine("Extensions by category:");
        
        var securityExts = allRemainingExts
            .Where(e => e.Categories != null && e.Categories.Contains("Security"))
            .ToList();
        Console.WriteLine($"\n  Security Extensions: {securityExts.Count}");
        foreach (var ext in securityExts)
        {
            Console.WriteLine($"    • {ext.DisplayName}");
        }

        var loggingExts = allRemainingExts
            .Where(e => e.Categories != null && e.Categories.Contains("Logging"))
            .ToList();
        Console.WriteLine($"\n  Logging Extensions: {loggingExts.Count}");
        foreach (var ext in loggingExts)
        {
            Console.WriteLine($"    • {ext.DisplayName}");
        }

        Console.WriteLine();

        // Filter by license
        var mitLicensed = allRemainingExts
            .Where(e => e.License == "MIT")
            .ToList();
        Console.WriteLine($"MIT Licensed Extensions: {mitLicensed.Count}");
        foreach (var ext in mitLicensed)
        {
            Console.WriteLine($"  • {ext.DisplayName}");
        }
        Console.WriteLine();

        // ========================================
        // Best Practices
        // ========================================
        Console.WriteLine("=== Best Practices ===\n");
        Console.WriteLine("1. Always call LoadInstalledExtensions() after creating a catalog");
        Console.WriteLine("2. Use unique IDs for extensions to avoid conflicts");
        Console.WriteLine("3. Keep the catalog synchronized with actual installed extensions");
        Console.WriteLine("4. Persist catalog changes to storage regularly");
        Console.WriteLine("5. Validate extension metadata before adding to catalog");
        Console.WriteLine("6. Handle catalog corruption gracefully");
        Console.WriteLine("7. Use GetInstalledExtensionsByPlatform() for platform-specific queries");
        Console.WriteLine("8. Update catalog immediately after installing or removing extensions");
        Console.WriteLine();

        // Cleanup
        try
        {
            if (Directory.Exists(catalogPath))
            {
                Directory.Delete(catalogPath, true);
                Console.WriteLine($"Cleaned up temporary catalog directory");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: Could not delete temporary catalog: {ex.Message}");
        }

        Console.WriteLine("\n=== Extension Catalog Example Completed ===");
    }
}
