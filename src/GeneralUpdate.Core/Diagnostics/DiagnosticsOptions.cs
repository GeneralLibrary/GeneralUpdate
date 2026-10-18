namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Well-known values for <see cref="DiagnosticsCollectRequest.Kind"/> and
///     <see cref="DiagnosticsManifest.Kind"/>.
/// </summary>
public static class DiagnosticsKinds
{
    /// <summary>Real-time bundle produced by <see cref="SelfCheck.RollbackManager"/> after a rollback.</summary>
    public const string Rollback = "rollback";

    /// <summary>Real-time bundle produced after a failed self-check session (without rollback).</summary>
    public const string SelfCheck = "self-check";

    /// <summary>Crash evidence outbox written by the Bowl watchdog (schema 2 disk contract).</summary>
    public const string Crash = "crash";

    /// <summary>Manually triggered bundle via the public <see cref="DiagnosticsUploader"/>/collector API.</summary>
    public const string Manual = "manual";
}

/// <summary>
///     Well-known values for the outbox manifest <c>state</c> field (Bowl ↔ Core disk contract).
/// </summary>
/// <remarks>
///     The contract is "existing means pending, deleted means uploaded": the Core scanner only
///     ever transitions a directory to <see cref="Giveup"/> after exhausting the retry budget;
///     successful uploads remove the whole directory, so no "uploaded" state file exists.
/// </remarks>
public static class OutboxStates
{
    /// <summary>Evidence waiting for upload (initial state written by Bowl).</summary>
    public const string Pending = "pending";

    /// <summary>Retry budget exhausted; the Core scanner stops trying (Bowl's disk GC removes the directory later).</summary>
    public const string Giveup = "giveup";
}

/// <summary>
///     Runtime settings of the diagnostics channel. Populated from
///     <see cref="Option.DiagnosticsUploadEnabled"/> / <see cref="Option.DiagnosticsReportUrl"/>
///     by the bootstrap; defaults match the design document.
/// </summary>
public sealed class DiagnosticsOptions
{
    /// <summary>Master switch. Default <c>false</c> — the channel is fully inert unless enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Server endpoint receiving diagnostic bundles. Independent of the status ReportUrl.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Upload size cap. Bundles over the cap are repacked without dump files (text only). Default 20 MB.</summary>
    public long MaxSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    ///     Crash-evidence retention advertised to the Bowl watchdog (its disk GC deletes older
    ///     outbox directories). Kept here so both sides can be configured from one place.
    ///     Default 14 days.
    /// </summary>
    public int RetainDays { get; set; } = 14;

    /// <summary>Upload attempts per outbox directory before it is marked giveup. Default 5.</summary>
    public int MaxRetry { get; set; } = 5;
}
