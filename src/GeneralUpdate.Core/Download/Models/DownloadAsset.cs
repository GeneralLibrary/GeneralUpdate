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
    int PackageType = 0,
    string? MinClientVersion = null,
    string? FallbackFullName = null,
    string? FallbackFullUrl = null,
    string? FallbackFullHash = null,
    string? FallbackFullVersion = null,
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

    /// <summary>
    /// Full replacement packages downloaded alongside chain packages as fallback.
    /// Not applied during the normal pipeline — only used when a chain package fails
    /// and its <see cref="DownloadAsset.FallbackFullUrl"/> matches an entry here.
    /// </summary>
    public IReadOnlyList<DownloadAsset> FallbackFulls { get; init; } = new List<DownloadAsset>();
}
