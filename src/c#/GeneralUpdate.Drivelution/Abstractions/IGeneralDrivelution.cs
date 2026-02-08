using System.Diagnostics.CodeAnalysis;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Abstractions;

/// <summary>
/// 驱动更新器核心接口
/// Core interface for driver updater
/// </summary>
public interface IGeneralDrivelution
{
    /// <summary>
    /// 异步更新驱动
    /// Updates driver asynchronously
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="strategy">更新策略 / Update strategy</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>更新结果 / Update result</returns>
    /// <remarks>
    /// Note: Update process may include signature validation that requires reflection on some platforms.
    /// </remarks>
    [RequiresUnreferencedCode("Update process may include signature validation that requires runtime reflection on some platforms")]
    [RequiresDynamicCode("Update process may include signature validation that requires runtime code generation on some platforms")]
    Task<UpdateResult> UpdateAsync(DriverInfo driverInfo, UpdateStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步验证驱动
    /// Validates driver asynchronously
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>验证是否通过 / Validation result</returns>
    /// <remarks>
    /// Note: Includes signature validation that may require reflection on some platforms.
    /// </remarks>
    [RequiresUnreferencedCode("Validation includes signature validation that may require runtime reflection on some platforms")]
    [RequiresDynamicCode("Validation includes signature validation that may require runtime code generation on some platforms")]
    Task<bool> ValidateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步备份驱动
    /// Backs up driver asynchronously
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="backupPath">备份路径 / Backup path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>备份结果 / Backup result</returns>
    Task<bool> BackupAsync(DriverInfo driverInfo, string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步回滚驱动
    /// Rolls back driver asynchronously
    /// </summary>
    /// <param name="backupPath">备份路径 / Backup path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>回滚结果 / Rollback result</returns>
    Task<bool> RollbackAsync(string backupPath, CancellationToken cancellationToken = default);
}
