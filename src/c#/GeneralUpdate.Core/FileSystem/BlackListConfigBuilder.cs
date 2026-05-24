using System.Collections.Generic;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Fluent builder for <see cref="BlackListConfig"/>.
/// Used via <c>Bootstrap.ConfigureBlackList(cfg => cfg.AddBlackFiles(...))</c>.
/// </summary>
public class BlackListConfigBuilder
{
    private readonly List<string> _blackFiles = new();
    private readonly List<string> _blackFormats = new();
    private readonly List<string> _skipDirectories = new();

    public BlackListConfigBuilder AddBlackFiles(params string[] files)
    {
        _blackFiles.AddRange(files);
        return this;
    }

    public BlackListConfigBuilder AddBlackFormats(params string[] formats)
    {
        _blackFormats.AddRange(formats);
        return this;
    }

    public BlackListConfigBuilder AddSkipDirectories(params string[] directories)
    {
        _skipDirectories.AddRange(directories);
        return this;
    }

    public BlackListConfig Build() => new(
        BlackFiles: _blackFiles.Count > 0 ? _blackFiles.AsReadOnly() : null,
        BlackFormats: _blackFormats.Count > 0 ? _blackFormats.AsReadOnly() : null,
        SkipDirectorys: _skipDirectories.Count > 0 ? _skipDirectories.AsReadOnly() : null
    );
}
