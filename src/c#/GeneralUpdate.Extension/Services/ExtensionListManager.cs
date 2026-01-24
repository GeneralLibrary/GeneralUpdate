using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Manages local and remote extension lists.
    /// </summary>
    public class ExtensionListManager
    {
        private readonly string _localExtensionsPath;
        private readonly List<LocalExtension> _localExtensions = new List<LocalExtension>();

        /// <summary>
        /// Initializes a new instance of the ExtensionListManager.
        /// </summary>
        /// <param name="localExtensionsPath">Path to the directory where extension metadata is stored.</param>
        public ExtensionListManager(string localExtensionsPath)
        {
            _localExtensionsPath = localExtensionsPath ?? throw new ArgumentNullException(nameof(localExtensionsPath));
            
            if (!Directory.Exists(_localExtensionsPath))
            {
                Directory.CreateDirectory(_localExtensionsPath);
            }
        }

        /// <summary>
        /// Loads local extensions from the file system.
        /// </summary>
        public void LoadLocalExtensions()
        {
            _localExtensions.Clear();
            
            var manifestFiles = Directory.GetFiles(_localExtensionsPath, "manifest.json", SearchOption.AllDirectories);
            
            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    var json = File.ReadAllText(manifestFile);
                    var localExtension = JsonSerializer.Deserialize<LocalExtension>(json);
                    
                    if (localExtension != null)
                    {
                        localExtension.InstallPath = Path.GetDirectoryName(manifestFile) ?? string.Empty;
                        _localExtensions.Add(localExtension);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other extensions
                    Console.WriteLine($"Error loading extension from {manifestFile}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets all locally installed extensions.
        /// </summary>
        /// <returns>List of local extensions.</returns>
        public List<LocalExtension> GetLocalExtensions()
        {
            return new List<LocalExtension>(_localExtensions);
        }

        /// <summary>
        /// Gets local extensions filtered by platform.
        /// </summary>
        /// <param name="platform">Platform to filter by.</param>
        /// <returns>List of local extensions for the specified platform.</returns>
        public List<LocalExtension> GetLocalExtensionsByPlatform(ExtensionPlatform platform)
        {
            return _localExtensions
                .Where(ext => (ext.Metadata.SupportedPlatforms & platform) != 0)
                .ToList();
        }

        /// <summary>
        /// Gets a local extension by ID.
        /// </summary>
        /// <param name="extensionId">The extension ID.</param>
        /// <returns>The local extension or null if not found.</returns>
        public LocalExtension? GetLocalExtensionById(string extensionId)
        {
            return _localExtensions.FirstOrDefault(ext => ext.Metadata.Id == extensionId);
        }

        /// <summary>
        /// Adds or updates a local extension.
        /// </summary>
        /// <param name="extension">The extension to add or update.</param>
        public void AddOrUpdateLocalExtension(LocalExtension extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            var existing = _localExtensions.FirstOrDefault(ext => ext.Metadata.Id == extension.Metadata.Id);
            
            if (existing != null)
            {
                _localExtensions.Remove(existing);
            }
            
            _localExtensions.Add(extension);
            SaveLocalExtension(extension);
        }

        /// <summary>
        /// Removes a local extension.
        /// </summary>
        /// <param name="extensionId">The extension ID to remove.</param>
        public void RemoveLocalExtension(string extensionId)
        {
            var extension = _localExtensions.FirstOrDefault(ext => ext.Metadata.Id == extensionId);
            
            if (extension != null)
            {
                _localExtensions.Remove(extension);
                
                // Remove the manifest file
                var manifestPath = Path.Combine(extension.InstallPath, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                }
            }
        }

        /// <summary>
        /// Saves a local extension manifest to disk.
        /// </summary>
        /// <param name="extension">The extension to save.</param>
        private void SaveLocalExtension(LocalExtension extension)
        {
            var manifestPath = Path.Combine(extension.InstallPath, "manifest.json");
            var json = JsonSerializer.Serialize(extension, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);
        }

        /// <summary>
        /// Parses remote extensions from JSON string.
        /// </summary>
        /// <param name="json">JSON string containing remote extensions.</param>
        /// <returns>List of remote extensions.</returns>
        public List<RemoteExtension> ParseRemoteExtensions(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<RemoteExtension>();

            try
            {
                var extensions = JsonSerializer.Deserialize<List<RemoteExtension>>(json);
                return extensions ?? new List<RemoteExtension>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing remote extensions: {ex.Message}");
                return new List<RemoteExtension>();
            }
        }

        /// <summary>
        /// Filters remote extensions by platform.
        /// </summary>
        /// <param name="remoteExtensions">List of remote extensions.</param>
        /// <param name="platform">Platform to filter by.</param>
        /// <returns>Filtered list of remote extensions.</returns>
        public List<RemoteExtension> FilterRemoteExtensionsByPlatform(List<RemoteExtension> remoteExtensions, ExtensionPlatform platform)
        {
            return remoteExtensions
                .Where(ext => (ext.Metadata.SupportedPlatforms & platform) != 0)
                .ToList();
        }
    }
}
