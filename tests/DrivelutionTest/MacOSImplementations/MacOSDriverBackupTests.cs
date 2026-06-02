using GeneralUpdate.Drivelution.MacOS.Implementation;

namespace DrivelutionTest.MacOSImplementations;

public class MacOSDriverBackupTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MacOSDriverBackup _backup;

    public MacOSDriverBackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"macos_drv_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _backup = new MacOSDriverBackup();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    [Fact(DisplayName = "MacOSDriverBackup_BackupAsync_源不存在_返回false")]
    public async Task BackupAsync_SourceNotExists_ReturnsFalse()
    {
        var result = await _backup.BackupAsync(Path.Combine(_tempDir, "nonexistent.kext"), Path.Combine(_tempDir, "backup.kext"));
        Assert.False(result);
    }

    [Fact(DisplayName = "MacOSDriverBackup_BackupAsync_文件复制_返回true")]
    public async Task BackupAsync_FileCopy_ReturnsTrue()
    {
        var sourceFile = Path.Combine(_tempDir, "test.kext");
        await File.WriteAllTextAsync(sourceFile, "kext data");
        var result = await _backup.BackupAsync(sourceFile, Path.Combine(_tempDir, "backups", "driver_backup.kext"));
        Assert.True(result);
    }

    [Fact(DisplayName = "MacOSDriverBackup_BackupAsync_目录复制_返回true")]
    public async Task BackupAsync_DirectoryCopy_ReturnsTrue()
    {
        var sourceDir = Path.Combine(_tempDir, "MyKext.kext");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Info.plist"), "<plist>");
        var result = await _backup.BackupAsync(sourceDir, Path.Combine(_tempDir, "backups", "MyKext.kext"));
        Assert.True(result);
    }

    [Fact(DisplayName = "MacOSDriverBackup_RestoreAsync_委托给BackupAsync")]
    public async Task RestoreAsync_DelegatesToBackup()
    {
        var sourceFile = Path.Combine(_tempDir, "restore_test.kext");
        await File.WriteAllTextAsync(sourceFile, "data");
        var result = await _backup.RestoreAsync(sourceFile, Path.Combine(_tempDir, "restored.kext"));
        Assert.True(result);
    }

    [Fact(DisplayName = "MacOSDriverBackup_DeleteBackupAsync_文件存在_返回true")]
    public async Task DeleteBackupAsync_FileExists_ReturnsTrue()
    {
        var file = Path.Combine(_tempDir, "delete_me.kext");
        await File.WriteAllTextAsync(file, "data");
        var result = await _backup.DeleteBackupAsync(file);
        Assert.True(result);
    }

    [Fact(DisplayName = "MacOSDriverBackup_DeleteBackupAsync_目录存在_递归删除返回true")]
    public async Task DeleteBackupAsync_DirectoryExists_ReturnsTrue()
    {
        var dir = Path.Combine(_tempDir, "delete_dir.kext");
        Directory.CreateDirectory(dir);
        var result = await _backup.DeleteBackupAsync(dir);
        Assert.True(result);
    }
}
