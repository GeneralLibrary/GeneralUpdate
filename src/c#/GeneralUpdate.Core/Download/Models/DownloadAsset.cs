using System.Collections.Generic;

namespace GeneralUpdate.Core.Download.Models;

/// <summary>Download resource descriptor — maps from server PacketDTO / VerificationResultDTO.</summary>
public record DownloadAsset(
    string Name,
    string Url,
    long Size,
    string? SHA256,
    string Version,
    DownloadPriority Priority = DownloadPriority.Normal,
    bool IsCrossVersion = false,
    string? FromVersion = null,
    string? MinClientVersion = null,
    string? SourceArchiveHash = null,
    string? TargetArchiveHash = null,
    bool IsForcibly = false,
    bool IsFreeze = false,
    int RecordId = 0,
    int? AppType = null,
    string? AuthScheme = null,
    string? AuthToken = null
);

/// <summary>Ordered download plan built from server response.</summary>
public record DownloadPlan(IReadOnlyList<DownloadAsset> Assets, bool IsForcibly)
{
    public static DownloadPlan Empty { get; } = new(new List<DownloadAsset>(), false);
    public bool HasAssets => Assets.Count > 0;
}
