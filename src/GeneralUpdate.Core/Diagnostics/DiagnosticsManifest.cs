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
/// <param name="SelfCheckReportJson">Serialized <c>SelfCheckReport</c>, when available.</param>
/// <param name="RollbackResultJson">Serialized <c>RollbackResult</c>, when available.</param>
public sealed record DiagnosticsCollectRequest(
    string InstallPath,
    string Kind,
    string? TargetVersion,
    string? Reason,
    string? SelfCheckReportJson = null,
    string? RollbackResultJson = null);

/// <summary>
///     Metadata declared alongside an uploaded bundle. Sent with every upload so the server can
///     audit what the package contains without unpacking it (privacy/audit requirement).
/// </summary>
public sealed class DiagnosticsManifest
{
    /// <summary>Manifest contract version.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("schema")]
    public int Schema { get; set; } = CurrentSchema;

    /// <summary>Current manifest contract version.</summary>
    public const int CurrentSchema = 1;

    /// <summary>One of <see cref="DiagnosticsKinds"/>.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("kind")]
    public string Kind { get; set; } = DiagnosticsKinds.Manual;

    /// <summary>Version the diagnostics are about.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public string? Version { get; set; }

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
