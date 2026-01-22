using System;
using System.Threading.Tasks;

namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Provides services for delta/incremental updates.
    /// </summary>
    public interface IDeltaUpdateService
    {
        /// <summary>
        /// Generates a delta patch between two versions.
        /// </summary>
        /// <param name="baselineVersion">The baseline version.</param>
        /// <param name="targetVersion">The target version.</param>
        /// <returns>A task that represents the asynchronous operation, containing the delta patch information.</returns>
        Task<DeltaPatchInfo> GenerateDeltaPatchAsync(string baselineVersion, string targetVersion);

        /// <summary>
        /// Applies a delta patch to upgrade from baseline to target version.
        /// </summary>
        /// <param name="patchInfo">The delta patch information.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> ApplyDeltaPatchAsync(DeltaPatchInfo patchInfo);

        /// <summary>
        /// Validates a delta patch before applying it.
        /// </summary>
        /// <param name="patchInfo">The delta patch information to validate.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the patch is valid.</returns>
        Task<bool> ValidateDeltaPatchAsync(DeltaPatchInfo patchInfo);

        /// <summary>
        /// Calculates the optimal update path from current version to target version.
        /// </summary>
        /// <param name="currentVersion">The current version.</param>
        /// <param name="targetVersion">The target version.</param>
        /// <returns>A task that represents the asynchronous operation, containing the update path.</returns>
        Task<UpdatePath> CalculateOptimalUpdatePathAsync(string currentVersion, string targetVersion);
    }

    /// <summary>
    /// Represents a path for updating from one version to another.
    /// </summary>
    public class UpdatePath
    {
        /// <summary>
        /// Gets or sets the starting version.
        /// </summary>
        public string StartVersion { get; set; }

        /// <summary>
        /// Gets or sets the ending version.
        /// </summary>
        public string EndVersion { get; set; }

        /// <summary>
        /// Gets or sets the list of intermediate versions in the update path.
        /// </summary>
        public string[] IntermediateVersions { get; set; }

        /// <summary>
        /// Gets or sets the estimated total download size for the update path.
        /// </summary>
        public long EstimatedDownloadSize { get; set; }
    }
}
