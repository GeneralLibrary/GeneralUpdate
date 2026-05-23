namespace GeneralUpdate.Bowl;

/// <summary>Result of a Bowl surveillance run.</summary>
public readonly record struct BowlResult
{
    /// <summary>Whether the monitored process exited normally (exit code 0).</summary>
    public bool Success { get; init; }

    /// <summary>Process exit code.</summary>
    public int ExitCode { get; init; }

    /// <summary>Whether a dump file was captured (crash detected).</summary>
    public bool DumpCaptured { get; init; }

    /// <summary>Full path to the dump file, or <c>null</c> if no crash.</summary>
    public string? DumpFilePath { get; init; }

    /// <summary>Full path to the crash report JSON, or <c>null</c> if not generated.</summary>
    public string? CrashReportPath { get; init; }

    /// <summary>Whether the backup was restored.</summary>
    public bool Restored { get; init; }

    /// <summary>Pre-built success result.</summary>
    public static BowlResult Ok => new() { Success = true };
}
