using System;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Default implementation of a Glob-pattern-based blacklist matcher, driven by <see cref="BlackListConfig"/>.
/// </summary>
/// <remarks>
/// <para>
/// DefaultBlackListMatcher implements the <see cref="IBlackListMatcher"/> interface and provides three types of blacklist filtering:
/// </para>
/// <list type="bullet">
///   <item><description><b>Blacklist files</b>: Matches file names using Glob patterns (e.g., <c>*.log</c> matches all log files).</description></item>
///   <item><description><b>Blacklist formats</b>: Performs case-insensitive exact matching by file extension.</description></item>
///   <item><description><b>Skip directories</b>: Uses case-insensitive substring containment matching to determine whether to skip subdirectories.</description></item>
/// </list>
/// <para>
/// Instances of this class are typically created from <see cref="GlobalConfigInfo"/> via the <see cref="FromConfigInfo"/> factory method,
/// or constructed directly by passing a <see cref="BlackListConfig"/> configuration object.
/// </para>
/// </remarks>
public class DefaultBlackListMatcher : IBlackListMatcher
{
    private readonly BlackListConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBlackListMatcher"/> class with the specified blacklist configuration.
    /// </summary>
    /// <param name="config">The blacklist configuration object containing file name patterns, extensions, and directory names to exclude.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is <c>null</c>.</exception>
    public DefaultBlackListMatcher(BlackListConfig config)
        => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Creates a matcher instance from the blacklist properties in <see cref="GlobalConfigInfo"/>.
    /// </summary>
    /// <param name="config">The global configuration information object.</param>
    /// <returns>A configured <see cref="DefaultBlackListMatcher"/> instance.</returns>
    /// <remarks>
    /// This factory method only sets the corresponding blacklist rules when the respective list has elements (<c>Count &gt; 0</c>);
    /// empty lists are treated as <c>null</c> (meaning that type of filtering is disabled).
    /// </remarks>
    public static DefaultBlackListMatcher FromConfigInfo(GlobalConfigInfo config)
    {
        var cfg = new BlackListConfig(
            config.BlackFiles?.Count > 0 ? config.BlackFiles : null,
            config.BlackFormats?.Count > 0 ? config.BlackFormats : null,
            config.SkipDirectorys?.Count > 0 ? config.SkipDirectorys : null);
        return new DefaultBlackListMatcher(cfg);
    }

    /// <summary>
    /// Determines whether the specified file is excluded by the blacklist matching rules.
    /// </summary>
    /// <param name="relativeFilePath">The relative path or file name of the file.</param>
    /// <returns><c>true</c> if the file matches the blacklist rules; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// The matching logic checks in the following order:
    /// <list type="number">
    ///   <item><description>Whether the file name matches any Glob pattern in <c>BlackFiles</c>.</description></item>
    ///   <item><description>Whether the file extension matches any format in <c>BlackFormats</c>.</description></item>
    /// </list>
    /// The file is considered blacklisted if any condition is met.
    /// </remarks>
    public bool IsBlacklisted(string relativeFilePath)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var ext = Path.GetExtension(relativeFilePath);

        if (_config.BlackFiles?.Any(f => MatchGlob(fileName, f)) == true) return true;
        if (_config.BlackFormats?.Any(f => string.Equals(f, ext, StringComparison.OrdinalIgnoreCase)) == true) return true;
        return false;
    }

    /// <summary>
    /// Determines whether the specified file extension is in the blacklist format list.
    /// </summary>
    /// <param name="extension">The file extension (e.g., <c>.log</c>, <c>.tmp</c>).</param>
    /// <returns><c>true</c> if the extension is in the blacklist; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Uses case-insensitive string comparison for evaluation.
    /// </remarks>
    public bool IsBlacklistedFormat(string extension)
        => _config.BlackFormats?.Any(f => string.Equals(f, extension, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>
    /// Determines whether the specified directory name should be skipped (i.e., not entered during traversal).
    /// </summary>
    /// <param name="directoryName">The directory name.</param>
    /// <returns><c>true</c> if the directory name matches any skip rule; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Uses case-insensitive substring containment matching (<c>string.IndexOf</c>) for evaluation.
    /// The directory is considered skippable if its name contains any string from the <c>SkipDirectorys</c> list.
    /// </remarks>
    public bool ShouldSkipDirectory(string directoryName)
        => _config.SkipDirectorys?.Any(d =>
            directoryName.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0) == true;

    /// <summary>
    /// Matches a file name against a simple Glob pattern.
    /// </summary>
    /// <param name="input">The file name to match.</param>
    /// <param name="pattern">The Glob pattern (supports <c>*.xxx</c> wildcard prefix matching and exact matching).</param>
    /// <returns><c>true</c> if the file name matches the pattern; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Currently supports two Glob patterns:
    /// <list type="bullet">
    ///   <item><description><c>*.log</c>: Wildcard matching for names ending with <c>.log</c>.</description></item>
    ///   <item><description><c>filename</c>: Case-insensitive exact file name matching.</description></item>
    /// </list>
    /// </remarks>
    private static bool MatchGlob(string input, string pattern)
    {
        if (pattern.StartsWith("*."))
        {
            var ext = pattern.Substring(1);
            return input.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
