using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>Matches files/directories against a blacklist configuration.</summary>
public interface IBlackListMatcher
{
    bool IsBlacklisted(string relativeFilePath);
    bool IsBlacklistedFormat(string extension);
    bool ShouldSkipDirectory(string directoryName);
}

/// <summary>
/// Thread-safe blacklist manager. Uses Lazy<T> singleton.
/// Matching is case-insensitive and supports prefix matching for skip directories.
/// </summary>
public class BlackListManager : IBlackListMatcher
{
    private static readonly Lazy<BlackListManager> _lazy = new(() => new BlackListManager());
    private readonly object _lock = new();

    private readonly List<string> _blackFiles =
    [
        "Microsoft.Bcl.AsyncInterfaces.dll",
        "System.Collections.Immutable.dll",
        "System.IO.Pipelines.dll",
        "System.Text.Encodings.Web.dll",
        "System.Text.Json.dll"
    ];

    private readonly List<string> _blackFormats = [".patch", ".pdb", ".rar", ".tar", ".json", Format.ZIP];
    private readonly List<string> _skipDirs = ["app-", "fail"];

    private BlackListManager() { }

    public static BlackListManager Instance => _lazy.Value;

    // Read-only accessors
    public IReadOnlyList<string> BlackFiles { get { lock (_lock) return _blackFiles.ToList(); } }
    public IReadOnlyList<string> BlackFormats { get { lock (_lock) return _blackFormats.ToList(); } }
    public IReadOnlyList<string> SkipDirectorys { get { lock (_lock) return _skipDirs.ToList(); } }

    // Mutation
    public void AddBlackFiles(List<string>? files)
    { if (files == null) return; lock (_lock) { foreach (var f in files) AddBlackFileLocked(f); } }
    public void AddBlackFile(string file)
    { if (string.IsNullOrWhiteSpace(file)) return; lock (_lock) AddBlackFileLocked(file); }
    private void AddBlackFileLocked(string file)
    { if (!_blackFiles.Contains(file)) _blackFiles.Add(file); }

    public void AddBlackFormats(List<string>? formats)
    { if (formats == null) return; lock (_lock) { foreach (var f in formats) AddBlackFormatLocked(f); } }
    public void AddBlackFormat(string format)
    { if (string.IsNullOrWhiteSpace(format)) return; lock (_lock) AddBlackFormatLocked(format); }
    private void AddBlackFormatLocked(string format)
    { if (!_blackFormats.Contains(format)) _blackFormats.Add(format); }

    public void AddSkipDirectorys(List<string>? dirs)
    { if (dirs == null) return; lock (_lock) { foreach (var d in dirs) AddSkipDirectoryLocked(d); } }
    public void AddSkipDirectory(string dir)
    { if (string.IsNullOrWhiteSpace(dir)) return; lock (_lock) AddSkipDirectoryLocked(dir); }
    private void AddSkipDirectoryLocked(string dir)
    { if (!_skipDirs.Contains(dir)) _skipDirs.Add(dir); }

    // Matching (read operations — no lock needed for immutable reads)
    public bool IsBlacklisted(string relativeFilePath)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var ext = Path.GetExtension(relativeFilePath);
        lock (_lock)
            return _blackFiles.Contains(fileName) || _blackFormats.Contains(ext);
    }

    public bool IsBlacklistedFormat(string extension)
    {
        lock (_lock) return _blackFormats.Contains(extension);
    }

    public bool ShouldSkipDirectory(string directoryName)
    {
        lock (_lock) return _skipDirs.Any(d => directoryName.Contains(d));
    }
}
