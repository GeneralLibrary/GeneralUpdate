using System.Collections.Generic;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>Matches files/directories against a blacklist configuration.</summary>
public interface IBlackListMatcher
{
    bool IsBlacklisted(string relativeFilePath);
    bool IsBlacklistedFormat(string extension);
    bool ShouldSkipDirectory(string directoryName);
}
