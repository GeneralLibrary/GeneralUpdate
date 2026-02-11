using System.Runtime.Versioning;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace GeneralUpdate.Drivelution.Windows.Implementation;

/// <summary>
/// Windows驱动备份实现
/// Windows driver backup implementation
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsDriverBackup : IDriverBackup
{
    public WindowsDriverBackup()
    {
    }

    /// <inheritdoc/>
    public async Task<bool> BackupAsync(
        string sourcePath,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Backing up driver from {sourcePath} to {backupPath}");

        try
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
            }

            // Ensure backup directory exists
            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
                GeneralTracer.Info($"Created backup directory: {backupDir}");
            }

            // Add timestamp to backup filename to avoid conflicts
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = Path.GetFileNameWithoutExtension(backupPath);
            var extension = Path.GetExtension(backupPath);
            var backupPathWithTimestamp = Path.Combine(
                backupDir ?? string.Empty,
                $"{fileName}_{timestamp}{extension}");

            // Copy file asynchronously
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var destinationStream = new FileStream(backupPathWithTimestamp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            GeneralTracer.Info($"Driver backup completed successfully: {backupPathWithTimestamp}");
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Failed to backup driver", ex);
            throw new DriverBackupException($"Failed to backup driver: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RestoreAsync(
        string backupPath,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Restoring driver from {backupPath} to {targetPath}");

        try
        {
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException($"Backup file not found: {backupPath}");
            }

            // Ensure target directory exists
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                GeneralTracer.Info($"Created target directory: {targetDir}");
            }

            // Backup existing target file if it exists
            if (File.Exists(targetPath))
            {
                var tempBackup = $"{targetPath}.old";
                File.Move(targetPath, tempBackup, true);
                GeneralTracer.Info($"Moved existing file to temporary backup: {tempBackup}");
            }

            // Copy backup file to target location
            using (var sourceStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var destinationStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            GeneralTracer.Info("Driver restore completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Failed to restore driver", ex);
            throw new DriverRollbackException($"Failed to restore driver: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Deleting backup: {backupPath}");

        return await Task.Run(() =>
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    GeneralTracer.Info("Backup deleted successfully");
                    return true;
                }
                else
                {
                    GeneralTracer.Warn($"Backup file not found: {backupPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("Failed to delete backup", ex);
                return false;
            }
        }, cancellationToken);
    }
}
