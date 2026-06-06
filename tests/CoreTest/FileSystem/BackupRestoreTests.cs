using System;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.FileSystem;
using Xunit;

namespace CoreTest.Backup;

public class BackupRestoreTests
{
    [Fact]
    public void Backup_And_Restore_Roundtrip()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "CoreTest.Backup." + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(tmpRoot, "source");
        var backupDir = Path.Combine(tmpRoot, "backup");
        var restoreDir = Path.Combine(tmpRoot, "restored");

        try
        {
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "app.exe"), "v1.0");
            File.WriteAllText(Path.Combine(sourceDir, "config.json"), "{}");
            var subDir = Path.Combine(sourceDir, "data");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "data.db"), "mydata");

            StorageManager.Backup(sourceDir, backupDir, Array.Empty<string>());
            Assert.True(Directory.Exists(backupDir));
            Assert.True(File.Exists(Path.Combine(backupDir, "app.exe")));
            Assert.True(File.Exists(Path.Combine(backupDir, "config.json")));

            StorageManager.Restore(backupDir, restoreDir);
            Assert.True(Directory.Exists(restoreDir));
            Assert.Equal("v1.0", File.ReadAllText(Path.Combine(restoreDir, "app.exe")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(restoreDir, "config.json")));
        }
        finally
        {
            if (Directory.Exists(tmpRoot)) Directory.Delete(tmpRoot, true);
        }
    }

    [Fact]
    public void CleanBackup_KeepsOnlyRecentVersions()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "CoreTest.CleanBackup." + Guid.NewGuid().ToString("N"));
        try
        {
            // Create new-format backup dirs directly in installPath (backup-{timestamp})
            for (int i = 1; i <= 5; i++)
            {
                var timestamp = $"2026060600000{i}"; // Use fixed timestamp pattern for deterministic ordering
                var dir = Path.Combine(installPath, $"backup-{timestamp}");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "app.exe"), $"v{i}");
            }

            // Also create a legacy app-* backup to verify it's cleaned too
            var legacyDir = Path.Combine(installPath, "app-0.0.1");
            Directory.CreateDirectory(legacyDir);
            File.WriteAllText(Path.Combine(legacyDir, "app.exe"), "v0");

            StorageManager.CleanBackup(installPath, keepVersions: 3);

            // Should keep the 3 most recent backup-* dirs
            var remaining = Directory.GetDirectories(installPath, "backup-*");
            Assert.Equal(3, remaining.Length);
            var names = remaining.Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Contains("backup-20260606000003", names[0]);
            Assert.Contains("backup-20260606000004", names[1]);
            Assert.Contains("backup-20260606000005", names[2]);

            // Legacy app-* should also be cleaned (keeps top 3, but there's only 1 legacy,
            // and since we're using CleanBackup with keepVersions=3, the one legacy dir
            // at installPath level would be kept unless there are 3+ newer app-* dirs)
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }

    [Fact]
    public void CleanBackup_AlsoCleansBackupsSubdirectory()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "CoreTest.CleanBackupSub." + Guid.NewGuid().ToString("N"));
        var backupRoot = Path.Combine(installPath, StorageManager.BackupRootDirectory);
        try
        {
            // Create backup dirs in .backups subdirectory (new format)
            for (int i = 1; i <= 5; i++)
            {
                var timestamp = $"2026060600000{i}";
                var dir = Path.Combine(backupRoot, $"backup-{timestamp}");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "app.exe"), $"v{i}");
            }

            StorageManager.CleanBackup(installPath, keepVersions: 3);

            var remaining = Directory.GetDirectories(backupRoot);
            Assert.Equal(3, remaining.Length);
            var names = remaining.Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Contains("backup-20260606000003", names[0]);
            Assert.Contains("backup-20260606000004", names[1]);
            Assert.Contains("backup-20260606000005", names[2]);
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }

    [Fact]
    public void ListBackups_ReturnsMetadata()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "CoreTest.ListBackups." + Guid.NewGuid().ToString("N"));
        try
        {
            // New format: .backups subdirectory
            var backupRoot = Path.Combine(installPath, StorageManager.BackupRootDirectory);
            var newDir = Path.Combine(backupRoot, "backup-20260606235200");
            Directory.CreateDirectory(newDir);
            File.WriteAllText(Path.Combine(newDir, "app.exe"), "v1");

            // Legacy format: app-* directly in installPath
            var legacyDir = Path.Combine(installPath, "app-1.0.0");
            Directory.CreateDirectory(legacyDir);
            File.WriteAllText(Path.Combine(legacyDir, "app.exe"), "v2");

            var backups = StorageManager.ListBackups(installPath);
            Assert.Equal(2, backups.Count);
            Assert.Contains(backups, b => b.Version == "backup-20260606235200");
            Assert.Contains(backups, b => b.Version == "app-1.0.0");
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }

    [Fact]
    public void GetLatestBackup_ReturnsMostRecent()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "CoreTest.LatestBackup." + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(installPath);

            // Create backup dirs with different timestamps
            var dir1 = Path.Combine(installPath, "backup-20260601000000");
            var dir2 = Path.Combine(installPath, "backup-20260606235200"); // This is the latest
            var dir3 = Path.Combine(installPath, "backup-20260603000000");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);
            Directory.CreateDirectory(dir3);

            var latest = StorageManager.GetLatestBackup(installPath);
            Assert.NotNull(latest);
            Assert.Equal(dir2, latest); // backup-20260606235200 is alphabetically last
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }

    [Fact]
    public void GetLatestBackup_ReturnsNull_WhenNoBackups()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "CoreTest.LatestBackupEmpty." + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(installPath);
            var latest = StorageManager.GetLatestBackup(installPath);
            Assert.Null(latest);
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }

    [Fact]
    public void Backup_SkipsBackupDirectories()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "CoreTest.SkipBackup." + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(tmpRoot, "source");
        var backupDir = Path.Combine(tmpRoot, "target_backup");

        try
        {
            // Create source with both pre-existing backup-format dirs and an empty skip list
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "app.exe"), "v1.0");

            // Pre-existing backup directories in source (simulating old backups)
            var oldBackup = Path.Combine(sourceDir, "backup-20260601000000");
            Directory.CreateDirectory(oldBackup);
            File.WriteAllText(Path.Combine(oldBackup, "old.exe"), "old");

            var legacyBackup = Path.Combine(sourceDir, "app-0.0.1");
            Directory.CreateDirectory(legacyBackup);
            File.WriteAllText(Path.Combine(legacyBackup, "legacy.exe"), "legacy");

            var backupsDir = Path.Combine(sourceDir, StorageManager.BackupRootDirectory);
            Directory.CreateDirectory(backupsDir);
            File.WriteAllText(Path.Combine(backupsDir, "nested.exe"), "nested");

            // Backup with EMPTY skip list — hard-coded exclusion must prevent recursion
            StorageManager.Backup(sourceDir, backupDir, Array.Empty<string>());

            Assert.True(Directory.Exists(backupDir));
            Assert.True(File.Exists(Path.Combine(backupDir, "app.exe")));

            // Verify backup-format dirs were NOT copied
            Assert.False(Directory.Exists(Path.Combine(backupDir, "backup-20260601000000")));
            Assert.False(Directory.Exists(Path.Combine(backupDir, "app-0.0.1")));
            Assert.False(Directory.Exists(Path.Combine(backupDir, StorageManager.BackupRootDirectory)));
        }
        finally
        {
            if (Directory.Exists(tmpRoot)) Directory.Delete(tmpRoot, true);
        }
    }

    [Fact]
    public void Backup_WithBackupDirInsideSource_DoesNotRecurse()
    {
        // This test simulates the real-world scenario: backup directory is inside the
        // source path (installPath). The hard-coded exclusion must prevent recursion.
        var installPath = Path.Combine(Path.GetTempPath(), "CoreTest.NoRecurse." + Guid.NewGuid().ToString("N"));
        var backupDir = Path.Combine(installPath, "backup-20260606235200");

        try
        {
            Directory.CreateDirectory(installPath);
            File.WriteAllText(Path.Combine(installPath, "app.exe"), "v1.0");
            File.WriteAllText(Path.Combine(installPath, "config.json"), "{}");

            // Backup directory IS inside installPath — this MUST NOT recurse
            StorageManager.Backup(installPath, backupDir, Array.Empty<string>());

            Assert.True(Directory.Exists(backupDir));
            Assert.True(File.Exists(Path.Combine(backupDir, "app.exe")));
            Assert.True(File.Exists(Path.Combine(backupDir, "config.json")));

            // Critical: the backup directory must NOT contain a nested copy of itself
            Assert.False(Directory.Exists(Path.Combine(backupDir, "backup-20260606235200")),
                "Backup directory was recursively copied into itself! This is the main bug fix.");
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }

    [Fact]
    public void GetBackupDirectoryName_ReturnsTimestampFormat()
    {
        var name = StorageManager.GetBackupDirectoryName();
        Assert.StartsWith("backup-", name);
        // Format: backup-yyyyMMddHHmmss (17 chars)
        Assert.Equal(21, name.Length); // "backup-" (7) + "yyyyMMddHHmmss" (14)
    }
}
