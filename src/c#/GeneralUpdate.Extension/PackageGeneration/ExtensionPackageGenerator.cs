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
        private readonly List<Func<string, Metadata.ExtensionMetadata, Task>> _customGenerators = new List<Func<string, Metadata.ExtensionMetadata, Task>>();

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
        /// <param name="generator">A custom generator function that takes the source directory and metadata.</param>
        public void AddCustomGenerator(Func<string, Metadata.ExtensionMetadata, Task> generator)
        {
            if (generator == null)
                throw new ArgumentNullException(nameof(generator));

            _customGenerators.Add(generator);
        }

        /// <summary>
        /// Generates an extension package (ZIP) from the specified source directory.
        /// Creates a manifest.json from the metadata and packages all files.
        /// </summary>
        /// <param name="sourceDirectory">The source directory containing the extension files.</param>
        /// <param name="metadata">The extension metadata metadata.</param>
        /// <param name="outputPath">The output path for the generated package file.</param>
        /// <returns>A task representing the asynchronous operation. Returns the path to the generated package.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when source directory doesn't exist.</exception>
        public async Task<string> GeneratePackageAsync(string sourceDirectory, Metadata.ExtensionMetadata metadata, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
                throw new ArgumentNullException(nameof(sourceDirectory));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
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
                await GenerateManifestAsync(tempDir, metadata);

                // Execute custom generators if any
                foreach (var customGenerator in _customGenerators)
                {
                    await customGenerator(tempDir, metadata);
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
        /// Generates the manifest.json file from the extension metadata.
        /// Follows VS Code extension manifest structure.
        /// </summary>
        /// <param name="targetDirectory">The directory where the manifest will be created.</param>
        /// <param name="metadata">The extension metadata.</param>
        private Task GenerateManifestAsync(string targetDirectory, Metadata.ExtensionMetadata metadata)
        {
            var manifestPath = Path.Combine(targetDirectory, "manifest.json");

            // Create a manifest object that includes both VS Code standard fields and our custom fields
            var manifest = new Dictionary<string, object>
            {
                ["name"] = metadata.Name,
                ["displayName"] = metadata.DisplayName,
                ["version"] = metadata.Version,
                ["description"] = metadata.Description ?? string.Empty,
                ["publisher"] = metadata.Publisher ?? string.Empty,
                ["license"] = metadata.License ?? string.Empty
            };

            // Add optional fields if present
            if (metadata.Categories != null && metadata.Categories.Count > 0)
            {
                manifest["categories"] = metadata.Categories;
            }

            // Add engine/compatibility information
            if (metadata.Compatibility != null)
            {
                var engines = new Dictionary<string, string>();
                
                if (metadata.Compatibility.MinHostVersion != null)
                {
                    engines["minHostVersion"] = metadata.Compatibility.MinHostVersion.ToString();
                }
                
                if (metadata.Compatibility.MaxHostVersion != null)
                {
                    engines["maxHostVersion"] = metadata.Compatibility.MaxHostVersion.ToString();
                }

                if (engines.Count > 0)
                {
                    manifest["engines"] = engines;
                }
            }

            // Add platform support
            manifest["supportedPlatforms"] = (int)metadata.SupportedPlatforms;

            // Add dependencies if present
            if (metadata.Dependencies != null && metadata.Dependencies.Count > 0)
            {
                manifest["dependencies"] = metadata.Dependencies;
            }

            // Add custom properties if present
            if (metadata.CustomProperties != null && metadata.CustomProperties.Count > 0)
            {
                manifest["customProperties"] = metadata.CustomProperties;
            }

            // Add package metadata
            if (!string.IsNullOrEmpty(metadata.PackageHash))
            {
                manifest["hash"] = metadata.PackageHash;
            }

            if (metadata.PackageSize > 0)
            {
                manifest["size"] = metadata.PackageSize;
            }

            if (metadata.ReleaseDate.HasValue)
            {
                manifest["releaseDate"] = metadata.ReleaseDate.Value.ToString("o");
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
