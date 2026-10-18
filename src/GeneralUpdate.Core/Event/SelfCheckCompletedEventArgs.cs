using System;
using GeneralUpdate.Core.SelfCheck;

namespace GeneralUpdate.Core.Event;

/// <summary>
///     Event arguments raised when the updater finishes a self-check session over the freshly
///     updated main application.
/// </summary>
/// <remarks>
///     Raised in both directions — a passed session lets hosts log successful verifications;
///     a failed session fires before the rollback starts, giving hosts a chance to record the
///     failure reason alongside their own telemetry. See
///     <c>GeneralUpdateBootstrap.AddListenerSelfCheckCompleted</c>.
/// </remarks>
public class SelfCheckCompletedEventArgs : EventArgs
{
    /// <summary>Whether the verification passed (application judged usable).</summary>
    public bool Passed { get; }

    /// <summary>Full rule-by-rule report (never null).</summary>
    public SelfCheckReport Report { get; }

    /// <summary>Version that was under verification.</summary>
    public string? TargetVersion { get; }

    /// <summary>Version the updater rolls back to on failure.</summary>
    public string? PreviousVersion { get; }

    public SelfCheckCompletedEventArgs(SelfCheckReport report, string? targetVersion, string? previousVersion)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
        TargetVersion = targetVersion;
        PreviousVersion = previousVersion;
        Passed = report.Passed;
    }
}
