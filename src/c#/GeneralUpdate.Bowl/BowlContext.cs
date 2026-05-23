using System;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Bowl;

/// <summary>
/// Immutable execution context for Bowl surveillance.
/// Replaces the mutable <see cref="Strategys.MonitorParameter"/>.
/// </summary>
public readonly record struct BowlContext
{
    /// <summary>The name or PID of the process being monitored.</summary>
    public string ProcessNameOrId { get; init; }

    /// <summary>Dump file name, typically "{version}_fail.dmp".</summary>
    public string DumpFileName { get; init; }

    /// <summary>Crash report file name, typically "{version}_fail.json".</summary>
    public string FailFileName { get; init; }

    /// <summary>Application install root path.</summary>
    public string TargetPath { get; init; }

    /// <summary>Directory for failure artifacts: {TargetPath}/fail/{version}.</summary>
    public string FailDirectory { get; init; }

    /// <summary>Backup directory: {TargetPath}/{version}.</summary>
    public string BackupDirectory { get; init; }

    /// <summary>
    /// Work mode:
    /// <list type="bullet">
    ///   <item><c>"Upgrade"</c> — integrated with GeneralUpdate upgrade pipeline. On crash, restores backup and sets the <c>UpgradeFail</c> environment variable.</item>
    ///   <item><c>"Normal"</c> — standalone monitoring. On crash, exports diagnostics but does not restore or set environment variables.</item>
    /// </list>
    /// </summary>
    public string WorkModel { get; init; }

    /// <summary>Extended field, typically the version number.</summary>
    public string ExtendedField { get; init; }

    /// <summary>Child process timeout in milliseconds. Default 30000 (30s).</summary>
    public int TimeoutMs { get; init; }

    /// <summary>Dump capture type.</summary>
    public DumpType DumpType { get; init; }

    /// <summary>When <c>true</c> and <see cref="WorkModel"/> is "Upgrade", restores backup on crash.</summary>
    public bool AutoRestore { get; init; }

    /// <summary>Optional callback invoked when a crash is detected.</summary>
    public Func<CrashInfo, CancellationToken, Task>? OnCrash { get; init; }

    /// <summary>Returns a context with sensible defaults applied.</summary>
    public BowlContext Normalize()
    {
        return new BowlContext
        {
            ProcessNameOrId = ProcessNameOrId,
            DumpFileName = DumpFileName,
            FailFileName = FailFileName,
            TargetPath = TargetPath,
            FailDirectory = FailDirectory,
            BackupDirectory = BackupDirectory,
            WorkModel = string.IsNullOrEmpty(WorkModel) ? "Upgrade" : WorkModel,
            ExtendedField = ExtendedField,
            TimeoutMs = TimeoutMs <= 0 ? 30_000 : TimeoutMs,
            DumpType = DumpType == default ? DumpType.Full : DumpType,
            AutoRestore = AutoRestore,
            OnCrash = OnCrash,
        };
    }
}
