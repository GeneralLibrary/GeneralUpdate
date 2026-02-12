using System.Runtime.Versioning;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace GeneralUpdate.Drivelution.Linux.Implementation;

/// <summary>
/// Linux驱动备份实现
/// Linux driver backup implementation
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxDriverBackup : IDriverBackup
{
    public LinuxDriverBackup()
    {
    }

    /// <inheritdoc/>
    public async Task<bool> BackupAsync(
        string sourcePath,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
            }

            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = Path.GetFileNameWithoutExtension(backupPath);
            var extension = Path.GetExtension(backupPath);
            var backupPathWithTimestamp = Path.Combine(
                backupDir ?? string.Empty,
                $"{fileName}_{timestamp}{extension}");

            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var destinationStream = new FileStream(backupPathWithTimestamp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            GeneralTracer.Info($"Backup completed: {backupPathWithTimestamp}");
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
        try
        {
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException($"Backup file not found: {backupPath}");
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(targetPath))
            {
                File.Move(targetPath, $"{targetPath}.old", true);
            }

            using (var sourceStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var destinationStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            GeneralTracer.Info("Restore completed");
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
        return await Task.Run(() =>
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("Failed to delete backup", ex);
                return false;
            }
        }, cancellationToken);
    }
}
