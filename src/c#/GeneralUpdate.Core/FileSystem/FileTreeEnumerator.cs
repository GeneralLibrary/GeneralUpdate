using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Recursively enumerates all files in a specified directory while applying blacklist filtering via <see cref="IBlackListMatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// FileTreeEnumerator is a lightweight file traverser. The core flow is:
/// </para>
/// <list type="number">
///   <item><description>Begins recursive traversal from the root directory level by level.</description></item>
///   <item><description>For each file, calls <c>IBlackListMatcher.IsBlacklisted</c> to determine whether to skip it.</description></item>
///   <item><description>For each subdirectory, calls <c>IBlackListMatcher.ShouldSkipDirectory</c> to determine whether to enter it.</description></item>
/// </list>
/// <para>
/// This traverser is commonly used as an input source for creating <see cref="FileTreeSnapshot"/> instances.
/// It can be created directly from a <see cref="BlackListConfig"/> via the <see cref="FromConfig"/> factory method.
/// </para>
/// </remarks>
public class FileTreeEnumerator
{
    private readonly IBlackListMatcher _matcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTreeEnumerator"/> class with the specified blacklist matcher.
    /// </summary>
    /// <param name="matcher">The blacklist matcher instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matcher"/> is <c>null</c>.</exception>
    public FileTreeEnumerator(IBlackListMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>
    /// Enumerates all files under the root directory, skipping blacklisted files and directories.
    /// </summary>
    /// <param name="rootPath">The root directory path to enumerate.</param>
    /// <returns>A collection of full paths for all files that were not skipped.</returns>
    /// <remarks>
    /// <para>
    /// Enumeration logic:
    /// <list type="bullet">
    ///   <item><description>First enumerates all files in the root directory, skipping files whose extensions match the blacklist.</description></item>
    ///   <item><description>Then enumerates all subdirectories in the root directory, skipping those that match the blacklist.</description></item>
    ///   <item><description>Recursively performs the same operations on subdirectories that were not skipped.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This method uses <c>yield return</c> for deferred execution, processing one file at a time.
    /// </para>
    /// </remarks>
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
    /// Quickly creates a <see cref="FileTreeEnumerator"/> instance from a <see cref="BlackListConfig"/> configuration.
    /// </summary>
    /// <param name="config">The blacklist configuration object.</param>
    /// <returns>A configured <see cref="FileTreeEnumerator"/> instance.</returns>
    /// <remarks>
    /// This factory method uses <see cref="DefaultBlackListMatcher"/> as the underlying blacklist matcher implementation.
    /// </remarks>
    public static FileTreeEnumerator FromConfig(BlackListConfig config)
        => new(new DefaultBlackListMatcher(config));
}
