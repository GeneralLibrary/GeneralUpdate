using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.PackageGeneration
{
    /// <summary>
    /// Provides functionality to generate extension packages from source directories.
    /// Follows VS Code extension packaging conventions with flexible structure support.
    /// </summary>
    public class ExtensionPackageGenerator : IExtensionPackageGenerator
    {
        private readonly List<Func<string, Metadata.ExtensionDescriptor, Task>> _customGenerators = new List<Func<string, Metadata.ExtensionDescriptor, Task>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionPackageGenerator"/> class.
        /// </summary>
        public ExtensionPackageGenerator()
        {
        }

        /// <summary>
        /// Adds a custom generation step that will be executed during package generation.
        /// This allows for flexible extension of the generation logic.
        /// </summary>
        /// <param name="generator">A custom generator function that takes the source directory and descriptor.</param>
        public void AddCustomGenerator(Func<string, Metadata.ExtensionDescriptor, Task> generator)
        {
            if (generator == null)
                throw new ArgumentNullException(nameof(generator));

            _customGenerators.Add(generator);
        }

        /// <summary>
        /// Generates an extension package (ZIP) from the specified source directory.
        /// Creates a manifest.json from the descriptor and packages all files.
        /// </summary>
        /// <param name="sourceDirectory">The source directory containing the extension files.</param>
        /// <param name="descriptor">The extension descriptor metadata.</param>
        /// <param name="outputPath">The output path for the generated package file.</param>
        /// <returns>A task representing the asynchronous operation. Returns the path to the generated package.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when source directory doesn't exist.</exception>
        public async Task<string> GeneratePackageAsync(string sourceDirectory, Metadata.ExtensionDescriptor descriptor, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
                throw new ArgumentNullException(nameof(sourceDirectory));
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

            // Validate extension structure
            if (!ValidateExtensionStructure(sourceDirectory))
            {
                throw new InvalidOperationException($"Invalid extension structure in directory: {sourceDirectory}");
            }

            // Create output directory if it doesn't exist
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Create temporary directory for packaging
            var tempDir = Path.Combine(Path.GetTempPath(), $"ext-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Copy all files from source to temp directory
                CopyDirectory(sourceDirectory, tempDir);

                // Generate manifest.json in the temp directory
                await GenerateManifestAsync(tempDir, descriptor);

                // Execute custom generators if any
                foreach (var customGenerator in _customGenerators)
                {
                    await customGenerator(tempDir, descriptor);
                }

                // Create ZIP package
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                ZipFile.CreateFromDirectory(tempDir, outputPath, CompressionLevel.Optimal, false);

                GeneralUpdate.Common.Shared.GeneralTracer.Info($"Extension package generated successfully: {outputPath}");

                return outputPath;
            }
            finally
            {
                // Clean up temporary directory
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        GeneralUpdate.Common.Shared.GeneralTracer.Error($"Failed to delete temporary directory: {tempDir}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Validates that the source directory contains required extension files.
        /// Checks for essential files and valid structure.
        /// </summary>
        /// <param name="sourceDirectory">The source directory to validate.</param>
        /// <returns>True if valid; otherwise, false.</returns>
        public bool ValidateExtensionStructure(string sourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                return false;

            // Check if directory contains any files
            var hasFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).Length > 0;
            
            return hasFiles;
        }

        /// <summary>
        /// Generates the manifest.json file from the extension descriptor.
        /// Follows VS Code extension manifest structure.
        /// </summary>
        /// <param name="targetDirectory">The directory where the manifest will be created.</param>
        /// <param name="descriptor">The extension descriptor.</param>
        private Task GenerateManifestAsync(string targetDirectory, Metadata.ExtensionDescriptor descriptor)
        {
            var manifestPath = Path.Combine(targetDirectory, "manifest.json");

            // Create a manifest object that includes both VS Code standard fields and our custom fields
            var manifest = new Dictionary<string, object>
            {
                ["name"] = descriptor.Name,
                ["displayName"] = descriptor.DisplayName,
                ["version"] = descriptor.Version,
                ["description"] = descriptor.Description ?? string.Empty,
                ["publisher"] = descriptor.Publisher ?? string.Empty,
                ["license"] = descriptor.License ?? string.Empty
            };

            // Add optional fields if present
            if (descriptor.Categories != null && descriptor.Categories.Count > 0)
            {
                manifest["categories"] = descriptor.Categories;
            }

            // Add engine/compatibility information
            if (descriptor.Compatibility != null)
            {
                var engines = new Dictionary<string, string>();
                
                if (descriptor.Compatibility.MinHostVersion != null)
                {
                    engines["minHostVersion"] = descriptor.Compatibility.MinHostVersion.ToString();
                }
                
                if (descriptor.Compatibility.MaxHostVersion != null)
                {
                    engines["maxHostVersion"] = descriptor.Compatibility.MaxHostVersion.ToString();
                }

                if (engines.Count > 0)
                {
                    manifest["engines"] = engines;
                }
            }

            // Add platform support
            manifest["supportedPlatforms"] = (int)descriptor.SupportedPlatforms;

            // Add dependencies if present
            if (descriptor.Dependencies != null && descriptor.Dependencies.Count > 0)
            {
                manifest["dependencies"] = descriptor.Dependencies;
            }

            // Add custom properties if present
            if (descriptor.CustomProperties != null && descriptor.CustomProperties.Count > 0)
            {
                manifest["customProperties"] = descriptor.CustomProperties;
            }

            // Add package metadata
            if (!string.IsNullOrEmpty(descriptor.PackageHash))
            {
                manifest["hash"] = descriptor.PackageHash;
            }

            if (descriptor.PackageSize > 0)
            {
                manifest["size"] = descriptor.PackageSize;
            }

            if (descriptor.ReleaseDate.HasValue)
            {
                manifest["releaseDate"] = descriptor.ReleaseDate.Value.ToString("o");
            }

            // Serialize and write to file
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(manifest, options);
            File.WriteAllText(manifestPath, json);
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Recursively copies a directory and all its contents.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destDir">Destination directory path.</param>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                
                // Skip manifest.json if it exists (we'll generate our own)
                if (fileName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }

            // Recursively copy subdirectories
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }
    }
}
