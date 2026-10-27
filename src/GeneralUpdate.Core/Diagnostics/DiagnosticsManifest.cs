using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Input of one real-time diagnostics collection — everything the bundle should contain.
///     Outbox uploads (Bowl crash evidence) do not use this record; they package the existing
///     evidence directory as-is (see <see cref="PendingDiagnosticsScanner"/>).
/// </summary>
/// <param name="InstallPath">Installation root (source for manifest snapshot and trace-log tail).</param>
/// <param name="Kind">One of <see cref="DiagnosticsKinds"/>.</param>
/// <param name="TargetVersion">Version the diagnostics are about (failed target / rolled-back target).</param>
/// <param name="Reason">Free-form reason (first self-check failure, exception message...).</param>
/// <param name="RecordId">
///     Server update record this bundle belongs to (0 = none). Written to the upload so the
///     server can link the artifact back to the failing upgrade/push record.
/// </param>
/// <param name="RecordType">Server record type matching <paramref name="RecordId"/> (1 = poll, 2 = push).</param>
/// <param name="SelfCheckReportJson">Serialized <c>SelfCheckReport</c>, when available.</param>
/// <param name="RollbackResultJson">Serialized <c>RollbackResult</c>, when available.</param>
public sealed record DiagnosticsCollectRequest(
    string InstallPath,
    string Kind,
    string? TargetVersion,
    string? Reason,
    int RecordId = 0,
    int RecordType = 0,
    string? SelfCheckReportJson = null,
    string? RollbackResultJson = null);

/// <summary>
///     Metadata declared alongside an uploaded bundle. It travels BOTH ways of the server
///     contract (GeneralSpace <c>/Fault/UploadArtifact</c>): as multipart form fields on the
///     wire — <c>guid</c> doubles as the server-side idempotency key — and as the
///     <c>manifest.json</c> entry inside the archive, so the server can audit what the
///     package contains without trusting the form (privacy/audit requirement).
/// </summary>
public sealed class DiagnosticsManifest
{
    /// <summary>Manifest contract version. Aligned with the Bowl outbox disk contract (schema 2).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("schema")]
    public int Schema { get; set; } = CurrentSchema;

    /// <summary>Current manifest contract version (matches <see cref="OutboxManifest.SupportedSchema"/>).</summary>
    public const int CurrentSchema = OutboxManifest.SupportedSchema;

    /// <summary>
    ///     Canonical bundle id (GUID "D" form). Required — the server deduplicates uploads on it;
    ///     callers generate it once at collection time and reuse it across retries.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;

    /// <summary>One of <see cref="DiagnosticsKinds"/>.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("kind")]
    public string Kind { get; set; } = DiagnosticsKinds.Manual;

    /// <summary>Monitored process name (diagnostic metadata; crash outboxes carry Bowl's target).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("targetName")]
    public string? TargetName { get; set; }

    /// <summary>Version the diagnostics are about.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Client identity used for verification (server attribute column); empty when unknown.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("appKey")]
    public string? AppKey { get; set; }

    /// <summary>Product the diagnostics belong to (server attribution); empty when unknown.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    /// <summary>Target platform per the GeneralSpace convention (1 = Windows, 2 = Linux); null when unknown.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("platform")]
    public int? Platform { get; set; }

    /// <summary>Server update record id this bundle belongs to; 0/absent = not linked to a record.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("recordId")]
    public int? RecordId { get; set; }

    /// <summary>Server record type matching <see cref="RecordId"/> (1 = upgrade/poll, 2 = push).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("recordType")]
    public int? RecordType { get; set; }

    /// <summary>Free-form reason collected from the caller.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>File names contained in the uploaded archive (audit list).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    /// <summary>UTC creation timestamp.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Result of a collection: the packed archive plus its declared metadata.</summary>
/// <param name="ZipPath">Full path of the temporary zip archive (caller/uploader deletes it).</param>
/// <param name="Manifest">Metadata declared for the archive.</param>
public sealed record DiagnosticsBundle(string ZipPath, DiagnosticsManifest Manifest);
