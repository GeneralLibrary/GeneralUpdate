using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Download.Sources;

/// <summary>
/// OSS (Object Storage Service) download source.
/// Downloads the version configuration JSON from a remote URL,
/// parses it, and returns a list of <see cref="DownloadAsset"/> for the orchestrator.
/// </summary>
/// <remarks>
/// Supports AliYun, AWS S3, MinIO, and Tencent COS via signed URLs.
/// The version JSON format uses <see cref="VersionOSS"/> records.
/// </remarks>
public class OssDownloadSource : IDownloadSource
{
    private readonly HttpClient _httpClient;
    private readonly string _versionJsonUrl;
    private readonly TimeSpan _timeout;

    public OssDownloadSource(HttpClient httpClient, string versionJsonUrl, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _versionJsonUrl = versionJsonUrl ?? throw new ArgumentNullException(nameof(versionJsonUrl));
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadAsset>> ListAsync(CancellationToken token = default)
    {
        // Download and parse the version JSON from OSS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(_timeout);

        var response = await _httpClient.GetAsync(_versionJsonUrl, HttpCompletionOption.ResponseContentRead, cts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var versions = System.Text.Json.JsonSerializer.Deserialize(json, VersionOSSJsonContext.Default.ListVersionOSS);
        if (versions == null || versions.Count == 0)
            return Array.Empty<DownloadAsset>();

        // Convert VersionOSS to DownloadAsset, ordered by publish time
        return versions
            .OrderBy(v => v.PubTime)
            .Select(v =>
            {
                if (string.IsNullOrWhiteSpace(v.Url))
                    throw new InvalidOperationException(
                        $"OSS version '{v.PacketName ?? v.Version}' has no download URL.");

                var zipName = $"{v.PacketName ?? v.Version}zip";
                if (!zipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    zipName += ".zip";

                return new DownloadAsset(
                    Name: zipName,
                    Url: v.Url,
                    Size: 0,
                    SHA256: v.Hash,
                    Version: v.Version ?? "0.0.0"
                );
            })
            .ToList()
            .AsReadOnly();
    }
}
