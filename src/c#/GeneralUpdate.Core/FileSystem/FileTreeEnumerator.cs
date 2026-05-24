using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Recursively enumerates files in a directory,
/// applying blacklist filtering via IBlackListMatcher.
/// </summary>
public class FileTreeEnumerator
{
    private readonly IBlackListMatcher _matcher;

    public FileTreeEnumerator(IBlackListMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>
    /// Enumerate all files under <paramref name="rootPath"/>,
    /// skipping blacklisted files and directories.
    /// </summary>
    public IEnumerable<string> EnumerateFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            yield break;

        foreach (var filePath in Directory.EnumerateFiles(rootPath))
        {
            var relativePath = Path.GetFileName(filePath);
            if (!_matcher.IsBlacklisted(relativePath))
                yield return filePath;
        }

        foreach (var dirPath in Directory.EnumerateDirectories(rootPath))
        {
            var dirName = Path.GetFileName(dirPath);
            if (_matcher.ShouldSkipDirectory(dirName))
                continue;

            foreach (var file in EnumerateFiles(dirPath))
                yield return file;
        }
    }

    /// <summary>
    /// Create an enumerator from a BlackListConfig.
    /// </summary>
    public static FileTreeEnumerator FromConfig(BlackListConfig config)
        => new(new DefaultBlackListMatcher(config));
}
