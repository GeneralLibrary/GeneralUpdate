using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Compatibility;
using System;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating the use of PlatformMatcher for checking platform compatibility.
/// Shows how to verify if extensions support different target platforms.
/// </summary>
public class PlatformMatcherExample
{
    /// <summary>
    /// Run the platform matching example.
    /// </summary>
    public static void RunExample()
    {
        Console.WriteLine("=== Platform Matcher Example ===\n");

        // Create a platform matcher
        var matcher = new PlatformMatcher();

        // Display current platform
        Console.WriteLine("Current Platform Information:");
        Console.WriteLine($"  OS Description: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine($"  OS Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Console.WriteLine();

        // ========================================
        // Example 1: Extension supporting all platforms
        // ========================================
        Console.WriteLine("=== Example 1: All Platforms Supported ===\n");

        var allPlatformsExt = new ExtensionMetadata
        {
            Id = "ext-all-001",
            Name = "universal-extension",
            DisplayName = "Universal Extension",
            Version = "1.0.0",
            Description = "Cross-platform extension supporting all operating systems",
            Publisher = "Universal Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.All
        };

        Console.WriteLine($"Extension: {allPlatformsExt.DisplayName}");
        Console.WriteLine($"  Supported Platforms: {allPlatformsExt.SupportedPlatforms}");
        Console.WriteLine();

        var isSupported1 = matcher.IsCurrentPlatformSupported(allPlatformsExt);
        Console.WriteLine($"✓ Current Platform Supported: {isSupported1}");
        Console.WriteLine("  This extension can run on Windows, Linux, and macOS");
        Console.WriteLine();

        // ========================================
        // Example 2: Windows-only extension
        // ========================================
        Console.WriteLine("=== Example 2: Windows-Only Extension ===\n");

        var windowsExt = new ExtensionMetadata
        {
            Id = "ext-win-001",
            Name = "windows-extension",
            DisplayName = "Windows Extension",
            Version = "1.0.0",
            Description = "Extension specifically for Windows platform",
            Publisher = "Windows Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows
        };

        Console.WriteLine($"Extension: {windowsExt.DisplayName}");
        Console.WriteLine($"  Supported Platforms: {windowsExt.SupportedPlatforms}");
        Console.WriteLine();

        var isSupported2 = matcher.IsCurrentPlatformSupported(windowsExt);
        Console.WriteLine($"Current Platform Supported: {isSupported2}");
        
        if (isSupported2)
        {
            Console.WriteLine("  ✓ This extension can run on the current Windows platform");
        }
        else
        {
            Console.WriteLine("  ✗ This extension requires Windows OS");
        }
        Console.WriteLine();

        // ========================================
        // Example 3: Linux-only extension
        // ========================================
        Console.WriteLine("=== Example 3: Linux-Only Extension ===\n");

        var linuxExt = new ExtensionMetadata
        {
            Id = "ext-linux-001",
            Name = "linux-extension",
            DisplayName = "Linux Extension",
            Version = "1.0.0",
            Description = "Extension specifically for Linux platform",
            Publisher = "Linux Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.Linux
        };

        Console.WriteLine($"Extension: {linuxExt.DisplayName}");
        Console.WriteLine($"  Supported Platforms: {linuxExt.SupportedPlatforms}");
        Console.WriteLine();

        var isSupported3 = matcher.IsCurrentPlatformSupported(linuxExt);
        Console.WriteLine($"Current Platform Supported: {isSupported3}");
        
        if (isSupported3)
        {
            Console.WriteLine("  ✓ This extension can run on the current Linux platform");
        }
        else
        {
            Console.WriteLine("  ✗ This extension requires Linux OS");
        }
        Console.WriteLine();

        // ========================================
        // Example 4: macOS-only extension
        // ========================================
        Console.WriteLine("=== Example 4: macOS-Only Extension ===\n");

        var macExt = new ExtensionMetadata
        {
            Id = "ext-mac-001",
            Name = "macos-extension",
            DisplayName = "macOS Extension",
            Version = "1.0.0",
            Description = "Extension specifically for macOS platform",
            Publisher = "Apple Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.MacOS
        };

        Console.WriteLine($"Extension: {macExt.DisplayName}");
        Console.WriteLine($"  Supported Platforms: {macExt.SupportedPlatforms}");
        Console.WriteLine();

        var isSupported4 = matcher.IsCurrentPlatformSupported(macExt);
        Console.WriteLine($"Current Platform Supported: {isSupported4}");
        
        if (isSupported4)
        {
            Console.WriteLine("  ✓ This extension can run on the current macOS platform");
        }
        else
        {
            Console.WriteLine("  ✗ This extension requires macOS");
        }
        Console.WriteLine();

        // ========================================
        // Example 5: Multiple platforms (Windows | Linux)
        // ========================================
        Console.WriteLine("=== Example 5: Multiple Platforms (Windows | Linux) ===\n");

        var multiPlatformExt = new ExtensionMetadata
        {
            Id = "ext-multi-001",
            Name = "multi-platform-extension",
            DisplayName = "Multi-Platform Extension",
            Version = "1.0.0",
            Description = "Extension supporting Windows and Linux",
            Publisher = "Multi Publisher",
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux
        };

        Console.WriteLine($"Extension: {multiPlatformExt.DisplayName}");
        Console.WriteLine($"  Supported Platforms: {multiPlatformExt.SupportedPlatforms}");
        Console.WriteLine($"  Breakdown:");
        
        if ((multiPlatformExt.SupportedPlatforms & TargetPlatform.Windows) != 0)
        {
            Console.WriteLine("    • Windows");
        }
        if ((multiPlatformExt.SupportedPlatforms & TargetPlatform.Linux) != 0)
        {
            Console.WriteLine("    • Linux");
        }
        if ((multiPlatformExt.SupportedPlatforms & TargetPlatform.MacOS) != 0)
        {
            Console.WriteLine("    • macOS");
        }
        Console.WriteLine();

        var isSupported5 = matcher.IsCurrentPlatformSupported(multiPlatformExt);
        Console.WriteLine($"Current Platform Supported: {isSupported5}");
        
        if (isSupported5)
        {
            Console.WriteLine("  ✓ This extension can run on the current platform");
        }
        else
        {
            Console.WriteLine("  ✗ This extension does not support the current platform");
        }
        Console.WriteLine();

        // ========================================
        // Example 6: Platform-specific features check
        // ========================================
        Console.WriteLine("=== Example 6: Platform-Specific Features ===\n");

        Console.WriteLine("Checking platform-specific capabilities:");
        Console.WriteLine();

        // Check each platform individually
        var testExt = new ExtensionMetadata
        {
            Id = "test-ext",
            Name = "test",
            DisplayName = "Test Extension",
            Version = "1.0.0",
            Status = true,
            SupportedPlatforms = TargetPlatform.Windows
        };

        Console.WriteLine("Testing Windows platform:");
        testExt.SupportedPlatforms = TargetPlatform.Windows;
        Console.WriteLine($"  Windows Extension: {(matcher.IsCurrentPlatformSupported(testExt) ? "✓ Supported" : "✗ Not Supported")}");

        Console.WriteLine();
        Console.WriteLine("Testing Linux platform:");
        testExt.SupportedPlatforms = TargetPlatform.Linux;
        Console.WriteLine($"  Linux Extension: {(matcher.IsCurrentPlatformSupported(testExt) ? "✓ Supported" : "✗ Not Supported")}");

        Console.WriteLine();
        Console.WriteLine("Testing macOS platform:");
        testExt.SupportedPlatforms = TargetPlatform.MacOS;
        Console.WriteLine($"  macOS Extension: {(matcher.IsCurrentPlatformSupported(testExt) ? "✓ Supported" : "✗ Not Supported")}");

        Console.WriteLine();

        // ========================================
        // Best Practices
        // ========================================
        Console.WriteLine("=== Best Practices ===\n");
        Console.WriteLine("1. Always check platform compatibility before installing extensions");
        Console.WriteLine("2. Use TargetPlatform.All for cross-platform extensions when possible");
        Console.WriteLine("3. Clearly document platform-specific features and limitations");
        Console.WriteLine("4. Test extensions on all supported platforms");
        Console.WriteLine("5. Use bitwise flags (|) to support multiple platforms");
        Console.WriteLine("6. Provide meaningful error messages for unsupported platforms");
        Console.WriteLine("7. Consider platform-specific dependencies and requirements");
        Console.WriteLine();

        // ========================================
        // Platform Flags Reference
        // ========================================
        Console.WriteLine("=== TargetPlatform Flags Reference ===\n");
        Console.WriteLine("Available platform flags:");
        Console.WriteLine($"  • TargetPlatform.Windows = {(int)TargetPlatform.Windows}");
        Console.WriteLine($"  • TargetPlatform.Linux = {(int)TargetPlatform.Linux}");
        Console.WriteLine($"  • TargetPlatform.MacOS = {(int)TargetPlatform.MacOS}");
        Console.WriteLine($"  • TargetPlatform.All = {(int)TargetPlatform.All}");
        Console.WriteLine();
        Console.WriteLine("Combine platforms using bitwise OR (|):");
        Console.WriteLine($"  Windows | Linux = {(int)(TargetPlatform.Windows | TargetPlatform.Linux)}");
        Console.WriteLine();

        Console.WriteLine("=== Platform Matcher Example Completed ===");
    }
}
