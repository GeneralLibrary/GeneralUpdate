using System;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>Glob-based blacklist matcher driven by BlackListConfig.</summary>
public class DefaultBlackListMatcher : IBlackListMatcher
{
    private readonly BlackListConfig _config;

    public DefaultBlackListMatcher(BlackListConfig config)
        => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>Create a matcher from GlobalConfigInfo blacklist properties.</summary>
    public static DefaultBlackListMatcher FromConfigInfo(GlobalConfigInfo config)
    {
        var cfg = new BlackListConfig(
            config.BlackFiles?.Count > 0 ? config.BlackFiles : null,
            config.BlackFormats?.Count > 0 ? config.BlackFormats : null,
            config.SkipDirectorys?.Count > 0 ? config.SkipDirectorys : null);
        return new DefaultBlackListMatcher(cfg);
    }

    public bool IsBlacklisted(string relativeFilePath)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var ext = Path.GetExtension(relativeFilePath);

        if (_config.BlackFiles?.Any(f => MatchGlob(fileName, f)) == true) return true;
        if (_config.BlackFormats?.Any(f => string.Equals(f, ext, StringComparison.OrdinalIgnoreCase)) == true) return true;
        return false;
    }

    public bool IsBlacklistedFormat(string extension)
        => _config.BlackFormats?.Any(f => string.Equals(f, extension, StringComparison.OrdinalIgnoreCase)) == true;

    public bool ShouldSkipDirectory(string directoryName)
        => _config.SkipDirectorys?.Any(d =>
            directoryName.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0) == true;

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
