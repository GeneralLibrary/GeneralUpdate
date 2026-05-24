using System;
using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Applies a <see cref="FileTreeDiff"/> to produce delta file bundles
/// for incremental/differential updates. Used by the Pipeline's PatchMiddleware.
/// </summary>
public static class FileTreeDiffer
{
    /// <summary>
    /// Produce delta file pairs for the given diff between old and updated snapshots.
    /// Returns pairs of (sourcePath, relativePath) for files that need to be patched.
    /// </summary>
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
    /// Produce the list of relative paths that should be deleted based on diff.
    /// </summary>
    public static IReadOnlyList<string> ProduceDeletes(FileTreeDiff diff)
        => diff.Deleted;

    /// <summary>
    /// Determine the optimal update mode: incremental (delta) if small diff, full if large.
    /// Returns true if delta patching is recommended.
    /// </summary>
    public static bool ShouldUseDeltaPatching(FileTreeDiff diff, int totalFileCount, double thresholdPercent = 0.5)
    {
        if (totalFileCount == 0) return false;
        var changeRatio = (double)diff.TotalChanges / totalFileCount;
        return changeRatio <= thresholdPercent;
    }
}
