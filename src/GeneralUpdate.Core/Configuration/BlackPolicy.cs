using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration;

public sealed record BlackPolicy(
    IReadOnlyList<string>? Files = null,
    IReadOnlyList<string>? Formats = null,
    IReadOnlyList<string>? Directories = null
)
{
    public static BlackPolicy Empty { get; } = new();
    public bool HasRules =>
        (Files?.Count > 0) || (Formats?.Count > 0) || (Directories?.Count > 0);
}
