using System.Collections.Generic;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Crash report data transfer object.
/// Serialized to JSON as the fail report when a crash is detected.
/// </summary>
internal class Crash
{
    /// <summary>Application install root path.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Directory for failure artifacts.</summary>
    public string? FailDirectory { get; set; }

    /// <summary>Backup directory path.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>The name or PID of the monitored process.</summary>
    public string? ProcessNameOrId { get; set; }

    /// <summary>Dump file name.</summary>
    public string? DumpFileName { get; set; }

    /// <summary>Crash report file name.</summary>
    public string? FailFileName { get; set; }

    /// <summary>Work mode: "Upgrade" or "Normal".</summary>
    public string? WorkModel { get; set; }

    /// <summary>Extended field, typically the version number.</summary>
    public string? ExtendedField { get; set; }

    /// <summary>Captured stdout/stderr lines from the procdump child process.</summary>
    public List<string> ProcdumpOutPutLines { get; set; } = new();
}
