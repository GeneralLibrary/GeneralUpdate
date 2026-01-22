using System;
using System.Threading.Tasks;

namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Provides core update services for extensions.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Checks for available updates for a specific extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing the update metadata.</returns>
        Task<UpdateMetadata> CheckForUpdatesAsync(string extensionId);

        /// <summary>
        /// Downloads an update package.
        /// </summary>
        /// <param name="packageInfo">The package information to download.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>A task that represents the asynchronous operation, containing the path to the downloaded package.</returns>
        Task<string> DownloadUpdateAsync(UpdatePackageInfo packageInfo, IProgress<double> progress = null);

        /// <summary>
        /// Installs an update from a downloaded package.
        /// </summary>
        /// <param name="packagePath">The path to the update package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> InstallUpdateAsync(string packagePath);

        /// <summary>
        /// Rolls back to a previous version.
        /// </summary>
        /// <param name="rollbackInfo">The rollback information.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> RollbackAsync(RollbackInfo rollbackInfo);

        /// <summary>
        /// Verifies the integrity of an update package.
        /// </summary>
        /// <param name="packagePath">The path to the package.</param>
        /// <param name="expectedHash">The expected hash value.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the package is valid.</returns>
        Task<bool> VerifyPackageIntegrityAsync(string packagePath, string expectedHash);
    }
}
