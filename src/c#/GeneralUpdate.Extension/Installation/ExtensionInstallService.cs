using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using GeneralUpdate.Differential;

namespace GeneralUpdate.Extension.Installation
{
    /// <summary>
    /// Handles installation, patching, and rollback of extension packages.
    /// Provides atomic operations with backup support for safe updates.
    /// </summary>
    public class ExtensionInstallService
    {
        private readonly string _installBasePath;
        private readonly string _backupBasePath;

        /// <summary>
        /// Occurs when an installation operation completes (success or failure).
        /// </summary>
        public event EventHandler<EventHandlers.InstallationCompletedEventArgs>? InstallationCompleted;

        /// <summary>
        /// Occurs when a rollback operation completes.
        /// </summary>
        public event EventHandler<EventHandlers.RollbackCompletedEventArgs>? RollbackCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionInstallService"/> class.
        /// </summary>
        /// <param name="installBasePath">Base directory where extensions are installed.</param>
        /// <param name="backupBasePath">Directory for storing installation backups (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="installBasePath"/> is null.</exception>
        public ExtensionInstallService(string installBasePath, string? backupBasePath = null)
        {
            if (string.IsNullOrWhiteSpace(installBasePath))
                throw new ArgumentNullException(nameof(installBasePath));

            _installBasePath = installBasePath;
            _backupBasePath = backupBasePath ?? Path.Combine(installBasePath, "_backups");

            if (!Directory.Exists(_installBasePath))
            {
                Directory.CreateDirectory(_installBasePath);
            }

            if (!Directory.Exists(_backupBasePath))
            {
                Directory.CreateDirectory(_backupBasePath);
            }
        }

        /// <summary>
        /// Installs an extension from a downloaded package file.
        /// Automatically creates backups and supports rollback on failure.
        /// </summary>
        /// <param name="packagePath">Path to the extension package file.</param>
        /// <param name="descriptor">Extension metadata descriptor.</param>
        /// <param name="enableRollback">Whether to enable automatic rollback on installation failure.</param>
        /// <returns>The installed extension object if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the package file doesn't exist.</exception>
        public async Task<InstalledExtension?> InstallAsync(string packagePath, Metadata.ExtensionDescriptor descriptor, bool enableRollback = true)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentNullException(nameof(packagePath));
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Package file not found", packagePath);

            var installPath = Path.Combine(_installBasePath, descriptor.ExtensionId);
            var backupPath = Path.Combine(_backupBasePath, $"{descriptor.ExtensionId}_{DateTime.Now:yyyyMMddHHmmss}");

            try
            {
                // Create backup if extension already exists
                if (Directory.Exists(installPath) && enableRollback)
                {
                    Directory.CreateDirectory(backupPath);
                    CopyDirectory(installPath, backupPath);
                }

                // Extract the package
                if (!Directory.Exists(installPath))
                {
                    Directory.CreateDirectory(installPath);
                }

                ExtractPackage(packagePath, installPath);

                // Create the installed extension object
                var installed = new InstalledExtension
                {
                    Descriptor = descriptor,
                    InstallPath = installPath,
                    InstallDate = DateTime.Now,
                    AutoUpdateEnabled = true,
                    IsEnabled = true,
                    LastUpdateDate = DateTime.Now
                };

                // Persist the manifest
                SaveManifest(installed);

                // Clean up backup on success
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                OnInstallationCompleted(descriptor.ExtensionId, descriptor.DisplayName, true, installPath, null);
                return installed;
            }
            catch (Exception ex)
            {
                OnInstallationCompleted(descriptor.ExtensionId, descriptor.DisplayName, false, installPath, ex.Message);

                // Attempt rollback if enabled
                if (enableRollback && Directory.Exists(backupPath))
                {
                    await RollbackAsync(descriptor.ExtensionId, descriptor.DisplayName, backupPath, installPath);
                }

                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Installation failed for extension {descriptor.ExtensionId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Applies a differential patch to an existing extension.
        /// Useful for incremental updates that don't require full package downloads.
        /// </summary>
        /// <param name="patchPath">Path to the directory containing patch files.</param>
        /// <param name="descriptor">Extension metadata descriptor for the target version.</param>
        /// <param name="enableRollback">Whether to enable automatic rollback on patch failure.</param>
        /// <returns>The updated extension object if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the patch directory doesn't exist.</exception>
        public async Task<InstalledExtension?> ApplyPatchAsync(string patchPath, Metadata.ExtensionDescriptor descriptor, bool enableRollback = true)
        {
            if (string.IsNullOrWhiteSpace(patchPath))
                throw new ArgumentNullException(nameof(patchPath));
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (!Directory.Exists(patchPath))
                throw new DirectoryNotFoundException("Patch directory not found");

            var installPath = Path.Combine(_installBasePath, descriptor.ExtensionId);
            var backupPath = Path.Combine(_backupBasePath, $"{descriptor.ExtensionId}_{DateTime.Now:yyyyMMddHHmmss}");

            try
            {
                // Create backup if rollback is enabled
                if (Directory.Exists(installPath) && enableRollback)
                {
                    Directory.CreateDirectory(backupPath);
                    CopyDirectory(installPath, backupPath);
                }

                // Apply the differential patch
                await DifferentialCore.Instance.Dirty(installPath, patchPath);

                // Load existing metadata to preserve installation history
                InstalledExtension? existing = null;
                var manifestPath = Path.Combine(installPath, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        existing = System.Text.Json.JsonSerializer.Deserialize<InstalledExtension>(json);
                    }
                    catch
                    {
                        // If manifest is corrupt, proceed with new metadata
                    }
                }

                // Create updated extension object
                var updated = new InstalledExtension
                {
                    Descriptor = descriptor,
                    InstallPath = installPath,
                    InstallDate = existing?.InstallDate ?? DateTime.Now,
                    AutoUpdateEnabled = existing?.AutoUpdateEnabled ?? true,
                    IsEnabled = existing?.IsEnabled ?? true,
                    LastUpdateDate = DateTime.Now
                };

                // Persist the updated manifest
                SaveManifest(updated);

                // Clean up backup on success
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                OnInstallationCompleted(descriptor.ExtensionId, descriptor.DisplayName, true, installPath, null);
                return updated;
            }
            catch (Exception ex)
            {
                OnInstallationCompleted(descriptor.ExtensionId, descriptor.DisplayName, false, installPath, ex.Message);

                // Attempt rollback if enabled
                if (enableRollback && Directory.Exists(backupPath))
                {
                    await RollbackAsync(descriptor.ExtensionId, descriptor.DisplayName, backupPath, installPath);
                }

                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Patch application failed for extension {descriptor.ExtensionId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Performs a rollback by restoring an extension from its backup.
        /// Removes the failed installation and restores the previous state.
        /// </summary>
        private async Task RollbackAsync(string extensionId, string extensionName, string backupPath, string installPath)
        {
            try
            {
                // Remove the failed installation
                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, true);
                }

                // Restore from backup
                await Task.Run(() => CopyDirectory(backupPath, installPath));

                // Clean up backup
                Directory.Delete(backupPath, true);

                OnRollbackCompleted(extensionId, extensionName, true, null);
            }
            catch (Exception ex)
            {
                OnRollbackCompleted(extensionId, extensionName, false, ex.Message);
                GeneralUpdate.Common.Shared.GeneralTracer.Error($"Rollback failed for extension {extensionId}", ex);
            }
        }

        /// <summary>
        /// Extracts a compressed package to the target directory.
        /// Currently supports ZIP format packages.
        /// </summary>
        private void ExtractPackage(string packagePath, string destinationPath)
        {
            var extension = Path.GetExtension(packagePath).ToLowerInvariant();

            if (extension == ".zip")
            {
                // Clear existing files to allow clean extraction
                if (Directory.Exists(destinationPath) && Directory.GetFiles(destinationPath).Length > 0)
                {
                    Directory.Delete(destinationPath, true);
                    Directory.CreateDirectory(destinationPath);
                }

                ZipFile.ExtractToDirectory(packagePath, destinationPath);
            }
            else
            {
                throw new NotSupportedException($"Package format {extension} is not supported");
            }
        }

        /// <summary>
        /// Recursively copies all files and subdirectories from source to destination.
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        /// <summary>
        /// Persists the extension manifest to disk in JSON format.
        /// </summary>
        private void SaveManifest(InstalledExtension extension)
        {
            var manifestPath = Path.Combine(extension.InstallPath, "manifest.json");
            var json = System.Text.Json.JsonSerializer.Serialize(extension, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);
        }

        /// <summary>
        /// Raises the InstallationCompleted event.
        /// </summary>
        private void OnInstallationCompleted(string extensionId, string extensionName, bool success, string? installPath, string? errorMessage)
        {
            InstallationCompleted?.Invoke(this, new EventHandlers.InstallationCompletedEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = extensionName,
                Success = success,
                InstallPath = installPath,
                ErrorMessage = errorMessage
            });
        }

        /// <summary>
        /// Raises the RollbackCompleted event.
        /// </summary>
        private void OnRollbackCompleted(string extensionId, string extensionName, bool success, string? errorMessage)
        {
            RollbackCompleted?.Invoke(this, new EventHandlers.RollbackCompletedEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = extensionName,
                Success = success,
                ErrorMessage = errorMessage
            });
        }
    }
}
