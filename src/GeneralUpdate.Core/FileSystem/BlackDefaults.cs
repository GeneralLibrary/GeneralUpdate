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

    /// <summary>
    /// Creates a <see cref="BlackPolicy"/> using caller-provided lists, falling back to
    /// <see cref="DefaultFiles"/>, <see cref="DefaultFormats"/>, and <see cref="DefaultDirectories"/>
    /// for any list that is null or empty.
    /// </summary>
    public static BlackPolicy CreatePolicyWithDefaults(
        IReadOnlyList<string>? files,
        IReadOnlyList<string>? formats,
        IReadOnlyList<string>? directories)
    {
        return new BlackPolicy(
            files?.Count > 0 ? new List<string>(files) : DefaultFiles,
            formats?.Count > 0 ? new List<string>(formats) : DefaultFormats,
            directories?.Count > 0 ? new List<string>(directories) : DefaultDirectories
        );
    }
}
