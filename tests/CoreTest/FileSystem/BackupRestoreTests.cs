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
        var backupRoot = Path.Combine(installPath, "__backups");
        try
        {
            for (int i = 1; i <= 5; i++)
            {
                var verDir = Path.Combine(backupRoot, $"{i}.0.0");
                Directory.CreateDirectory(verDir);
                File.WriteAllText(Path.Combine(verDir, "app.exe"), $"v{i}");
            }

            StorageManager.CleanBackup(installPath, keepVersions: 3);

            var remaining = Directory.GetDirectories(backupRoot);
            Assert.Equal(3, remaining.Length);
            var names = remaining.Select(Path.GetFileName).OrderBy(n => new Version(n!)).ToList();
            Assert.Equal("3.0.0", names[0]);
            Assert.Equal("4.0.0", names[1]);
            Assert.Equal("5.0.0", names[2]);
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
        var backupRoot = Path.Combine(installPath, "__backups");
        try
        {
            var verDir = Path.Combine(backupRoot, "1.0.0");
            Directory.CreateDirectory(verDir);
            File.WriteAllText(Path.Combine(verDir, "app.exe"), "v1");

            var backups = StorageManager.ListBackups(installPath);
            Assert.Single(backups);
            Assert.Equal("1.0.0", backups[0].Version);
            Assert.Contains("__backups", backups[0].Path);
        }
        finally
        {
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
        }
    }
}
