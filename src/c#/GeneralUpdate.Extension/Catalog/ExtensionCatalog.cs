using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Catalog;

/// <summary>
/// Implementation of extension catalog for managing installed extensions
/// </summary>
public class ExtensionCatalog : IExtensionCatalog
{
    private readonly Dictionary<string, ExtensionMetadata> _installedExtensions = new();
    private readonly string _catalogPath;
    private readonly object _lock = new();

    /// <summary>
    /// Initialize extension catalog
    /// </summary>
    /// <param name="catalogPath">Path to extensions directory</param>
    public ExtensionCatalog(string catalogPath)
    {
        _catalogPath = catalogPath;
    }

    /// <inheritdoc/>
    public void LoadInstalledExtensions()
    {
        lock (_lock)
        {
            if (!Directory.Exists(_catalogPath))
            {
                return;
            }

            try
            {
                _installedExtensions.Clear();
                
                // Traverse all subdirectories under _catalogPath
                var extensionDirs = Directory.GetDirectories(_catalogPath);
                
                foreach (var extensionDir in extensionDirs)
                {
                    if (extensionDir.Contains(".backup")) continue;
                    
                    var manifestPath = Path.Combine(extensionDir, "manifest.json");
                    
                    // Check if manifest.json exists in the subdirectory
                    if (File.Exists(manifestPath))
                    {
                        var json = File.ReadAllText(manifestPath);
                        var extension = JsonConvert.DeserializeObject<ExtensionMetadata>(json);
                        
                        if (extension != null && !string.IsNullOrEmpty(extension.Id))
                        {
                            _installedExtensions[extension.Id] = extension;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load extensions catalog: {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public List<ExtensionMetadata> GetInstalledExtensions()
    {
        lock (_lock)
        {
            return _installedExtensions.Values.ToList();
        }
    }

    /// <inheritdoc/>
    public List<ExtensionMetadata> GetInstalledExtensionsByPlatform(TargetPlatform platform)
    {
        lock (_lock)
        {
            return _installedExtensions.Values
                .Where(e => (e.SupportedPlatforms & platform) != 0)
                .ToList();
        }
    }

    /// <inheritdoc/>
    public ExtensionMetadata? GetInstalledExtensionById(string extensionId)
    {
        lock (_lock)
        {
            return _installedExtensions.TryGetValue(extensionId, out var extension) ? extension : null;
        }
    }

    /// <inheritdoc/>
    public void AddOrUpdateInstalledExtension(ExtensionMetadata extension)
    {
        lock (_lock)
        {
            _installedExtensions[extension.Id] = extension;
            SaveCatalog();
        }
    }

    /// <inheritdoc/>
    public void RemoveInstalledExtension(string extensionId)
    {
        lock (_lock)
        {
            if (_installedExtensions.TryGetValue(extensionId, out var extension))
            {
                _installedExtensions.Remove(extensionId);
                
                // Remove the extension directory if it exists
                try
                {
                    var extensionDirName = GetExtensionDirectoryName(extension);
                    var extensionDir = Path.Combine(_catalogPath, extensionDirName);
                    
                    if (Directory.Exists(extensionDir))
                    {
                        Directory.Delete(extensionDir, true);
                    }
                }
                catch
                {
                    // Silently ignore directory cleanup failures
                    // The extension is already removed from the in-memory catalog
                }
            }
        }
    }

    private void SaveCatalog()
    {
        try
        {
            // Ensure the catalog directory exists
            if (!Directory.Exists(_catalogPath))
            {
                Directory.CreateDirectory(_catalogPath);
            }

            // Save each extension to its own subdirectory with manifest.json
            foreach (var extension in _installedExtensions.Values)
            {
                var extensionDirName = GetExtensionDirectoryName(extension);
                var extensionDir = Path.Combine(_catalogPath, extensionDirName);
                
                if (!Directory.Exists(extensionDir))
                {
                    Directory.CreateDirectory(extensionDir);
                }

                var manifestPath = Path.Combine(extensionDir, "manifest.json");
                var json = JsonConvert.SerializeObject(extension, Formatting.Indented);
                File.WriteAllText(manifestPath, json);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save extensions catalog: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get a safe directory name for an extension by sanitizing the name or using the ID
    /// </summary>
    private string GetExtensionDirectoryName(ExtensionMetadata extension)
    {
        var baseName = !string.IsNullOrEmpty(extension.Name) ? extension.Name : extension.Id;
        return SanitizeDirectoryName(baseName ?? "unknown");
    }

    /// <summary>
    /// Sanitize a string to be safe for use as a directory name
    /// </summary>
    private string SanitizeDirectoryName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "unknown";
        }

        // Remove or replace invalid filesystem characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Ensure the name is not empty after sanitization
        return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
    }
}
