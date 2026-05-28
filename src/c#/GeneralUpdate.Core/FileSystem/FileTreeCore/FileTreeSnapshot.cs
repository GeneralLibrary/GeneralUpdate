using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Represents an immutable file entry record containing a snapshot of a single file within a directory tree.
/// Records the file's relative path, size, and last write time, used for file difference comparison.
/// </summary>
/// <param name="RelativePath">The relative path of the file with respect to the root directory.</param>
/// <param name="Size">The file size in bytes.</param>
/// <param name="LastWriteTimeUtc">The last write time of the file in UTC.</param>
/// <remarks>
/// FileEntry is the fundamental data unit of <see cref="FileTreeSnapshot"/>.
/// File modification is determined by comparing <see cref="Size"/> and <see cref="LastWriteTimeUtc"/>.
/// This record type is a read-only struct, guaranteeing snapshot immutability.
/// </remarks>
public readonly record struct FileEntry(
    string RelativePath,
    long Size,
    DateTime LastWriteTimeUtc
);

/// <summary>
/// An immutable directory tree snapshot that records the metadata state of all files in a directory at a specific point in time.
/// </summary>
/// <remarks>
/// <para>
/// FileTreeSnapshot is created by <see cref="FileTreeEnumerator"/> in conjunction with <see cref="IBlackListMatcher"/>,
/// automatically skipping blacklisted files and directories during file traversal.
/// </para>
/// <para>
/// Typical usage flow:
/// <list type="number">
///   <item><description>Create a snapshot from a root directory using the <see cref="FromEnumerator"/> method.</description></item>
///   <item><description>Serialize the snapshot to JSON for storage or transmission.</description></item>
///   <item><description>During differential updates, use <see cref="FileTreeComparer.Compare"/> to compare old and new snapshots.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class FileTreeSnapshot
{
    /// <summary>
    /// Gets the UTC timestamp when the snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the root directory path that was snapshotted.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets a read-only list of all file entries contained in the snapshot.
    /// </summary>
    public IReadOnlyList<FileEntry> Entries { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTreeSnapshot"/> class with the specified root directory path and file entries.
    /// </summary>
    /// <param name="rootPath">The root directory path that was snapshotted.</param>
    /// <param name="entries">The collection of file entries.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootPath"/> is <c>null</c>.</exception>
    public FileTreeSnapshot(string rootPath, IEnumerable<FileEntry> entries)
    {
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        Entries = (entries ?? Array.Empty<FileEntry>()).ToList();
    }

    /// <summary>
    /// Creates a file system snapshot by traversing the specified root directory using a <see cref="FileTreeEnumerator"/>.
    /// </summary>
    /// <param name="rootPath">The root directory path to create a snapshot of.</param>
    /// <param name="enumerator">A <see cref="FileTreeEnumerator"/> instance configured with blacklist rules.</param>
    /// <returns>A new snapshot containing all files (not filtered by the blacklist) in the directory.</returns>
    /// <remarks>
    /// This method enumerates all files under the root directory (skipping blacklisted files and directories),
    /// creates a <see cref="FileEntry"/> record for each file (including size and last write time),
    /// and then wraps them into a <see cref="FileTreeSnapshot"/> to return.
    /// The current UTC time is recorded as <see cref="CreatedAt"/> when the snapshot is created.
    /// </remarks>
    public static FileTreeSnapshot FromEnumerator(string rootPath, FileTreeEnumerator enumerator)
    {
        var entries = new List<FileEntry>();
        var normalizedRoot = rootPath.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())
            ? rootPath
            : rootPath + System.IO.Path.DirectorySeparatorChar;

        foreach (var filePath in enumerator.EnumerateFiles(rootPath))
        {
            var fi = new System.IO.FileInfo(filePath);
            // Manual relative path (netstandard2.0 compatible)
            var relative = filePath.StartsWith(normalizedRoot)
                ? filePath.Substring(normalizedRoot.Length)
                : filePath;
            entries.Add(new FileEntry(relative, fi.Length, fi.LastWriteTimeUtc));
        }
        return new FileTreeSnapshot(rootPath, entries);
    }

    /// <summary>
    /// Creates an empty file system snapshot (containing no file entries), used to represent an empty directory or as a placeholder.
    /// </summary>
    /// <param name="rootPath">The root directory path.</param>
    /// <returns>An empty <see cref="FileTreeSnapshot"/> instance.</returns>
    public static FileTreeSnapshot Empty(string rootPath) => new(rootPath, Array.Empty<FileEntry>());
}
