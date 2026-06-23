using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Core;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Examples;

/// <summary>
/// Example demonstrating how to generate a valid extension package (zip file) and install it using InstallExtensionAsync.
/// This example creates a complete extension package with all required files.
/// </summary>
public class InstallExtensionExample
{
    /// <summary>
    /// Run the complete installation example.
    /// </summary>
    public static async Task RunExample()
    {
        Console.WriteLine("=== Extension Installation Example ===\n");

        // Initialize the extension host
        var options = new ExtensionHostOptions
        {
            ServerUrl = "http://127.0.0.1:7391",
            Scheme = "Bearer",
            Token = "your-token-here",
            HostVersion = "1.0.0",
            ExtensionsDirectory = "./extensions"
        };

        var host = new GeneralExtensionHost(options);

        // ========================================
        // Step 1: Generate a valid extension package
        // ========================================
        Console.WriteLine("=== Step 1: Generating Extension Package ===\n");

        var extensionMetadata = new ExtensionMetadata
        {
            Id = "demo-extension-" + Guid.NewGuid().ToString(),
            Name = "demo-extension",
            DisplayName = "Demo Extension",
            Version = "1.0.0",
            Description = "A sample extension for demonstration purposes",
            Publisher = "Demo Publisher",
            Format = ".zip",
            FileSize = 0, // Will be calculated after zip creation
            Hash = "demo-hash-sha256",
            Status = true,
            SupportedPlatforms = Common.Enums.TargetPlatform.All,
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0",
            IsPreRelease = false,
            ReleaseDate = DateTime.UtcNow,
            UploadTime = DateTime.UtcNow,
            DownloadUrl = "http://127.0.0.1:7391/Extension/download",
            Categories = "Tools,Development",
            License = "MIT",
            Dependencies = null // No dependencies for this demo
        };

        Console.WriteLine("Extension Metadata:");
        Console.WriteLine($"  ID: {extensionMetadata.Id}");
        Console.WriteLine($"  Name: {extensionMetadata.Name}");
        Console.WriteLine($"  Display Name: {extensionMetadata.DisplayName}");
        Console.WriteLine($"  Version: {extensionMetadata.Version}");
        Console.WriteLine($"  Publisher: {extensionMetadata.Publisher}");
        Console.WriteLine();

        // Generate the zip file
        var zipFileName = $"{extensionMetadata.Name}_{extensionMetadata.Version}.zip";
        var zipPath = Path.Combine(options.ExtensionsDirectory, zipFileName);
        
        Directory.CreateDirectory(options.ExtensionsDirectory);

        Console.WriteLine("Creating extension package...");
        await CreateExtensionPackage(zipPath, extensionMetadata);
        
        var fileInfo = new FileInfo(zipPath);
        extensionMetadata.FileSize = fileInfo.Length;

        Console.WriteLine($"✓ Extension package created: {zipPath}");
        Console.WriteLine($"  File Size: {extensionMetadata.FileSize} bytes ({extensionMetadata.FileSize / 1024.0:F2} KB)");
        Console.WriteLine();

        // ========================================
        // Step 2: Install the extension
        // ========================================
        Console.WriteLine("=== Step 2: Installing Extension ===\n");

        Console.WriteLine("Calling InstallExtensionAsync...");
        Console.WriteLine($"  Extension Path: {zipPath}");
        Console.WriteLine($"  Rollback Enabled: true");
        Console.WriteLine();

        try
        {
            var installSuccess = await host.InstallExtensionAsync(zipPath, rollbackOnFailure: true);

            if (installSuccess)
            {
                Console.WriteLine("✓ Extension installed successfully!");

                // Verify installation
                var extractedDir = Path.Combine(options.ExtensionsDirectory, extensionMetadata.Name);
                if (Directory.Exists(extractedDir))
                {
                    Console.WriteLine($"  Installation Directory: {extractedDir}");
                    
                    var extractedFiles = Directory.GetFiles(extractedDir);
                    Console.WriteLine($"  Extracted Files ({extractedFiles.Length}):");
                    foreach (var file in extractedFiles)
                    {
                        Console.WriteLine($"    • {Path.GetFileName(file)}");
                    }
                }

                // ========================================
                // Step 3: Update catalog and verify
                // ========================================
                Console.WriteLine("\n=== Step 3: Updating Catalog ===\n");

                host.ExtensionCatalog.AddOrUpdateInstalledExtension(extensionMetadata);
                Console.WriteLine("✓ Extension catalog updated");

                // Verify in catalog
                var installedExt = host.ExtensionCatalog.GetInstalledExtensionById(extensionMetadata.Id);
                if (installedExt != null)
                {
                    Console.WriteLine("\n✓ Extension verified in catalog:");
                    Console.WriteLine($"  ID: {installedExt.Id}");
                    Console.WriteLine($"  Name: {installedExt.DisplayName}");
                    Console.WriteLine($"  Version: {installedExt.Version}");
                    Console.WriteLine($"  Status: {(installedExt.Status == true ? "Enabled" : "Disabled")}");
                }
                else
                {
                    Console.WriteLine("\n✗ Extension not found in catalog");
                }
            }
            else
            {
                Console.WriteLine("✗ Extension installation failed!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error during installation: {ex.Message}");
            Console.WriteLine($"   Type: {ex.GetType().Name}");
        }

        Console.WriteLine("\n=== Installation Example Completed ===");
    }

    /// <summary>
    /// Creates a valid extension package (zip file) compatible with InstallExtensionAsync.
    /// The package includes metadata, mock DLL, dependencies configuration, and README.
    /// </summary>
    /// <param name="zipPath">Path where the zip file should be created</param>
    /// <param name="metadata">Extension metadata to include in the package</param>
    private static async Task CreateExtensionPackage(string zipPath, ExtensionMetadata metadata)
    {
        await Task.Run(() =>
        {
            // Create a temporary directory for the extension files
            var tempDir = Path.Combine(Path.GetTempPath(), $"extension-temp-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Create extension.json metadata file
                var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(Path.Combine(tempDir, "extension.json"), metadataJson);

                // 2. Create mock extension.dll file
                var dllContent = $@"// Mock DLL content
// In production, this would be the actual compiled extension binary
// The extension should implement the required interfaces for the host
namespace {metadata.Name}
{{
    public class ExtensionEntry
    {{
        public void Initialize()
        {{
            // Extension initialization code
        }}
    }}
}}";
                File.WriteAllText(Path.Combine(tempDir, "extension.dll"), dllContent);

                // 3. Create extension.deps.json - dependency configuration
                var depsContent = new
                {
                    runtimeTarget = new
                    {
                        name = ".NETStandard,Version=v2.0",
                        signature = ""
                    },
                    compilationOptions = new { },
                    targets = new
                    {
                        netstandard20 = new
                        {
                            extension = new
                            {
                                runtime = new
                                {
                                    extension_dll = new { }
                                }
                            }
                        }
                    },
                    libraries = new
                    {
                        extension = new
                        {
                            type = "project",
                            serviceable = false,
                            sha512 = ""
                        }
                    }
                };
                File.WriteAllText(Path.Combine(tempDir, "extension.deps.json"),
                    JsonConvert.SerializeObject(depsContent, Formatting.Indented));

                // 4. Create README.md
                var readmeContent = $@"# {metadata.DisplayName}

## Version: {metadata.Version}

### Description
{metadata.Description}

### Publisher
{metadata.Publisher}

### License
{metadata.License}

### Supported Platforms
{metadata.SupportedPlatforms}

### Compatibility
- Min Host Version: {metadata.MinHostVersion}
- Max Host Version: {metadata.MaxHostVersion}

### Categories
{metadata.Categories}

---

## Installation
This extension package is compatible with GeneralUpdate.Extension host.
The package includes:
- extension.dll: Main extension assembly
- extension.deps.json: Dependency configuration
- extension.json: Extension metadata
- README.md: This documentation file

## Usage
After installation, the extension will be available in the host application.
Refer to the extension documentation for specific usage instructions.

---
Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}
";
                File.WriteAllText(Path.Combine(tempDir, "README.md"), readmeContent);

                // 5. Create CHANGELOG.md
                var changelogContent = $@"# Changelog

## [{metadata.Version}] - {DateTime.UtcNow:yyyy-MM-dd}

### Added
- Initial release of {metadata.DisplayName}
- Basic extension functionality
- Compatible with host version {metadata.MinHostVersion} to {metadata.MaxHostVersion}

### Features
- Supports {metadata.SupportedPlatforms} platforms
- Categories: {metadata.Categories}
";
                File.WriteAllText(Path.Combine(tempDir, "CHANGELOG.md"), changelogContent);

                // 6. Create LICENSE.txt
                var licenseContent = $@"MIT License

Copyright (c) {DateTime.UtcNow.Year} {metadata.Publisher}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
";
                File.WriteAllText(Path.Combine(tempDir, "LICENSE.txt"), licenseContent);

                // 7. Create the zip file
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                ZipFile.CreateFromDirectory(tempDir, zipPath);
            }
            finally
            {
                // Clean up temporary directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        });
    }
}
