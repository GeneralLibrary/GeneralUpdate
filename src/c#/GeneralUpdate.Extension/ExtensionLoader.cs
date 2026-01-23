using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MyApp.Extensions.Runtime;

namespace MyApp.Extensions
{
    /// <summary>
    /// Default implementation of IExtensionLoader for loading and managing extensions.
    /// </summary>
    public class ExtensionLoader : IExtensionLoader
    {
        private readonly IRuntimeResolver _runtimeResolver;
        private readonly Dictionary<string, ExtensionManifest> _loadedExtensions;
        private readonly HashSet<string> _activeExtensions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionLoader"/> class.
        /// </summary>
        /// <param name="runtimeResolver">The runtime resolver for loading different extension types.</param>
        public ExtensionLoader(IRuntimeResolver runtimeResolver)
        {
            _runtimeResolver = runtimeResolver ?? throw new ArgumentNullException(nameof(runtimeResolver));
            _loadedExtensions = new Dictionary<string, ExtensionManifest>();
            _activeExtensions = new HashSet<string>();
        }

        /// <summary>
        /// Loads an extension from the specified path.
        /// </summary>
        /// <param name="extensionPath">The path to the extension package.</param>
        /// <returns>A task that represents the asynchronous operation, containing the loaded extension manifest.</returns>
        public async Task<ExtensionManifest> LoadAsync(string extensionPath)
        {
            try
            {
                if (!Directory.Exists(extensionPath))
                    throw new DirectoryNotFoundException($"Extension path not found: {extensionPath}");

                // Load manifest
                var manifestPath = Path.Combine(extensionPath, "manifest.json");
                if (!File.Exists(manifestPath))
                    throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ExtensionManifest>(manifestJson);

                if (manifest == null)
                    throw new InvalidOperationException("Failed to deserialize manifest");

                // Store loaded extension
                _loadedExtensions[manifest.Id] = manifest;

                return manifest;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load extension from {extensionPath}", ex);
            }
        }

        /// <summary>
        /// Unloads a previously loaded extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to unload.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        public async Task<bool> UnloadAsync(string extensionId)
        {
            try
            {
                if (!_loadedExtensions.ContainsKey(extensionId))
                    return false;

                // Deactivate if active
                if (_activeExtensions.Contains(extensionId))
                {
                    await DeactivateAsync(extensionId);
                }

                // Remove from loaded extensions
                _loadedExtensions.Remove(extensionId);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Activates a loaded extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to activate.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        public async Task<bool> ActivateAsync(string extensionId)
        {
            try
            {
                if (!_loadedExtensions.TryGetValue(extensionId, out var manifest))
                    return false;

                if (_activeExtensions.Contains(extensionId))
                    return true; // Already active

                // Parse runtime type
                if (!Enum.TryParse<RuntimeType>(manifest.Runtime, true, out var runtimeType))
                {
                    runtimeType = RuntimeType.DotNet; // Default
                }

                // Get runtime host
                var runtimeHost = _runtimeResolver.Resolve(runtimeType);
                if (runtimeHost == null)
                    return false;

                // Start runtime if not running
                if (!runtimeHost.IsRunning)
                {
                    var runtimeInfo = new RuntimeEnvironmentInfo
                    {
                        RuntimeType = runtimeType,
                        WorkingDirectory = Path.GetDirectoryName(manifest.Entrypoint)
                    };

                    await runtimeHost.StartAsync(runtimeInfo);
                }

                // Invoke extension entry point (simplified)
                // In real implementation, this would load and initialize the extension
                await Task.Delay(10); // Placeholder for actual activation

                // Mark as active
                _activeExtensions.Add(extensionId);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deactivates an active extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension to deactivate.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        public async Task<bool> DeactivateAsync(string extensionId)
        {
            try
            {
                if (!_activeExtensions.Contains(extensionId))
                    return false;

                // In real implementation, this would call cleanup/dispose on the extension
                await Task.Delay(10); // Placeholder

                // Mark as inactive
                _activeExtensions.Remove(extensionId);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether an extension is currently loaded.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>True if the extension is loaded; otherwise, false.</returns>
        public bool IsLoaded(string extensionId)
        {
            return _loadedExtensions.ContainsKey(extensionId);
        }

        /// <summary>
        /// Gets a value indicating whether an extension is currently active.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>True if the extension is active; otherwise, false.</returns>
        public bool IsActive(string extensionId)
        {
            return _activeExtensions.Contains(extensionId);
        }
    }
}
