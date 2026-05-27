using System.Collections.Generic;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>Built-in default blacklist items.</summary>
public static class BlackListDefaults
{
    /// <summary>Default blacklisted files (system DLLs that ship with the runtime).</summary>
    public static readonly List<string> DefaultBlackFiles = new()
    {
        "Microsoft.Bcl.AsyncInterfaces.dll",
        "System.Collections.Immutable.dll",
        "System.IO.Pipelines.dll",
        "System.Text.Encodings.Web.dll",
        "System.Text.Json.dll"
    };

    /// <summary>Default blacklisted file extensions.</summary>
    public static readonly List<string> DefaultBlackFormats = new()
        { ".patch", ".pdb", ".rar", ".tar", ".json", Configuration.Format.Zip.ToExtension() };

    /// <summary>Default skipped directory prefixes.</summary>
    public static readonly List<string> DefaultSkipDirectories = new()
        { "app-", "fail" };
}
