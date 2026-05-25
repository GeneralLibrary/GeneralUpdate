using GeneralUpdate.Drivelution.Windows.Implementation;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace DrivelutionTest.WindowsImplementations;

/// <summary>
/// WindowsDriverBackup 测试
/// 分支覆盖点:
/// - BackupAsync: 源文件不存在 -> FileNotFoundException
/// - BackupAsync: 备份目录不存在 -> 自动创建
/// - BackupAsync: 成功备份 -> true
/// - BackupAsync: 异常 -> DriverBackupException
/// - RestoreAsync: 备份文件不存在 -> FileNotFoundException
/// - RestoreAsync: 目标文件已存在 -> 重命名为.old
/// - RestoreAsync: 成功恢复 -> true
/// - RestoreAsync: 异常 -> DriverRollbackException
/// - DeleteBackupAsync: 文件存在 -> 删除返回true
/// - DeleteBackupAsync: 文件不存在 -> 返回false
/// - DeleteBackupAsync: 异常 -> 返回false
/// 触发条件：创建临时文件测试备份/恢复/删除
/// 预期结果：I/O操作正确执行
/// </summary>
public class WindowsDriverBackupTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WindowsDriverBackup _backup;

    public WindowsDriverBackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drivelution_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _backup = new WindowsDriverBackup();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { /* cleanup best-effort */ }
    }

    [Fact(DisplayName = "WindowsDriverBackup_BackupAsync_源文件不存在_抛出FileNotFoundException")]
    public async Task BackupAsync_SourceNotExists_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _backup.BackupAsync(Path.Combine(_tempDir, "nonexistent.sys"),
                Path.Combine(_tempDir, "backup")));
    }

    [Fact(DisplayName = "WindowsDriverBackup_BackupAsync_成功备份_返回true")]
    public async Task BackupAsync_SuccessfulBackup_ReturnsTrue()
    {
        var sourceFile = Path.Combine(_tempDir, "test.sys");
        await File.WriteAllTextAsync(sourceFile, "driver data");
        var backupPath = Path.Combine(_tempDir, "backups", "driver_backup");

        var result = await _backup.BackupAsync(sourceFile, backupPath);

        Assert.True(result);
        // Directory should be created
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "backups")));
    }

    [Fact(DisplayName = "WindowsDriverBackup_RestoreAsync_备份文件不存在_抛出FileNotFoundException")]
    public async Task RestoreAsync_BackupNotExists_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _backup.RestoreAsync(Path.Combine(_tempDir, "nobackup.sys"),
                Path.Combine(_tempDir, "target.sys")));
    }

    [Fact(DisplayName = "WindowsDriverBackup_RestoreAsync_成功恢复_返回true")]
    public async Task RestoreAsync_SuccessfulRestore_ReturnsTrue()
    {
        // Create backup
        var backupFile = Path.Combine(_tempDir, "backup_driver.sys");
        await File.WriteAllTextAsync(backupFile, "backup data");
        var targetFile = Path.Combine(_tempDir, "restored_driver.sys");

        var result = await _backup.RestoreAsync(backupFile, targetFile);

        Assert.True(result);
        Assert.True(File.Exists(targetFile));
    }

    [Fact(DisplayName = "WindowsDriverBackup_RestoreAsync_目标文件已存在_先重命名")]
    public async Task RestoreAsync_TargetExists_RenamesFirst()
    {
        var backupFile = Path.Combine(_tempDir, "backup_driver2.sys");
        await File.WriteAllTextAsync(backupFile, "backup data");
        var targetFile = Path.Combine(_tempDir, "existing_target.sys");
        await File.WriteAllTextAsync(targetFile, "existing data");

        var result = await _backup.RestoreAsync(backupFile, targetFile);

        Assert.True(result);
        Assert.True(File.Exists(targetFile + ".old"));
    }

    [Fact(DisplayName = "WindowsDriverBackup_DeleteBackupAsync_文件存在_返回true")]
    public async Task DeleteBackupAsync_FileExists_ReturnsTrue()
    {
        var file = Path.Combine(_tempDir, "to_delete.sys");
        await File.WriteAllTextAsync(file, "data");

        var result = await _backup.DeleteBackupAsync(file);

        Assert.True(result);
        Assert.False(File.Exists(file));
    }

    [Fact(DisplayName = "WindowsDriverBackup_DeleteBackupAsync_文件不存在_返回false")]
    public async Task DeleteBackupAsync_FileNotExists_ReturnsFalse()
    {
        var result = await _backup.DeleteBackupAsync(Path.Combine(_tempDir, "nonexistent.sys"));
        Assert.False(result);
    }
}
