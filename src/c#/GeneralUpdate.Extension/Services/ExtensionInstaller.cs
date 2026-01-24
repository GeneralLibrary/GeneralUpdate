using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using GeneralUpdate.Differential;
using GeneralUpdate.Extension.Events;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Handles installation and rollback of extensions.
    /// </summary>
    public class ExtensionInstaller
    {
        private readonly string _installBasePath;
        private readonly string _backupBasePath;

        /// <summary>
        /// Event fired when installation completes.
        /// </summary>
        public event EventHandler<ExtensionInstallEventArgs>? InstallCompleted;

        /// <summary>
        /// Event fired when rollback completes.
        /// </summary>
        public event EventHandler<ExtensionRollbackEventArgs>? RollbackCompleted;

        /// <summary>
        /// Initializes a new instance of the ExtensionInstaller.
        /// </summary>
        /// <param name="installBasePath">Base path where extensions will be installed.</param>
        /// <param name="backupBasePath">Base path where backups will be stored.</param>
        public ExtensionInstaller(string installBasePath, string? backupBasePath = null)
        {
            _installBasePath = installBasePath ?? throw new ArgumentNullException(nameof(installBasePath));
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
        /// Installs an extension from a downloaded package.
        /// </summary>
        /// <param name="packagePath">Path to the downloaded package file.</param>
        /// <param name="extensionMetadata">Metadata of the extension being installed.</param>
        /// <param name="enableRollback">Whether to enable rollback on failure.</param>
        /// <returns>The installed LocalExtension, or null if installation failed.</returns>
        public async Task<LocalExtension?> InstallExtensionAsync(string packagePath, ExtensionMetadata extensionMetadata, bool enableRollback = true)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentNullException(nameof(packagePath));
            if (extensionMetadata == null)
                throw new ArgumentNullException(nameof(extensionMetadata));
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Package file not found", packagePath);

            var extensionInstallPath = Path.Combine(_installBasePath, extensionMetadata.Id);
            var backupPath = Path.Combine(_backupBasePath, $"{extensionMetadata.Id}_{DateTime.Now:yyyyMMddHHmmss}");
            bool needsRollback = false;

            try
            {
                // Create backup if extension already exists
                if (Directory.Exists(extensionInstallPath) && enableRollback)
                {
                    Directory.CreateDirectory(backupPath);
                    CopyDirectory(extensionInstallPath, backupPath);
                }

                // Extract the package
                if (!Directory.Exists(extensionInstallPath))
                {
                    Directory.CreateDirectory(extensionInstallPath);
                }

                ExtractPackage(packagePath, extensionInstallPath);

                // Create LocalExtension object
                var localExtension = new LocalExtension
                {
                    Metadata = extensionMetadata,
                    InstallPath = extensionInstallPath,
                    InstallDate = DateTime.Now,
                    AutoUpdateEnabled = true,
                    IsEnabled = true,
                    LastUpdateDate = DateTime.Now
                };

                // Save manifest
                var manifestPath = Path.Combine(extensionInstallPath, "manifest.json");
                var json = System.Text.Json.JsonSerializer.Serialize(localExtension, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, json);

                // Clean up backup if successful
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                OnInstallCompleted(extensionMetadata.Id, extensionMetadata.Name, true, extensionInstallPath, null);
                return localExtension;
            }
            catch (Exception ex)
            {
                needsRollback = enableRollback;
                OnInstallCompleted(extensionMetadata.Id, extensionMetadata.Name, false, extensionInstallPath, ex.Message);

                // Perform rollback if enabled
                if (needsRollback && Directory.Exists(backupPath))
                {
                    await RollbackAsync(extensionMetadata.Id, extensionMetadata.Name, backupPath, extensionInstallPath);
                }

                return null;
            }
        }

        /// <summary>
        /// Installs or updates an extension using differential patching.
        /// </summary>
        /// <param name="patchPath">Path to the patch files.</param>
        /// <param name="extensionMetadata">Metadata of the extension being updated.</param>
        /// <param name="enableRollback">Whether to enable rollback on failure.</param>
        /// <returns>The updated LocalExtension, or null if update failed.</returns>
        public async Task<LocalExtension?> ApplyPatchAsync(string patchPath, ExtensionMetadata extensionMetadata, bool enableRollback = true)
        {
            if (string.IsNullOrWhiteSpace(patchPath))
                throw new ArgumentNullException(nameof(patchPath));
            if (extensionMetadata == null)
                throw new ArgumentNullException(nameof(extensionMetadata));
            if (!Directory.Exists(patchPath))
                throw new DirectoryNotFoundException("Patch directory not found");

            var extensionInstallPath = Path.Combine(_installBasePath, extensionMetadata.Id);
            var backupPath = Path.Combine(_backupBasePath, $"{extensionMetadata.Id}_{DateTime.Now:yyyyMMddHHmmss}");
            bool needsRollback = false;

            try
            {
                // Create backup if rollback is enabled
                if (Directory.Exists(extensionInstallPath) && enableRollback)
                {
                    Directory.CreateDirectory(backupPath);
                    CopyDirectory(extensionInstallPath, backupPath);
                }

                // Apply patch using DifferentialCore.Dirty
                await DifferentialCore.Instance.Dirty(extensionInstallPath, patchPath);

                // Create or update LocalExtension object
                var localExtension = new LocalExtension
                {
                    Metadata = extensionMetadata,
                    InstallPath = extensionInstallPath,
                    InstallDate = DateTime.Now,
                    AutoUpdateEnabled = true,
                    IsEnabled = true,
                    LastUpdateDate = DateTime.Now
                };

                // Save manifest
                var manifestPath = Path.Combine(extensionInstallPath, "manifest.json");
                var json = System.Text.Json.JsonSerializer.Serialize(localExtension, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, json);

                // Clean up backup if successful
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                OnInstallCompleted(extensionMetadata.Id, extensionMetadata.Name, true, extensionInstallPath, null);
                return localExtension;
            }
            catch (Exception ex)
            {
                needsRollback = enableRollback;
                OnInstallCompleted(extensionMetadata.Id, extensionMetadata.Name, false, extensionInstallPath, ex.Message);

                // Perform rollback if enabled
                if (needsRollback && Directory.Exists(backupPath))
                {
                    await RollbackAsync(extensionMetadata.Id, extensionMetadata.Name, backupPath, extensionInstallPath);
                }

                return null;
            }
        }

        /// <summary>
        /// Performs a rollback by restoring from backup.
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
            }
        }

        /// <summary>
        /// Extracts a package to the specified directory.
        /// </summary>
        private void ExtractPackage(string packagePath, string destinationPath)
        {
            var extension = Path.GetExtension(packagePath).ToLowerInvariant();
            
            if (extension == ".zip")
            {
                // Delete existing directory if it exists to allow overwrite
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
        /// Recursively copies a directory.
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

        private void OnInstallCompleted(string extensionId, string extensionName, bool isSuccessful, string? installPath, string? errorMessage)
        {
            InstallCompleted?.Invoke(this, new ExtensionInstallEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = extensionName,
                IsSuccessful = isSuccessful,
                InstallPath = installPath,
                ErrorMessage = errorMessage
            });
        }

        private void OnRollbackCompleted(string extensionId, string extensionName, bool isSuccessful, string? errorMessage)
        {
            RollbackCompleted?.Invoke(this, new ExtensionRollbackEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = extensionName,
                IsSuccessful = isSuccessful,
                ErrorMessage = errorMessage
            });
        }
    }
}
