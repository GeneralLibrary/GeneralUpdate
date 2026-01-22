using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.Extensions.Security
{
    /// <summary>
    /// Provides methods for managing enterprise repository mirrors.
    /// </summary>
    public interface IRepositoryMirror
    {
        /// <summary>
        /// Registers a new repository mirror.
        /// </summary>
        /// <param name="mirrorUrl">The URL of the mirror repository.</param>
        /// <param name="priority">The priority of the mirror (higher values are preferred).</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> RegisterMirrorAsync(string mirrorUrl, int priority);

        /// <summary>
        /// Removes a repository mirror.
        /// </summary>
        /// <param name="mirrorUrl">The URL of the mirror repository to remove.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> RemoveMirrorAsync(string mirrorUrl);

        /// <summary>
        /// Gets all registered repository mirrors.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation, containing a list of mirror URLs.</returns>
        Task<List<RepositoryMirrorInfo>> GetMirrorsAsync();

        /// <summary>
        /// Synchronizes a mirror with the primary repository.
        /// </summary>
        /// <param name="mirrorUrl">The URL of the mirror repository to synchronize.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> SyncMirrorAsync(string mirrorUrl);

        /// <summary>
        /// Tests the connectivity and health of a repository mirror.
        /// </summary>
        /// <param name="mirrorUrl">The URL of the mirror repository to test.</param>
        /// <returns>A task that represents the asynchronous operation, containing the health status.</returns>
        Task<MirrorHealthStatus> TestMirrorHealthAsync(string mirrorUrl);

        /// <summary>
        /// Sets the primary repository mirror to use.
        /// </summary>
        /// <param name="mirrorUrl">The URL of the mirror repository to set as primary.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> SetPrimaryMirrorAsync(string mirrorUrl);
    }

    /// <summary>
    /// Represents information about a repository mirror.
    /// </summary>
    public class RepositoryMirrorInfo
    {
        /// <summary>
        /// Gets or sets the URL of the mirror.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the priority of the mirror.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mirror is currently active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the last synchronization timestamp.
        /// </summary>
        public DateTime LastSync { get; set; }
    }

    /// <summary>
    /// Represents the health status of a repository mirror.
    /// </summary>
    public class MirrorHealthStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether the mirror is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        public int ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets any error messages.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
