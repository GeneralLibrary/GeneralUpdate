using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.FileSystem.FileTree;

/// <summary>Recursively enumerates files, respecting blacklist rules.</summary>
public class FileTreeEnumerator
{
    private readonly IBlackListMatcher? _blacklist;

    public FileTreeEnumerator(IBlackListMatcher? blacklist = null)
        => _blacklist = blacklist;

    public IEnumerable<FileEntry> Enumerate(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            yield break;

        foreach (var file in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = GetRelativePath(rootPath, file);
            if (_blacklist?.IsBlacklisted(relativePath) == true)
                continue;

            var dir = Path.GetDirectoryName(relativePath);
            if (dir != null && _blacklist?.ShouldSkipDirectory(dir) == true)
                continue;

            var fi = new FileInfo(file);
            yield return new FileEntry(relativePath, fi.Length, string.Empty, fi.LastWriteTimeUtc);
        }
    }

    private static string GetRelativePath(string root, string fullPath)
    {
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            root += Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullPath.Substring(root.Length)
            : fullPath;
    }
}

/// <summary>File tree snapshot for backup and comparison.</summary>
public record FileTreeSnapshot(
    string RootPath,
    IReadOnlyDictionary<string, FileEntry> Files,
    DateTime CapturedAt
);

public record FileEntry(string RelativePath, long SizeBytes, string SHA256, DateTime LastWriteUtc);

/// <summary>Compares two file tree snapshots, producing add/modify/delete lists.</summary>
public class FileTreeComparer
{
    public FileTreeDiff Compare(FileTreeSnapshot oldSnap, FileTreeSnapshot newSnap)
    {
        var added = new List<string>();
        var modified = new List<string>();
        var deleted = new List<string>();

        foreach (var (path, newEntry) in newSnap.Files)
        {
            if (oldSnap.Files.TryGetValue(path, out var oldEntry))
            {
                if (oldEntry.LastWriteUtc != newEntry.LastWriteUtc || oldEntry.SizeBytes != newEntry.SizeBytes)
                    modified.Add(path);
            }
            else
                added.Add(path);
        }

        foreach (var path in oldSnap.Files.Keys)
            if (!newSnap.Files.ContainsKey(path))
                deleted.Add(path);

        return new FileTreeDiff(added, modified, deleted);
    }
}

public record FileTreeDiff(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Modified,
    IReadOnlyList<string> Deleted
);
