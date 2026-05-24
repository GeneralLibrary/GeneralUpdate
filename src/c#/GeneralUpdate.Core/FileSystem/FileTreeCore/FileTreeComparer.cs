using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Result of comparing two file tree snapshots.
/// </summary>
public readonly record struct FileTreeDiff(
    IReadOnlyList<FileEntry> Added,
    IReadOnlyList<FileEntry> Modified,
    IReadOnlyList<string> Deleted
)
{
    public bool HasChanges => Added.Count > 0 || Modified.Count > 0 || Deleted.Count > 0;
    public int TotalChanges => Added.Count + Modified.Count + Deleted.Count;

    public static FileTreeDiff Empty { get; } = new(
        Array.Empty<FileEntry>(), Array.Empty<FileEntry>(), Array.Empty<string>());
}

/// <summary>
/// Compares two <see cref="FileTreeSnapshot"/> instances and produces a <see cref="FileTreeDiff"/>.
/// Identifies added, modified, and deleted files between old and new state.
/// </summary>
public static class FileTreeComparer
{
    /// <summary>
    /// Compare two snapshots. <paramref name="old"/> is the baseline, <paramref name="updated"/> is the new state.
    /// </summary>
    public static FileTreeDiff Compare(FileTreeSnapshot old, FileTreeSnapshot updated)
    {
        if (old == null) throw new ArgumentNullException(nameof(old));
        if (updated == null) throw new ArgumentNullException(nameof(updated));

        var oldMap = old.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);
        var newMap = updated.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);

        var added = new List<FileEntry>();
        var modified = new List<FileEntry>();
        var deleted = new List<string>();

        // Files present in updated but not in old → Added
        // Files present in updated and old with different size or time → Modified
        foreach (var kv in newMap)
        {
            var path = kv.Key;
            var entry = kv.Value;
            if (!oldMap.TryGetValue(path, out var oldEntry))
            {
                added.Add(entry);
            }
            else if (oldEntry.Size != entry.Size || oldEntry.LastWriteTimeUtc != entry.LastWriteTimeUtc)
            {
                modified.Add(entry);
            }
        }

        // Files present in old but not in updated → Deleted
        foreach (var path in oldMap.Keys)
        {
            if (!newMap.ContainsKey(path))
                deleted.Add(path);
        }

        return new FileTreeDiff(added.AsReadOnly(), modified.AsReadOnly(), deleted.AsReadOnly());
    }

    /// <summary>
    /// Quick check: compare two snapshots and return true if any files changed.
    /// Short-circuits on first difference.
    /// </summary>
    public static bool HasChanges(FileTreeSnapshot old, FileTreeSnapshot updated)
    {
        if (old.Entries.Count != updated.Entries.Count) return true;

        var oldMap = old.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);
        var newDict = updated.Entries.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in newDict)
        {
            if (!oldMap.TryGetValue(kv.Key, out var oldEntry)) return true;
            if (oldEntry.Size != kv.Value.Size || oldEntry.LastWriteTimeUtc != kv.Value.LastWriteTimeUtc) return true;
        }
        return false;
    }
}
