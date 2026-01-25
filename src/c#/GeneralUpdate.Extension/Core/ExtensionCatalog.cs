using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GeneralUpdate.Extension.Core
{
    /// <summary>
    /// Manages the catalog of installed and available extensions.
    /// Provides centralized access to extension metadata and storage.
    /// </summary>
    public class ExtensionCatalog : IExtensionCatalog
    {
        private readonly string _installBasePath;
        private readonly List<Installation.InstalledExtension> _installedExtensions = new List<Installation.InstalledExtension>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionCatalog"/> class.
        /// </summary>
        /// <param name="installBasePath">The base directory where extensions are installed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="installBasePath"/> is null or whitespace.</exception>
        public ExtensionCatalog(string installBasePath)
        {
            if (string.IsNullOrWhiteSpace(installBasePath))
                throw new ArgumentNullException(nameof(installBasePath));

            _installBasePath = installBasePath;

            if (!Directory.Exists(_installBasePath))
            {
                Directory.CreateDirectory(_installBasePath);
            }
        }

        /// <summary>
        /// Loads all locally installed extensions from the file system by scanning for manifest files.
        /// Existing entries in the catalog are cleared before loading.
        /// </summary>
        public void LoadInstalledExtensions()
        {
            lock (_lockObject)
            {
                _installedExtensions.Clear();

                var manifestFiles = Directory.GetFiles(_installBasePath, "manifest.json", SearchOption.AllDirectories);

                foreach (var manifestFile in manifestFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(manifestFile);
                        var extension = JsonSerializer.Deserialize<Installation.InstalledExtension>(json);

                        if (extension != null)
                        {
                            extension.InstallPath = Path.GetDirectoryName(manifestFile) ?? string.Empty;
                            _installedExtensions.Add(extension);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other extensions
                        GeneralUpdate.Common.Shared.GeneralTracer.Error($"Failed to load extension manifest from {manifestFile}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Gets all locally installed extensions currently in the catalog.
        /// </summary>
        /// <returns>A defensive copy of the installed extensions list.</returns>
        public List<Installation.InstalledExtension> GetInstalledExtensions()
        {
            lock (_lockObject)
            {
                return new List<Installation.InstalledExtension>(_installedExtensions);
            }
        }

        /// <summary>
        /// Gets installed extensions that support the specified target platform.
        /// </summary>
        /// <param name="platform">The platform to filter by (supports flag-based filtering).</param>
        /// <returns>A list of extensions compatible with the specified platform.</returns>
        public List<Installation.InstalledExtension> GetInstalledExtensionsByPlatform(Metadata.TargetPlatform platform)
        {
            lock (_lockObject)
            {
                return _installedExtensions
                    .Where(ext => (ext.Descriptor.SupportedPlatforms & platform) != 0)
                    .ToList();
            }
        }

        /// <summary>
        /// Retrieves a specific installed extension by its unique identifier.
        /// </summary>
        /// <param name="extensionName">The unique extension identifier to search for.</param>
        /// <returns>The matching extension if found; otherwise, null.</returns>
        public Installation.InstalledExtension? GetInstalledExtensionById(string extensionName)
        {
            lock (_lockObject)
            {
                return _installedExtensions.FirstOrDefault(ext => ext.Descriptor.Name == extensionName);
            }
        }

        /// <summary>
        /// Adds a new extension to the catalog or updates an existing one.
        /// The extension manifest is automatically persisted to disk.
        /// </summary>
        /// <param name="extension">The extension to add or update.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="extension"/> is null.</exception>
        public void AddOrUpdateInstalledExtension(Installation.InstalledExtension extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            lock (_lockObject)
            {
                var existing = _installedExtensions.FirstOrDefault(ext => ext.Descriptor.Name == extension.Descriptor.Name);

                if (existing != null)
                {
                    _installedExtensions.Remove(existing);
                }

                _installedExtensions.Add(extension);
                SaveExtensionManifest(extension);
            }
        }

        /// <summary>
        /// Removes an installed extension from the catalog and deletes its manifest file.
        /// The extension directory is not removed.
        /// </summary>
        /// <param name="extensionName">The unique identifier of the extension to remove.</param>
        public void RemoveInstalledExtension(string extensionName)
        {
            lock (_lockObject)
            {
                var extension = _installedExtensions.FirstOrDefault(ext => ext.Descriptor.Name == extensionName);

                if (extension != null)
                {
                    _installedExtensions.Remove(extension);

                    // Remove the manifest file
                    var manifestPath = Path.Combine(extension.InstallPath, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        try
                        {
                            File.Delete(manifestPath);
                        }
                        catch (Exception ex)
                        {
                            GeneralUpdate.Common.Shared.GeneralTracer.Error($"Failed to delete manifest file {manifestPath}", ex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parses a JSON string containing available extensions from a remote source.
        /// </summary>
        /// <param name="json">The JSON-formatted extension data.</param>
        /// <returns>A list of parsed available extensions, or an empty list if parsing fails.</returns>
        public List<Metadata.AvailableExtension> ParseAvailableExtensions(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<Metadata.AvailableExtension>();

            try
            {
                var extensions = JsonSerializer.Deserialize<List<Metadata.AvailableExtension>>(json);
                return extensions ?? new List<Metadata.AvailableExtension>();
            }
            catch (Exception ex)
            {
                GeneralUpdate.Common.Shared.GeneralTracer.Error("Failed to parse available extensions JSON", ex);
                return new List<Metadata.AvailableExtension>();
            }
        }

        /// <summary>
        /// Filters available extensions to only include those supporting the specified platform.
        /// </summary>
        /// <param name="extensions">The list of extensions to filter.</param>
        /// <param name="platform">The target platform to filter by.</param>
        /// <returns>A filtered list of platform-compatible extensions.</returns>
        public List<Metadata.AvailableExtension> FilterByPlatform(List<Metadata.AvailableExtension> extensions, Metadata.TargetPlatform platform)
        {
            if (extensions == null)
                return new List<Metadata.AvailableExtension>();

            return extensions
                .Where(ext => (ext.Descriptor.SupportedPlatforms & platform) != 0)
                .ToList();
        }

        /// <summary>
        /// Persists an extension's manifest file to disk in JSON format.
        /// </summary>
        /// <param name="extension">The extension whose manifest should be saved.</param>
        private void SaveExtensionManifest(Installation.InstalledExtension extension)
        {
            try
            {
                var manifestPath = Path.Combine(extension.InstallPath, "manifest.json");
                var json = JsonSerializer.Serialize(extension, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, json);
            }
            catch (Exception ex)
            {
                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Failed to save extension manifest for {extension.Descriptor.Name}", ex);
            }
        }
    }
}
