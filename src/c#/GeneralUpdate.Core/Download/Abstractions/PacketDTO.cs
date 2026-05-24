using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>Server-side packet descriptor DTO (mirrors server contract).</summary>
public record PacketDTO
{
    public string? Name { get; set; }
    public string? Hash { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Url { get; set; }
    public string? Version { get; set; }
    public int? AppType { get; set; }
    public int? Platform { get; set; }
    public string? ProductId { get; set; }
    public bool? IsForcibly { get; set; }
    public bool? IsFreeze { get; set; }
    public string? Format { get; set; }
    public long? Size { get; set; }
    public string? FromVersion { get; set; }
    public bool? IsCrossVersion { get; set; }
    public string? MinClientVersion { get; set; }
    public string? SourceArchiveHash { get; set; }
    public string? TargetArchiveHash { get; set; }
}

public record VersionRequest(
    string AppName,
    string ClientVersion,
    string? UpgradeClientVersion,
    int? Platform,
    string? ProductId
);

public record VersionResponse(
    bool HasUpdate,
    IReadOnlyList<PacketDTO>? Packets
);
