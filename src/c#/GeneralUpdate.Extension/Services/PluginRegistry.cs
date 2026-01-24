using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Interfaces;
using GeneralUpdate.Extension.Models;
using GeneralUpdate.Extension.Platform;
using GeneralUpdate.Extension.Utils;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Manages the registry of installed and available plugins.
    /// Provides plugin discovery, version tracking, and update detection.
    /// </summary>
    public class PluginRegistry : IPluginRegistry
    {
        private readonly string _localPluginsPath;
        private readonly string _registryFilePath;
        private readonly Dictionary<string, PluginInfo> _localPlugins = new Dictionary<string, PluginInfo>();
        private readonly object _lock = new object();
        private readonly PlatformResolver _platformResolver;

        /// <summary>
        /// Initializes a new instance of PluginRegistry.
        /// </summary>
        /// <param name="localPluginsPath">Path to the local plugins directory.</param>
        public PluginRegistry(string localPluginsPath)
        {
            if (string.IsNullOrWhiteSpace(localPluginsPath))
                throw new ArgumentException("Local plugins path cannot be null or empty.", nameof(localPluginsPath));

            _localPluginsPath = localPluginsPath;
            _registryFilePath = Path.Combine(localPluginsPath, "plugin-registry.json");
            _platformResolver = PlatformResolver.Instance;

            // Ensure directory exists
            if (!Directory.Exists(_localPluginsPath))
            {
                Directory.CreateDirectory(_localPluginsPath);
            }

            // Load existing registry
            LoadRegistryAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets all locally installed plugins.
        /// </summary>
        public Task<List<PluginInfo>> GetLocalPluginsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_localPlugins.Values.ToList());
            }
        }

        /// <summary>
        /// Gets the list of plugins available on the server/marketplace.
        /// This is a placeholder that should be implemented based on your server API.
        /// </summary>
        public async Task<List<PluginInfo>> GetServerPluginsAsync()
        {
            // TODO: Implement actual server API call
            // This is a placeholder implementation
            // In a real scenario, you would:
            // 1. Make HTTP request to plugin marketplace/server
            // 2. Parse response into List<PluginInfo>
            // 3. Filter by platform compatibility
            await Task.CompletedTask;
            return new List<PluginInfo>();
        }

        /// <summary>
        /// Gets a specific plugin by its ID.
        /// </summary>
        public Task<PluginInfo> GetPluginByIdAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            lock (_lock)
            {
                _localPlugins.TryGetValue(pluginId, out var plugin);
                return Task.FromResult(plugin);
            }
        }

        /// <summary>
        /// Checks for updates for all installed plugins.
        /// </summary>
        public async Task<List<PluginInfo>> CheckForUpdatesAsync()
        {
            var localPlugins = await GetLocalPluginsAsync();
            var serverPlugins = await GetServerPluginsAsync();
            var pluginsWithUpdates = new List<PluginInfo>();

            foreach (var localPlugin in localPlugins)
            {
                var serverPlugin = serverPlugins.FirstOrDefault(p => p.Id == localPlugin.Id);
                if (serverPlugin != null && 
                    !string.IsNullOrEmpty(localPlugin.Version) && 
                    !string.IsNullOrEmpty(serverPlugin.Version))
                {
                    if (VersionComparer.IsUpdateAvailable(localPlugin.Version, serverPlugin.Version))
                    {
                        localPlugin.UpdateAvailable = true;
                        localPlugin.AvailableVersion = serverPlugin.Version;
                        localPlugin.DownloadUrl = serverPlugin.DownloadUrl;
                        pluginsWithUpdates.Add(localPlugin);
                    }
                }
            }

            // Update registry with new information
            await SaveRegistryAsync();

            return pluginsWithUpdates;
        }

        /// <summary>
        /// Checks for updates for a specific plugin.
        /// </summary>
        public async Task<PluginInfo> CheckForUpdateAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            var localPlugin = await GetPluginByIdAsync(pluginId);
            if (localPlugin == null)
                return null;

            var serverPlugins = await GetServerPluginsAsync();
            var serverPlugin = serverPlugins.FirstOrDefault(p => p.Id == pluginId);

            if (serverPlugin != null && 
                !string.IsNullOrEmpty(localPlugin.Version) && 
                !string.IsNullOrEmpty(serverPlugin.Version))
            {
                if (VersionComparer.IsUpdateAvailable(localPlugin.Version, serverPlugin.Version))
                {
                    localPlugin.UpdateAvailable = true;
                    localPlugin.AvailableVersion = serverPlugin.Version;
                    localPlugin.DownloadUrl = serverPlugin.DownloadUrl;
                    await SaveRegistryAsync();
                }
            }

            return localPlugin;
        }

        /// <summary>
        /// Registers a newly installed plugin in the registry.
        /// </summary>
        public async Task RegisterPluginAsync(PluginInfo plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (string.IsNullOrWhiteSpace(plugin.Id))
                throw new ArgumentException("Plugin ID cannot be null or empty.");

            lock (_lock)
            {
                _localPlugins[plugin.Id] = plugin;
            }

            await SaveRegistryAsync();
        }

        /// <summary>
        /// Unregisters a plugin from the registry.
        /// </summary>
        public async Task UnregisterPluginAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            lock (_lock)
            {
                _localPlugins.Remove(pluginId);
            }

            await SaveRegistryAsync();
        }

        /// <summary>
        /// Updates the metadata of an existing plugin.
        /// </summary>
        public async Task UpdatePluginMetadataAsync(PluginInfo plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (string.IsNullOrWhiteSpace(plugin.Id))
                throw new ArgumentException("Plugin ID cannot be null or empty.");

            lock (_lock)
            {
                if (_localPlugins.ContainsKey(plugin.Id))
                {
                    _localPlugins[plugin.Id] = plugin;
                }
            }

            await SaveRegistryAsync();
        }

        /// <summary>
        /// Enables a plugin.
        /// </summary>
        public async Task EnablePluginAsync(string pluginId)
        {
            var plugin = await GetPluginByIdAsync(pluginId);
            if (plugin != null)
            {
                plugin.IsEnabled = true;
                await UpdatePluginMetadataAsync(plugin);
            }
        }

        /// <summary>
        /// Disables a plugin.
        /// </summary>
        public async Task DisablePluginAsync(string pluginId)
        {
            var plugin = await GetPluginByIdAsync(pluginId);
            if (plugin != null)
            {
                plugin.IsEnabled = false;
                await UpdatePluginMetadataAsync(plugin);
            }
        }

        /// <summary>
        /// Gets plugins filtered by platform compatibility.
        /// </summary>
        public async Task<List<PluginInfo>> GetPluginsByPlatformAsync(string platform)
        {
            var allPlugins = await GetLocalPluginsAsync();
            return allPlugins.Where(p => _platformResolver.IsCompatible(p.Platform)).ToList();
        }

        private async Task LoadRegistryAsync()
        {
            if (!File.Exists(_registryFilePath))
                return;

            try
            {
                var json = await File.ReadAllTextAsync(_registryFilePath);
                var plugins = JsonSerializer.Deserialize<List<PluginInfo>>(json);

                if (plugins != null)
                {
                    lock (_lock)
                    {
                        _localPlugins.Clear();
                        foreach (var plugin in plugins)
                        {
                            _localPlugins[plugin.Id] = plugin;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - start with empty registry
                Console.WriteLine($"Error loading plugin registry: {ex.Message}");
            }
        }

        private async Task SaveRegistryAsync()
        {
            try
            {
                List<PluginInfo> plugins;
                lock (_lock)
                {
                    plugins = _localPlugins.Values.ToList();
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(plugins, options);
                await File.WriteAllTextAsync(_registryFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving plugin registry: {ex.Message}");
                throw;
            }
        }
    }
}
