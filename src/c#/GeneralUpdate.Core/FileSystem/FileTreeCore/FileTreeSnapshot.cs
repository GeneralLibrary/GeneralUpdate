using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Immutable snapshot of a file entry in a directory tree.
/// Captures path, size, and modification timestamp for comparison.
/// </summary>
public readonly record struct FileEntry(
    string RelativePath,
    long Size,
    DateTime LastWriteTimeUtc
);

/// <summary>
/// Immutable snapshot of a directory tree at a point in time.
/// Created by <see cref="FileTreeEnumerator"/> + <see cref="IBlackListMatcher"/>.
/// </summary>
public sealed class FileTreeSnapshot
{
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public string RootPath { get; }
    public IReadOnlyList<FileEntry> Entries { get; }

    public FileTreeSnapshot(string rootPath, IEnumerable<FileEntry> entries)
    {
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        Entries = (entries ?? Array.Empty<FileEntry>()).ToList();
    }

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

    public static FileTreeSnapshot Empty(string rootPath) => new(rootPath, Array.Empty<FileEntry>());
}
