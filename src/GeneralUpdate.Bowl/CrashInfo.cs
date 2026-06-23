namespace GeneralUpdate.Bowl;

/// <summary>
/// Crash detection payload passed to <see cref="BowlContext.OnCrash"/> callback.
/// </summary>
public readonly record struct CrashInfo
{
    /// <summary>Full path to the captured dump file.</summary>
    public string DumpFilePath { get; init; }

    /// <summary>Full path to the crash report JSON.</summary>
    public string CrashReportPath { get; init; }

    /// <summary>Version that crashed.</summary>
    public string Version { get; init; }

    /// <summary>Process exit code.</summary>
    public int ExitCode { get; init; }
}
