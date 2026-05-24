using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Storage management interface — backup, restore, and cleanup operations.
/// Instance-based for dependency injection with blacklist matcher support.
/// </summary>
public interface IStorageManager
{
    /// <summary>Backup source directory to destination.</summary>
    Task BackupAsync(string source, string dest, IReadOnlyList<string> skipDirectories, CancellationToken token = default);

    /// <summary>Restore from backup to installation path.</summary>
    Task RestoreAsync(string backupDir, string installPath, CancellationToken token = default);

    /// <summary>Clean old backups, keeping only the N most recent versions.</summary>
    Task CleanBackupAsync(string installPath, int keepVersions = 3, CancellationToken token = default);

    /// <summary>List all backups with metadata.</summary>
    IReadOnlyList<BackupInfo> ListBackups(string installPath);
}

/// <summary>
/// Default storage manager implementation wrapping the static StorageManager.
/// </summary>
public class DefaultStorageManager : IStorageManager
{
    private readonly IBlackListMatcher? _matcher;

    public DefaultStorageManager(IBlackListMatcher? matcher = null)
    {
        _matcher = matcher;
    }

    public Task BackupAsync(string source, string dest, IReadOnlyList<string> skipDirectories, CancellationToken token = default)
    {
        return StorageManager.BackupAsync(source, dest, skipDirectories);
    }

    public Task RestoreAsync(string backupDir, string installPath, CancellationToken token = default)
    {
        return StorageManager.RestoreAsync(backupDir, installPath);
    }

    public Task CleanBackupAsync(string installPath, int keepVersions = 3, CancellationToken token = default)
    {
        return StorageManager.CleanBackupAsync(installPath, keepVersions);
    }

    public IReadOnlyList<BackupInfo> ListBackups(string installPath)
    {
        return StorageManager.ListBackups(installPath);
    }
}
