using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.FileSystem.FileTreeCore;

/// <summary>Recursively enumerates files with blacklist filtering and SHA256 hashing.</summary>
public class FileTreeEnumerator
{
    private readonly IBlackListMatcher? _blacklist;

    public FileTreeEnumerator(IBlackListMatcher? blacklist = null)
        => _blacklist = blacklist;

    public IEnumerable<FileEntry> Enumerate(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            if (_blacklist != null)
            {
                var relativePath = GetRelativePath(rootPath, file);
                if (_blacklist.IsBlacklisted(relativePath))
                    continue;
                var dir = Path.GetDirectoryName(relativePath);
                if (dir != null && _blacklist.ShouldSkipDirectory(dir))
                    continue;
            }

            var fi = new FileInfo(file);
            var hash = ComputeSha256(file);
            var relative = GetRelativePath(rootPath, file);
            yield return new FileEntry(relative, fi.Length, hash, fi.LastWriteTimeUtc);
        }
    }

    private static string ComputeSha256(string path)
    {
        try
        {
            using var sha = SHA256.Create();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var h = sha.ComputeHash(fs);
            return BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
        }
        catch { return string.Empty; }
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

/// <summary>Individual file entry with hash.</summary>
public record FileEntry(string RelativePath, long SizeBytes, string SHA256, DateTime LastWriteUtc);

/// <summary>Compares two file tree snapshots using SHA256 for content comparison.</summary>
public class FileTreeComparer
{
    public FileTreeDiff Compare(FileTreeSnapshot oldSnap, FileTreeSnapshot newSnap)
    {
        var added = new List<string>();
        var modified = new List<string>();
        var deleted = new List<string>();

        foreach (var kv in newSnap.Files)
        {
            var path = kv.Key;
            var newEntry = kv.Value;
            if (oldSnap.Files.TryGetValue(path, out var oldEntry))
            {
                if (oldEntry.SHA256 != newEntry.SHA256)
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
