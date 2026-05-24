using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration;

public sealed record BlackListConfig(
    IReadOnlyList<string>? BlackFiles = null,
    IReadOnlyList<string>? BlackFormats = null,
    IReadOnlyList<string>? SkipDirectorys = null
)
{
    public static BlackListConfig Empty { get; } = new();
    public bool HasRules =>
        (BlackFiles?.Count > 0) || (BlackFormats?.Count > 0) || (SkipDirectorys?.Count > 0);
}
