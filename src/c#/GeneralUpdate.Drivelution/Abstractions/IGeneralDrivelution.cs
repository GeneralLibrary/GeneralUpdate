using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Abstractions;

/// <summary>
/// Core interface for driver updater
/// </summary>
public interface IGeneralDrivelution
{
    /// <summary>
    /// Updates driver asynchronously
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="strategy">Update strategy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update result</returns>
    Task<UpdateResult> UpdateAsync(DriverInfo driverInfo, UpdateStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates driver asynchronously
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<bool> ValidateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backs up driver asynchronously
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="backupPath">Backup path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup result</returns>
    Task<bool> BackupAsync(DriverInfo driverInfo, string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back driver asynchronously
    /// </summary>
    /// <param name="backupPath">Backup path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<bool> RollbackAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads driver information from local directory
    /// </summary>
    /// <param name="directoryPath">Directory path</param>
    /// <param name="searchPattern">Search pattern (optional, e.g., "*.inf", "*.ko")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of driver information</returns>
    Task<List<DriverInfo>> GetDriversFromDirectoryAsync(string directoryPath, string? searchPattern = null, CancellationToken cancellationToken = default);
}
