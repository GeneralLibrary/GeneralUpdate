using System;
using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Generates the data packages required for incremental updates based on the <see cref="FileTreeDiff"/> difference result.
/// Used by the Pipeline's PatchMiddleware for constructing and applying differential patches.
/// </summary>
/// <remarks>
/// <para>
/// FileTreeDiffer converts file snapshot comparison results into actual incremental update operation instructions:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ProduceDeltaPaths"/>: Generates file path pairs that need to be packaged based on the diff result.</description></item>
///   <item><description><see cref="ProduceDeletes"/>: Extracts the list of files that need to be deleted on the client side.</description></item>
///   <item><description><see cref="ShouldUseDeltaPatching"/>: Decides whether to use incremental or full update based on the change ratio.</description></item>
/// </list>
/// <para>
/// The core principle of the incremental update strategy: use incremental patches when the change ratio is below the threshold,
/// and recommend a full package when the ratio exceeds the threshold, to avoid the performance overhead of numerous small patches.
/// </para>
/// </remarks>
public static class FileTreeDiffer
{
    /// <summary>
    /// Generates file path pairs that need to be packaged based on the difference result.
    /// </summary>
    /// <param name="diff">The difference result between the old and new snapshots.</param>
    /// <param name="updatedRoot">The root directory path of the new version files.</param>
    /// <returns>
    /// A read-only list of path pairs, each containing <c>(source file full path, relative path)</c>,
    /// where the source path points to the new version file and the relative path is used for locating it in the update package.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Generation logic:
    /// <list type="bullet">
    ///   <item><description>Added files: Included directly in the delta package.</description></item>
    ///   <item><description>Modified files: Included in the delta package.</description></item>
    ///   <item><description>Deleted files: Not handled here; extracted separately by <see cref="ProduceDeletes"/>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For each path pair, the method checks whether the source file actually exists on disk and skips it if not.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string SourcePath, string RelativePath)> ProduceDeltaPaths(
        FileTreeDiff diff, string updatedRoot)
    {
        var result = new List<(string, string)>();

        // Added files — can be bundled directly
        foreach (var entry in diff.Added)
        {
            var sourcePath = Path.Combine(updatedRoot, entry.RelativePath);
            if (File.Exists(sourcePath))
                result.Add((sourcePath, entry.RelativePath));
        }

        // Modified files — need patching
        foreach (var entry in diff.Modified)
        {
            var sourcePath = Path.Combine(updatedRoot, entry.RelativePath);
            if (File.Exists(sourcePath))
                result.Add((sourcePath, entry.RelativePath));
        }

        // Deleted files — skipped (handled by cleanup separately)

        return result.AsReadOnly();
    }

    /// <summary>
    /// Extracts the list of relative paths for files that need to be deleted from the difference result.
    /// </summary>
    /// <param name="diff">The difference result between the old and new snapshots.</param>
    /// <returns>A read-only list of relative paths for files that need to be deleted on the client side.</returns>
    /// <remarks>
    /// This method directly returns the <see cref="FileTreeDiff.Deleted"/> list, which contains
    /// the relative paths of all files that existed in the old version but have been removed in the new version.
    /// </remarks>
    public static IReadOnlyList<string> ProduceDeletes(FileTreeDiff diff)
        => diff.Deleted;

    /// <summary>
    /// Decides the optimal update mode based on the change ratio: recommends incremental (delta) update
    /// when the change ratio is low, and full update when the ratio is high.
    /// </summary>
    /// <param name="diff">The difference result between the old and new snapshots.</param>
    /// <param name="totalFileCount">The total number of files in the application.</param>
    /// <param name="thresholdPercent">The delta update threshold percentage. When the change file ratio is less than or equal to this value,
    /// incremental update is recommended. Default is 0.5 (50%).</param>
    /// <returns><c>true</c> if delta patching is recommended; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// Decision logic: change ratio = (added + modified + deleted) / total file count.
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Change ratio &lt;= thresholdPercent: Recommends incremental update. Fewer changes make patching more efficient.</description></item>
    ///   <item><description>Change ratio &gt; thresholdPercent: Recommends full update. The advantage of incremental patches diminishes with many changes.</description></item>
    /// </list>
    /// <para>
    /// Returns <c>false</c> when <paramref name="totalFileCount"/> is 0.
    /// </para>
    /// </remarks>
    public static bool ShouldUseDeltaPatching(FileTreeDiff diff, int totalFileCount, double thresholdPercent = 0.5)
    {
        if (totalFileCount == 0) return false;
        var changeRatio = (double)diff.TotalChanges / totalFileCount;
        return changeRatio <= thresholdPercent;
    }
}
