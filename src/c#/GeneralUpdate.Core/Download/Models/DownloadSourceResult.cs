using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Download.Models;

/// <summary>
/// Result from <see cref="Abstractions.IDownloadSource.ListAsync"/>,
/// carrying the download assets plus flags indicating which side (main/upgrade)
/// the server returned version information for.
/// </summary>
public class DownloadSourceResult
{
    public IReadOnlyList<DownloadAsset> Assets { get; init; } = Array.Empty<DownloadAsset>();
    public bool HasMainUpdate { get; init; }
    public bool HasUpgradeUpdate { get; init; }
}
