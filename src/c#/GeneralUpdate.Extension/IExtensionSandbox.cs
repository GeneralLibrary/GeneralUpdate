using System;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides permission and resource isolation for extensions.
    /// </summary>
    public interface IExtensionSandbox
    {
        /// <summary>
        /// Checks whether an extension has a specific permission.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="permission">The permission to check.</param>
        /// <returns>True if the extension has the permission; otherwise, false.</returns>
        bool HasPermission(string extensionId, ExtensionPermission permission);

        /// <summary>
        /// Requests a permission for an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="permission">The permission to request.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the permission was granted.</returns>
        Task<bool> RequestPermissionAsync(string extensionId, ExtensionPermission permission);

        /// <summary>
        /// Revokes a permission from an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="permission">The permission to revoke.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> RevokePermissionAsync(string extensionId, ExtensionPermission permission);

        /// <summary>
        /// Checks whether an extension can access a file or directory.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="path">The file or directory path.</param>
        /// <param name="accessType">The type of access (e.g., "Read", "Write").</param>
        /// <returns>True if access is allowed; otherwise, false.</returns>
        bool CanAccessFile(string extensionId, string path, string accessType);

        /// <summary>
        /// Checks whether an extension can access a network resource.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="url">The URL to access.</param>
        /// <returns>True if access is allowed; otherwise, false.</returns>
        bool CanAccessNetwork(string extensionId, string url);

        /// <summary>
        /// Checks whether an extension can access a system resource.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="resourceType">The type of system resource (e.g., "Registry", "Process").</param>
        /// <returns>True if access is allowed; otherwise, false.</returns>
        bool CanAccessSystem(string extensionId, string resourceType);

        /// <summary>
        /// Sets resource limits for an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="limits">The resource limits to apply.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> SetResourceLimitsAsync(string extensionId, ResourceLimits limits);
    }

    /// <summary>
    /// Represents resource limits for an extension.
    /// </summary>
    public class ResourceLimits
    {
        /// <summary>
        /// Gets or sets the maximum memory usage in MB.
        /// </summary>
        public int MaxMemoryMB { get; set; }

        /// <summary>
        /// Gets or sets the maximum CPU usage percentage.
        /// </summary>
        public double MaxCpuPercent { get; set; }

        /// <summary>
        /// Gets or sets the maximum disk space usage in MB.
        /// </summary>
        public long MaxDiskSpaceMB { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of network connections.
        /// </summary>
        public int MaxNetworkConnections { get; set; }
    }
}
