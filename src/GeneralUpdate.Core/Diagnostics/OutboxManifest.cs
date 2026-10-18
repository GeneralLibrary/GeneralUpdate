using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Reader-side DTO of the Bowl outbox manifest (<c>{install}/fail/{version}/{guid}/manifest.json</c>,
///     schema 2 disk contract — see docs/bowl-redesign.md §3.2). Bowl writes it, Core reads it;
///     there is no IPC, no shared state — the directory layout IS the contract.
/// </summary>
/// <remarks>
///     Contract rules ("existing means pending, deleted means uploaded, schema tolerance"):
///     a <c>{guid}/</c> directory that exists is pending upload; Core deletes the whole directory
///     after a successful upload; a manifest whose <see cref="Schema"/> is not 2 is skipped with a
///     warning so the two components can evolve independently.
/// </remarks>
public sealed class OutboxManifest
{
    /// <summary>Contract version; only <see cref="SupportedSchema"/> is processed.</summary>
    [JsonPropertyName("schema")]
    public int Schema { get; set; }

    /// <summary>The schema this Core build knows how to process.</summary>
    public const int SupportedSchema = 2;

    /// <summary>Case identifier; doubles as the server-side idempotency key.</summary>
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;

    /// <summary>Evidence kind; crash outboxes use <see cref="DiagnosticsKinds.Crash"/>.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = DiagnosticsKinds.Crash;

    /// <summary>Monitored process name (diagnostic metadata).</summary>
    [JsonPropertyName("targetName")]
    public string? TargetName { get; set; }

    /// <summary>Application version the evidence belongs to (also the parent directory name).</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Exit code of the crashed process (Bowl's exit-code verdict).</summary>
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    /// <summary>UTC timestamp of evidence creation.</summary>
    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>File names contained in the evidence directory (server audit list).</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    /// <summary>Outbox state; see <see cref="OutboxStates"/>.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = OutboxStates.Pending;

    /// <summary>Upload attempts already made; Core increments it after each failed upload.</summary>
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }
}
