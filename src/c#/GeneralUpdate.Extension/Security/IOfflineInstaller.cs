using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.Extensions.Security
{
    /// <summary>
    /// Provides methods for offline plugin installation.
    /// </summary>
    public interface IOfflineInstaller
    {
        /// <summary>
        /// Installs an extension from an offline package.
        /// </summary>
        /// <param name="packagePath">The path to the offline package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> InstallFromOfflinePackageAsync(string packagePath);

        /// <summary>
        /// Creates an offline installation package for an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="version">The version to package.</param>
        /// <param name="outputPath">The path where the offline package should be created.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> CreateOfflinePackageAsync(string extensionId, string version, string outputPath);

        /// <summary>
        /// Validates an offline installation package.
        /// </summary>
        /// <param name="packagePath">The path to the offline package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the package is valid.</returns>
        Task<bool> ValidateOfflinePackageAsync(string packagePath);

        /// <summary>
        /// Imports an offline package index.
        /// </summary>
        /// <param name="indexPath">The path to the index file.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> ImportOfflineIndexAsync(string indexPath);

        /// <summary>
        /// Exports the current offline package index.
        /// </summary>
        /// <param name="outputPath">The path where the index file should be saved.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> ExportOfflineIndexAsync(string outputPath);

        /// <summary>
        /// Gets all extensions available in the offline index.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation, containing a list of available extensions.</returns>
        Task<List<OfflinePackageInfo>> GetAvailableOfflinePackagesAsync();
    }

    /// <summary>
    /// Represents information about an offline package.
    /// </summary>
    public class OfflinePackageInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the extension.
        /// </summary>
        public string ExtensionId { get; set; }

        /// <summary>
        /// Gets or sets the version of the extension.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the path to the offline package.
        /// </summary>
        public string PackagePath { get; set; }

        /// <summary>
        /// Gets or sets the size of the package in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the package creation timestamp.
        /// </summary>
        public DateTime CreatedDate { get; set; }
    }
}
