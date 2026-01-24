using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using GeneralUpdate.Differential;
using GeneralUpdate.Extension.Interfaces;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Handles plugin installation, updates, and rollback.
    /// Integrates with GeneralUpdate.Differential for patch restoration.
    /// </summary>
    public class PluginInstaller : IPluginInstaller
    {
        private readonly string _pluginsBasePath;
        private readonly string _backupsPath;
        private readonly IPluginRegistry _pluginRegistry;

        /// <summary>
        /// Initializes a new instance of PluginInstaller.
        /// </summary>
        /// <param name="pluginsBasePath">Base directory for plugin installations.</param>
        /// <param name="pluginRegistry">Plugin registry for metadata management.</param>
        public PluginInstaller(string pluginsBasePath, IPluginRegistry pluginRegistry)
        {
            if (string.IsNullOrWhiteSpace(pluginsBasePath))
                throw new ArgumentException("Plugins base path cannot be null or empty.", nameof(pluginsBasePath));

            _pluginsBasePath = pluginsBasePath;
            _backupsPath = Path.Combine(pluginsBasePath, ".backups");
            _pluginRegistry = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));

            // Ensure directories exist
            if (!Directory.Exists(_pluginsBasePath))
            {
                Directory.CreateDirectory(_pluginsBasePath);
            }
            if (!Directory.Exists(_backupsPath))
            {
                Directory.CreateDirectory(_backupsPath);
            }
        }

        /// <summary>
        /// Installs a plugin from a downloaded package.
        /// </summary>
        public async Task<bool> InstallAsync(PluginInfo plugin, string packagePath)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("Package path cannot be null or empty.", nameof(packagePath));
            if (!File.Exists(packagePath))
                throw new FileNotFoundException($"Package not found: {packagePath}");

            try
            {
                // Determine installation path
                var installPath = Path.Combine(_pluginsBasePath, plugin.Id);

                // Extract package
                if (Directory.Exists(installPath))
                {
                    // If already exists, this is an update, not a fresh install
                    return await UpdateAsync(plugin, packagePath);
                }

                // Create installation directory
                Directory.CreateDirectory(installPath);

                // Extract ZIP package
                await Task.Run(() => ZipFile.ExtractToDirectory(packagePath, installPath));

                // Update plugin metadata
                plugin.InstallPath = installPath;
                plugin.IsInstalled = true;
                plugin.LastUpdated = DateTime.UtcNow;

                // Register with registry
                await _pluginRegistry.RegisterPluginAsync(plugin);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing plugin {plugin.Id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uninstalls a plugin and removes its files.
        /// </summary>
        public async Task<bool> UninstallAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                if (plugin == null)
                    return false;

                var installPath = plugin.InstallPath ?? Path.Combine(_pluginsBasePath, pluginId);

                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, true);
                }

                // Unregister from registry
                await _pluginRegistry.UnregisterPluginAsync(pluginId);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uninstalling plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates an existing plugin using differential patching.
        /// Utilizes GeneralUpdate.Differential.Dirty for patch application.
        /// </summary>
        public async Task<bool> UpdateAsync(PluginInfo plugin, string patchPath)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (string.IsNullOrWhiteSpace(patchPath))
                throw new ArgumentException("Patch path cannot be null or empty.", nameof(patchPath));
            if (!File.Exists(patchPath))
                throw new FileNotFoundException($"Patch file not found: {patchPath}");

            try
            {
                var installPath = plugin.InstallPath ?? Path.Combine(_pluginsBasePath, plugin.Id);

                if (!Directory.Exists(installPath))
                {
                    // Plugin not installed, perform fresh install
                    return await InstallAsync(plugin, patchPath);
                }

                // Create backup before update
                var backupPath = await BackupAsync(plugin.Id);
                if (string.IsNullOrEmpty(backupPath))
                {
                    Console.WriteLine($"Warning: Failed to create backup for plugin {plugin.Id}");
                }

                // Extract patch to temporary directory
                var tempPatchDir = Path.Combine(Path.GetTempPath(), $"plugin-patch-{Guid.NewGuid()}");
                Directory.CreateDirectory(tempPatchDir);

                try
                {
                    // Extract patch package
                    await Task.Run(() => ZipFile.ExtractToDirectory(patchPath, tempPatchDir));

                    // Apply differential patch using Dirty function
                    await DifferentialCore.Instance.Dirty(installPath, tempPatchDir);

                    // Update plugin metadata
                    plugin.Version = plugin.AvailableVersion ?? plugin.Version;
                    plugin.UpdateAvailable = false;
                    plugin.AvailableVersion = null;
                    plugin.LastUpdated = DateTime.UtcNow;

                    await _pluginRegistry.UpdatePluginMetadataAsync(plugin);

                    return true;
                }
                finally
                {
                    // Clean up temporary directory
                    if (Directory.Exists(tempPatchDir))
                    {
                        try
                        {
                            Directory.Delete(tempPatchDir, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating plugin {plugin.Id}: {ex.Message}");
                // Attempt rollback
                await RestoreAsync(plugin.Id, null);
                return false;
            }
        }

        /// <summary>
        /// Creates a backup of the current plugin installation before updating.
        /// </summary>
        public async Task<string> BackupAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                if (plugin == null)
                    return null;

                var installPath = plugin.InstallPath ?? Path.Combine(_pluginsBasePath, pluginId);
                if (!Directory.Exists(installPath))
                    return null;

                // Create backup filename with timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var backupFileName = $"{pluginId}-{plugin.Version}-{timestamp}.zip";
                var backupPath = Path.Combine(_backupsPath, backupFileName);

                // Create backup ZIP
                await Task.Run(() => ZipFile.CreateFromDirectory(installPath, backupPath));

                // Keep only the last 3 backups
                await CleanOldBackupsAsync(pluginId, 3);

                return backupPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating backup for plugin {pluginId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Restores a plugin from a backup.
        /// </summary>
        public async Task<bool> RestoreAsync(string pluginId, string backupPath)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            try
            {
                // If no backup path specified, find the most recent backup
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    backupPath = FindLatestBackup(pluginId);
                    if (string.IsNullOrEmpty(backupPath))
                    {
                        Console.WriteLine($"No backup found for plugin {pluginId}");
                        return false;
                    }
                }

                if (!File.Exists(backupPath))
                {
                    Console.WriteLine($"Backup file not found: {backupPath}");
                    return false;
                }

                var plugin = await _pluginRegistry.GetPluginByIdAsync(pluginId);
                if (plugin == null)
                    return false;

                var installPath = plugin.InstallPath ?? Path.Combine(_pluginsBasePath, pluginId);

                // Remove current installation
                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, true);
                }

                // Restore from backup
                Directory.CreateDirectory(installPath);
                await Task.Run(() => ZipFile.ExtractToDirectory(backupPath, installPath));

                plugin.LastUpdated = DateTime.UtcNow;
                await _pluginRegistry.UpdatePluginMetadataAsync(plugin);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates a plugin package before installation.
        /// </summary>
        public Task<bool> ValidatePackageAsync(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                return Task.FromResult(false);

            if (!File.Exists(packagePath))
                return Task.FromResult(false);

            try
            {
                // Basic validation: check if it's a valid ZIP file
                using (var zip = ZipFile.OpenRead(packagePath))
                {
                    // Check if ZIP has at least one entry
                    return Task.FromResult(zip.Entries.Count > 0);
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private string FindLatestBackup(string pluginId)
        {
            if (!Directory.Exists(_backupsPath))
                return null;

            var backupFiles = Directory.GetFiles(_backupsPath, $"{pluginId}-*.zip");
            if (backupFiles.Length == 0)
                return null;

            // Sort by creation time descending and return the most recent
            Array.Sort(backupFiles, (a, b) => File.GetCreationTimeUtc(b).CompareTo(File.GetCreationTimeUtc(a)));
            return backupFiles[0];
        }

        private async Task CleanOldBackupsAsync(string pluginId, int keepCount)
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(_backupsPath))
                    return;

                var backupFiles = Directory.GetFiles(_backupsPath, $"{pluginId}-*.zip");
                if (backupFiles.Length <= keepCount)
                    return;

                // Sort by creation time descending
                Array.Sort(backupFiles, (a, b) => File.GetCreationTimeUtc(b).CompareTo(File.GetCreationTimeUtc(a)));

                // Delete old backups
                for (int i = keepCount; i < backupFiles.Length; i++)
                {
                    try
                    {
                        File.Delete(backupFiles[i]);
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }
                }
            });
        }
    }
}
