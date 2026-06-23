using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Compatibility;
using System;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating the use of VersionCompatibilityChecker for verifying extension compatibility.
/// Shows how to check if extensions are compatible with different host versions.
/// </summary>
public class VersionCompatibilityExample
{
    /// <summary>
    /// Run the version compatibility checking example.
    /// </summary>
    public static void RunExample()
    {
        Console.WriteLine("=== Version Compatibility Checker Example ===\n");

        // Create a version compatibility checker
        var checker = new VersionCompatibilityChecker();

        // ========================================
        // Example 1: Extension compatible with current host version
        // ========================================
        Console.WriteLine("=== Example 1: Compatible Extension ===\n");

        var compatibleExtension = new ExtensionMetadata
        {
            Id = "ext-compatible-001",
            Name = "compatible-extension",
            DisplayName = "Compatible Extension",
            Version = "1.5.0",
            Description = "An extension compatible with host version 1.0.0",
            Publisher = "Demo Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0"
        };

        var hostVersion1 = "1.5.0";

        Console.WriteLine($"Extension: {compatibleExtension.DisplayName}");
        Console.WriteLine($"  Min Host Version: {compatibleExtension.MinHostVersion}");
        Console.WriteLine($"  Max Host Version: {compatibleExtension.MaxHostVersion}");
        Console.WriteLine($"  Current Host Version: {hostVersion1}");
        Console.WriteLine();

        var isCompatible1 = checker.IsCompatible(compatibleExtension, hostVersion1);
        Console.WriteLine($"✓ Compatibility: {isCompatible1}");
        
        if (isCompatible1)
        {
            Console.WriteLine($"  The extension can be installed on host version {hostVersion1}");
        }
        Console.WriteLine();

        // ========================================
        // Example 2: Host version too old
        // ========================================
        Console.WriteLine("=== Example 2: Host Version Too Old ===\n");

        var modernExtension = new ExtensionMetadata
        {
            Id = "ext-modern-001",
            Name = "modern-extension",
            DisplayName = "Modern Extension",
            Version = "2.0.0",
            Description = "Modern extension requiring newer host",
            Publisher = "Demo Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            MinHostVersion = "2.0.0",
            MaxHostVersion = "3.0.0"
        };

        var hostVersion2 = "1.5.0";

        Console.WriteLine($"Extension: {modernExtension.DisplayName}");
        Console.WriteLine($"  Min Host Version: {modernExtension.MinHostVersion}");
        Console.WriteLine($"  Max Host Version: {modernExtension.MaxHostVersion}");
        Console.WriteLine($"  Current Host Version: {hostVersion2}");
        Console.WriteLine();

        var isCompatible2 = checker.IsCompatible(modernExtension, hostVersion2);
        Console.WriteLine($"✗ Compatibility: {isCompatible2}");
        
        if (!isCompatible2)
        {
            Console.WriteLine($"  Host version {hostVersion2} is too old!");
            Console.WriteLine($"  Minimum required version: {modernExtension.MinHostVersion}");
            Console.WriteLine($"  Action: Upgrade host to at least version {modernExtension.MinHostVersion}");
        }
        Console.WriteLine();

        // ========================================
        // Example 3: Host version too new
        // ========================================
        Console.WriteLine("=== Example 3: Host Version Too New ===\n");

        var legacyExtension = new ExtensionMetadata
        {
            Id = "ext-legacy-001",
            Name = "legacy-extension",
            DisplayName = "Legacy Extension",
            Version = "1.0.0",
            Description = "Legacy extension for older hosts",
            Publisher = "Legacy Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            MinHostVersion = "1.0.0",
            MaxHostVersion = "1.5.0"
        };

        var hostVersion3 = "2.0.0";

        Console.WriteLine($"Extension: {legacyExtension.DisplayName}");
        Console.WriteLine($"  Min Host Version: {legacyExtension.MinHostVersion}");
        Console.WriteLine($"  Max Host Version: {legacyExtension.MaxHostVersion}");
        Console.WriteLine($"  Current Host Version: {hostVersion3}");
        Console.WriteLine();

        var isCompatible3 = checker.IsCompatible(legacyExtension, hostVersion3);
        Console.WriteLine($"✗ Compatibility: {isCompatible3}");
        
        if (!isCompatible3)
        {
            Console.WriteLine($"  Host version {hostVersion3} is too new!");
            Console.WriteLine($"  Maximum supported version: {legacyExtension.MaxHostVersion}");
            Console.WriteLine($"  Action: Use an updated version of the extension or downgrade host");
        }
        Console.WriteLine();

        // ========================================
        // Example 4: Extension with no version constraints
        // ========================================
        Console.WriteLine("=== Example 4: No Version Constraints ===\n");

        var flexibleExtension = new ExtensionMetadata
        {
            Id = "ext-flexible-001",
            Name = "flexible-extension",
            DisplayName = "Flexible Extension",
            Version = "1.0.0",
            Description = "Extension with no version constraints",
            Publisher = "Flexible Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            MinHostVersion = null, // No minimum
            MaxHostVersion = null  // No maximum
        };

        var hostVersion4 = "3.5.0";

        Console.WriteLine($"Extension: {flexibleExtension.DisplayName}");
        Console.WriteLine($"  Min Host Version: {flexibleExtension.MinHostVersion ?? "Not specified"}");
        Console.WriteLine($"  Max Host Version: {flexibleExtension.MaxHostVersion ?? "Not specified"}");
        Console.WriteLine($"  Current Host Version: {hostVersion4}");
        Console.WriteLine();

        var isCompatible4 = checker.IsCompatible(flexibleExtension, hostVersion4);
        Console.WriteLine($"✓ Compatibility: {isCompatible4}");
        
        if (isCompatible4)
        {
            Console.WriteLine("  Extension has no version constraints and is compatible with any host version");
        }
        Console.WriteLine();

        // ========================================
        // Example 5: Edge cases - exact version match
        // ========================================
        Console.WriteLine("=== Example 5: Edge Cases ===\n");

        var edgeCaseExtension = new ExtensionMetadata
        {
            Id = "ext-edge-001",
            Name = "edge-extension",
            DisplayName = "Edge Case Extension",
            Version = "1.0.0",
            Description = "Testing edge cases",
            Publisher = "Test Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All,
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0"
        };

        Console.WriteLine($"Extension: {edgeCaseExtension.DisplayName}");
        Console.WriteLine($"  Min Host Version: {edgeCaseExtension.MinHostVersion}");
        Console.WriteLine($"  Max Host Version: {edgeCaseExtension.MaxHostVersion}");
        Console.WriteLine();

        // Test with minimum version (inclusive)
        var hostVersionMin = "1.0.0";
        var isCompatibleMin = checker.IsCompatible(edgeCaseExtension, hostVersionMin);
        Console.WriteLine($"Host {hostVersionMin} (minimum): {(isCompatibleMin ? "✓" : "✗")} Compatible");

        // Test with maximum version (inclusive)
        var hostVersionMax = "2.0.0";
        var isCompatibleMax = checker.IsCompatible(edgeCaseExtension, hostVersionMax);
        Console.WriteLine($"Host {hostVersionMax} (maximum): {(isCompatibleMax ? "✓" : "✗")} Compatible");

        // Test with version in range
        var hostVersionMid = "1.5.0";
        var isCompatibleMid = checker.IsCompatible(edgeCaseExtension, hostVersionMid);
        Console.WriteLine($"Host {hostVersionMid} (in range): {(isCompatibleMid ? "✓" : "✗")} Compatible");

        Console.WriteLine();

        // ========================================
        // Best Practices
        // ========================================
        Console.WriteLine("=== Best Practices ===\n");
        Console.WriteLine("1. Always check compatibility before installing extensions");
        Console.WriteLine("2. Use semantic versioning (SemVer) for version numbers");
        Console.WriteLine("3. Test extensions with minimum and maximum supported host versions");
        Console.WriteLine("4. Provide clear error messages when compatibility check fails");
        Console.WriteLine("5. Document version requirements in extension metadata");
        Console.WriteLine("6. Consider backward compatibility when updating extensions");
        Console.WriteLine("7. Version ranges should be inclusive (include min and max)");
        Console.WriteLine();

        Console.WriteLine("=== Version Compatibility Example Completed ===");
    }
}
