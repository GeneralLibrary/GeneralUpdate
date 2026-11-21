namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Well-known values for <see cref="DiagnosticsCollectRequest.Kind"/> and
///     <see cref="DiagnosticsManifest.Kind"/>.
/// </summary>
/// <remarks>
///     The vocabulary is the server contract of GeneralSpace <c>/Fault/UploadArtifact</c>
///     (docs/自检与Bowl诊断联动服务端适配方案 §3.2): <c>crash</c> / <c>update</c> / <c>selfcheck</c>.
///     The server stores any non-empty kind verbatim (only <c>crash</c> opens a StartupFail
///     fault ticket), so <see cref="Manual"/> stays a distinct, honest value instead of
///     masquerading as an update-failure diagnostic.
/// </remarks>
public static class DiagnosticsKinds
{
    /// <summary>
    ///     Real-time bundle produced by <see cref="SelfCheck.RollbackManager"/> after a rollback.
    ///     Per the server adaptation design the rollback evidence is a self-check failure
    ///     diagnostic (kind=selfcheck + recordId), so it shares the self-check bucket.
    /// </summary>
    public const string Rollback = SelfCheck;

    /// <summary>
    ///     Real-time bundle produced after a failed self-check session (with or without rollback).
    /// </summary>
    public const string SelfCheck = "selfcheck";

    /// <summary>Update-failure diagnostic (pipeline failure evidence); also the crash-outbox sibling bucket.</summary>
    public const string Update = "update";

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

    // ---- Server attribution identity (GeneralSpace /Fault/UploadArtifact form fields) ----
    // Populated by the bootstrap from the verification configuration so every bundle —
    // real-time and outbox — carries the same client identity without call-site plumbing.

    /// <summary>Client identity used for verification (<c>AppSecretKey</c>); empty when unknown.</summary>
    public string? AppKey { get; set; }

    /// <summary>Product the diagnostics belong to; empty when unknown.</summary>
    public string? ProductId { get; set; }

    /// <summary>Target platform per the GeneralSpace convention (1 = Windows, 2 = Linux); null when unknown.</summary>
    public int? Platform { get; set; }

    /// <summary>Monitored / verified process name (manifest <c>targetName</c>); empty when unknown.</summary>
    public string? TargetName { get; set; }
}
