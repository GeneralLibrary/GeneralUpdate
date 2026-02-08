namespace GeneralUpdate.Drivelution.Abstractions;

/// <summary>
/// 驱动备份接口
/// Driver backup interface
/// </summary>
public interface IDriverBackup
{
    /// <summary>
    /// 异步备份驱动
    /// Backs up driver asynchronously
    /// </summary>
    /// <param name="sourcePath">源路径 / Source path</param>
    /// <param name="backupPath">备份路径 / Backup path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>备份结果 / Backup result</returns>
    Task<bool> BackupAsync(string sourcePath, string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步恢复驱动
    /// Restores driver asynchronously
    /// </summary>
    /// <param name="backupPath">备份路径 / Backup path</param>
    /// <param name="targetPath">目标路径 / Target path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>恢复结果 / Restore result</returns>
    Task<bool> RestoreAsync(string backupPath, string targetPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除备份
    /// Deletes backup asynchronously
    /// </summary>
    /// <param name="backupPath">备份路径 / Backup path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>删除结果 / Delete result</returns>
    Task<bool> DeleteBackupAsync(string backupPath, CancellationToken cancellationToken = default);
}
