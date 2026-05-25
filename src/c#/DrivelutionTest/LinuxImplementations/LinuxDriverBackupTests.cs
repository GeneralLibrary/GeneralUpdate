using GeneralUpdate.Drivelution.Linux.Implementation;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace DrivelutionTest.LinuxImplementations;

public class LinuxDriverBackupTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LinuxDriverBackup _backup;

    public LinuxDriverBackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"linux_drv_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _backup = new LinuxDriverBackup();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    [Fact(DisplayName = "LinuxDriverBackup_BackupAsync_源文件不存在_抛出FileNotFoundException")]
    public async Task BackupAsync_SourceNotExists_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _backup.BackupAsync(Path.Combine(_tempDir, "nonexistent.ko"), Path.Combine(_tempDir, "backup")));
    }

    [Fact(DisplayName = "LinuxDriverBackup_BackupAsync_成功备份_返回true")]
    public async Task BackupAsync_SuccessfulBackup_ReturnsTrue()
    {
        var sourceFile = Path.Combine(_tempDir, "test.ko");
        await File.WriteAllTextAsync(sourceFile, "module data");
        var result = await _backup.BackupAsync(sourceFile, Path.Combine(_tempDir, "backups", "driver_backup"));
        Assert.True(result);
    }

    [Fact(DisplayName = "LinuxDriverBackup_RestoreAsync_备份文件不存在_抛出FileNotFoundException")]
    public async Task RestoreAsync_BackupNotExists_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _backup.RestoreAsync(Path.Combine(_tempDir, "nobackup.ko"), Path.Combine(_tempDir, "target.ko")));
    }

    [Fact(DisplayName = "LinuxDriverBackup_RestoreAsync_成功恢复_返回true")]
    public async Task RestoreAsync_SuccessfulRestore_ReturnsTrue()
    {
        var backupFile = Path.Combine(_tempDir, "backup_module.ko");
        await File.WriteAllTextAsync(backupFile, "backup data");
        var targetFile = Path.Combine(_tempDir, "restored_module.ko");
        var result = await _backup.RestoreAsync(backupFile, targetFile);
        Assert.True(result);
    }

    [Fact(DisplayName = "LinuxDriverBackup_DeleteBackupAsync_文件存在_返回true")]
    public async Task DeleteBackupAsync_FileExists_ReturnsTrue()
    {
        var file = Path.Combine(_tempDir, "to_delete.ko");
        await File.WriteAllTextAsync(file, "data");
        var result = await _backup.DeleteBackupAsync(file);
        Assert.True(result);
    }

    [Fact(DisplayName = "LinuxDriverBackup_DeleteBackupAsync_文件不存在_返回false")]
    public async Task DeleteBackupAsync_FileNotExists_ReturnsFalse()
    {
        var result = await _backup.DeleteBackupAsync(Path.Combine(_tempDir, "nonexistent.ko"));
        Assert.False(result);
    }
}
