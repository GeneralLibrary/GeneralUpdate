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
/// OSS (Object Storage Service) download source that retrieves a version configuration JSON
/// from a remote URL, parses it, and returns a list of <see cref="DownloadAsset"/> objects.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDownloadSource"/> and supports object storage services
/// such as AliYun OSS, AWS S3, MinIO, and Tencent COS via pre-signed URLs.
/// The version JSON format uses <see cref="VersionOSS"/> records.
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item>Downloads the version JSON file from the configured URL via HTTP GET.</item>
///   <item>Parses the JSON into a list of <see cref="VersionOSS"/> records using source-generated serialization context.</item>
///   <item>Converts each <see cref="VersionOSS"/> record into a <see cref="DownloadAsset"/> with name, URL, hash, and version.</item>
///   <item>Orders the assets by publish time (<c>PubTime</c>) in ascending order.</item>
///   <item>Returns a <see cref="DownloadSourceResult"/> containing the ordered asset list.</item>
/// </list>
/// </para>
/// <para>
/// Each asset's file name is derived from <c>PacketName</c> (or <c>Version</c> if null) with a ".zip" extension.
/// </para>
/// </remarks>
public class OssDownloadSource : IDownloadSource
{
    private readonly HttpClient _httpClient;
    private readonly string _versionJsonUrl;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="OssDownloadSource"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> instance used to download the version JSON. Must not be null.</param>
    /// <param name="versionJsonUrl">The URL of the version configuration JSON file. Must not be null or empty.</param>
    /// <param name="timeout">Optional timeout for the HTTP request. Defaults to 60 seconds if not specified.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="versionJsonUrl"/> is null.</exception>
    public OssDownloadSource(HttpClient httpClient, string versionJsonUrl, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _versionJsonUrl = versionJsonUrl ?? throw new ArgumentNullException(nameof(versionJsonUrl));
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Asynchronously retrieves the list of downloadable assets by downloading and parsing
    /// the version configuration JSON from the OSS URL.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    /// A <see cref="DownloadSourceResult"/> containing the list of <see cref="DownloadAsset"/> objects
    /// ordered by publish time, and flags indicating whether any updates are available.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when a version record has a null or empty download URL.</exception>
    /// <remarks>
    /// <para>
    /// The method performs the following steps:
    /// </para>
    /// <list type="number">
    ///   <item>Creates a linked cancellation token with the configured timeout.</item>
    ///   <item>Sends an HTTP GET request to download the complete version JSON response.</item>
    ///   <item>Deserializes the JSON into a list of <see cref="VersionOSS"/> records.</item>
    ///   <item>If no records are found, returns an empty result.</item>
    ///   <item>Otherwise, orders records by <c>PubTime</c>, converts each to a <see cref="DownloadAsset"/>,
    ///         and returns the result with both <c>HasMainUpdate</c> and <c>HasUpgradeUpdate</c> set to true.</item>
    /// </list>
    /// </remarks>
    public async Task<DownloadSourceResult> ListAsync(CancellationToken token = default)
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
            return new DownloadSourceResult { Assets = Array.Empty<DownloadAsset>() };

        // Convert VersionOSS to DownloadAsset, ordered by publish time
        var assets = versions
            .OrderBy(v => v.PubTime)
            .Select(v =>
            {
                if (string.IsNullOrWhiteSpace(v.Url))
                    throw new InvalidOperationException(
                        $"OSS version '{v.PacketName ?? v.Version}' has no download URL.");

                var zipName = $"{v.PacketName ?? v.Version}.zip";
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

        return new DownloadSourceResult
        {
            Assets = assets,
            HasMainUpdate = assets.Count > 0,
            HasUpgradeUpdate = assets.Count > 0
        };
    }
}
