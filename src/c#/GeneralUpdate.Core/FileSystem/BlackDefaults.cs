using System.Collections.Generic;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>Built-in default blacklist items.</summary>
public static class BlackDefaults
{
    /// <summary>Default blacklisted files (system DLLs that ship with the runtime).</summary>
    public static readonly List<string> DefaultFiles = new()
    {
        "Microsoft.Bcl.AsyncInterfaces.dll",
        "System.Collections.Immutable.dll",
        "System.IO.Pipelines.dll",
        "System.Text.Encodings.Web.dll",
        "System.Text.Json.dll"
    };

    /// <summary>Default blacklisted file extensions.</summary>
    public static readonly List<string> DefaultFormats = new()
        { ".patch", ".pdb", ".rar", ".tar", ".json", Configuration.Format.Zip.ToExtension() };

    /// <summary>Default skipped directory prefixes.</summary>
    public static readonly List<string> DefaultDirectories = new()
    {
        StorageManager.BackupRootDirectory,
        StorageManager.DirectoryName,
        StorageManager.LegacyDirectoryPrefix,
        "fail"
    };
}
