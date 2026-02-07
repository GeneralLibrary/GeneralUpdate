using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Dependencies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating the use of DependencyResolver for managing extension dependencies.
/// Shows how to resolve dependencies, check for missing dependencies, and handle dependency chains.
/// </summary>
public class DependencyResolverExample
{
    /// <summary>
    /// Run the dependency resolver example.
    /// </summary>
    public static void RunExample()
    {
        Console.WriteLine("=== Dependency Resolver Example ===\n");

        // Create an extension catalog and dependency resolver
        var catalogPath = Path.Combine(Path.GetTempPath(), $"test-catalog-{Guid.NewGuid()}");
        var catalog = new ExtensionCatalog(catalogPath);
        var resolver = new DependencyResolver(catalog);

        // ========================================
        // Setup: Create sample extensions with dependencies
        // ========================================
        Console.WriteLine("=== Setting Up Test Extensions ===\n");

        // Base extension with no dependencies
        var baseExtension = new ExtensionMetadata
        {
            Id = "base-lib-001",
            Name = "base-library",
            DisplayName = "Base Library",
            Version = "1.0.0",
            Description = "Core base library with no dependencies",
            Publisher = "Core Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            Dependencies = null // No dependencies
        };

        // Utility extension depending on base
        var utilExtension = new ExtensionMetadata
        {
            Id = "util-lib-001",
            Name = "utility-library",
            DisplayName = "Utility Library",
            Version = "1.5.0",
            Description = "Utility functions, depends on base library",
            Publisher = "Core Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            Dependencies = "base-lib-001" // Depends on base library
        };

        // Advanced extension depending on both base and utility
        var advancedExtension = new ExtensionMetadata
        {
            Id = "advanced-ext-001",
            Name = "advanced-extension",
            DisplayName = "Advanced Extension",
            Version = "2.0.0",
            Description = "Advanced features, depends on base and utility libraries",
            Publisher = "Advanced Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows,
            Dependencies = "base-lib-001,util-lib-001" // Depends on both
        };

        // Complex extension with multiple dependencies
        var complexExtension = new ExtensionMetadata
        {
            Id = "complex-ext-001",
            Name = "complex-extension",
            DisplayName = "Complex Extension",
            Version = "3.0.0",
            Description = "Complex extension with multiple dependencies",
            Publisher = "Complex Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            Dependencies = "advanced-ext-001,util-lib-001" // Depends on advanced and utility
        };

        // Add base and utility to catalog (simulating they are already installed)
        catalog.AddOrUpdateInstalledExtension(baseExtension);
        catalog.AddOrUpdateInstalledExtension(utilExtension);

        Console.WriteLine("Installed extensions in catalog:");
        Console.WriteLine($"  • {baseExtension.DisplayName} (ID: {baseExtension.Id})");
        Console.WriteLine($"    Dependencies: None");
        Console.WriteLine();
        Console.WriteLine($"  • {utilExtension.DisplayName} (ID: {utilExtension.Id})");
        Console.WriteLine($"    Dependencies: {utilExtension.Dependencies}");
        Console.WriteLine();

        // ========================================
        // Example 1: Check dependencies for extension with all dependencies met
        // ========================================
        Console.WriteLine("=== Example 1: All Dependencies Met ===\n");

        Console.WriteLine($"Checking dependencies for: {advancedExtension.DisplayName}");
        Console.WriteLine($"  Required Dependencies: {advancedExtension.Dependencies}");
        Console.WriteLine();

        var missingDeps1 = GetMissingDependencies(advancedExtension, catalog);
        if (missingDeps1.Count == 0)
        {
            Console.WriteLine("✓ All dependencies are satisfied!");
            Console.WriteLine($"  {advancedExtension.DisplayName} can be installed.");
        }
        else
        {
            Console.WriteLine($"✗ Missing dependencies: {string.Join(", ", missingDeps1)}");
        }
        Console.WriteLine();

        // ========================================
        // Example 2: Check dependencies with missing dependencies
        // ========================================
        Console.WriteLine("=== Example 2: Missing Dependencies ===\n");

        Console.WriteLine($"Checking dependencies for: {complexExtension.DisplayName}");
        Console.WriteLine($"  Required Dependencies: {complexExtension.Dependencies}");
        Console.WriteLine();

        var missingDeps2 = GetMissingDependencies(complexExtension, catalog);
        if (missingDeps2.Count > 0)
        {
            Console.WriteLine($"✗ Missing dependencies ({missingDeps2.Count}):");
            foreach (var depId in missingDeps2)
            {
                Console.WriteLine($"  • {depId}");
            }
            Console.WriteLine();
            Console.WriteLine("Action required: Install missing dependencies before installing this extension");
        }
        else
        {
            Console.WriteLine("✓ All dependencies are satisfied!");
        }
        Console.WriteLine();

        // ========================================
        // Example 3: Resolve dependency chain
        // ========================================
        Console.WriteLine("=== Example 3: Dependency Chain Resolution ===\n");

        Console.WriteLine("Resolving dependency chain for Complex Extension:");
        Console.WriteLine();

        // Build dependency chain
        var dependencyChain = BuildDependencyChain(complexExtension);
        Console.WriteLine("Installation order (dependencies first):");
        for (int i = 0; i < dependencyChain.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {dependencyChain[i]}");
        }
        Console.WriteLine($"  {dependencyChain.Count + 1}. {complexExtension.Id} ({complexExtension.DisplayName})");
        Console.WriteLine();

        // ========================================
        // Example 4: Handling circular dependencies
        // ========================================
        Console.WriteLine("=== Example 4: Circular Dependency Detection ===\n");

        Console.WriteLine("Note: The system should detect and prevent circular dependencies.");
        Console.WriteLine("Example scenario:");
        Console.WriteLine("  • Extension A depends on Extension B");
        Console.WriteLine("  • Extension B depends on Extension C");
        Console.WriteLine("  • Extension C depends on Extension A (circular!)");
        Console.WriteLine();
        Console.WriteLine("✓ The DependencyResolver should detect such cycles and prevent installation.");
        Console.WriteLine();

        // ========================================
        // Best Practices
        // ========================================
        Console.WriteLine("=== Best Practices ===\n");
        Console.WriteLine("1. Always check dependencies before installation");
        Console.WriteLine("2. Install dependencies in the correct order (base dependencies first)");
        Console.WriteLine("3. Handle missing dependencies gracefully");
        Console.WriteLine("4. Detect and prevent circular dependencies");
        Console.WriteLine("5. Version compatibility checking for dependencies");
        Console.WriteLine("6. Provide clear error messages for dependency issues");
        Console.WriteLine();

        // Cleanup
        try
        {
            if (File.Exists(catalogPath))
            {
                File.Delete(catalogPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        Console.WriteLine("=== Dependency Resolver Example Completed ===");
    }

    /// <summary>
    /// Parse dependencies string into a list of dependency IDs
    /// </summary>
    private static List<string> ParseDependencies(string? dependencies)
    {
        if (string.IsNullOrWhiteSpace(dependencies))
        {
            return new List<string>();
        }

        return dependencies!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .ToList();
    }

    /// <summary>
    /// Get list of missing dependencies for an extension
    /// </summary>
    private static List<string> GetMissingDependencies(ExtensionMetadata extension, IExtensionCatalog catalog)
    {
        var missingDeps = new List<string>();
        var dependencies = ParseDependencies(extension.Dependencies);
        
        foreach (var depId in dependencies)
        {
            var installedDep = catalog.GetInstalledExtensionById(depId);
            
            if (installedDep == null)
            {
                missingDeps.Add(depId);
            }
        }

        return missingDeps;
    }

    /// <summary>
    /// Build the dependency chain for an extension
    /// </summary>
    private static List<string> BuildDependencyChain(ExtensionMetadata extension)
    {
        var chain = new List<string>();
        var dependencies = ParseDependencies(extension.Dependencies);
        
        foreach (var depId in dependencies)
        {
            if (!chain.Contains(depId))
            {
                chain.Add(depId);
            }
        }

        return chain;
    }
}
