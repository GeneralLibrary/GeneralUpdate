using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Download.Sources;

/// <summary>
/// A single GeneralSpace SignalR packet, used as the source for one push-triggered update.
/// </summary>
public sealed class PushDownloadSource : IDownloadSource
{
    private readonly DownloadAsset _asset;

    /// <summary>
    /// Parses the single JSON string delivered to the ReceiveMessage listener.
    /// The packet must identify its application type, URL and version; a missing push
    /// record ID is supported for older servers but cannot be reported.
    /// </summary>
    public PushDownloadSource(string message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        var packet = JsonSerializer.Deserialize(message, PushPayloadJsonContext.Default.PushPayload)
            ?? throw new JsonException("The push packet must not be null.");
        if (packet.AppType != (int)AppType.Client && packet.AppType != (int)AppType.Upgrade)
            throw new JsonException("The push packet must specify a Client or Upgrade AppType.");
        if (string.IsNullOrWhiteSpace(packet.Url) || string.IsNullOrWhiteSpace(packet.Version))
            throw new JsonException("The push packet must include a download URL and version.");
        if (packet.RecordId < 0 || packet.Size < 0 || packet.PackageType < 0 || packet.PackageType > 2)
            throw new JsonException("The push packet contains invalid record, size or package type values.");

        var name = string.IsNullOrWhiteSpace(packet.Name) ? packet.Version! : packet.Name!;
        if (name.EndsWith(Format.Zip.ToExtension(), StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - Format.Zip.ToExtension().Length);
        if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." ||
            name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new JsonException("The push packet contains an invalid package name.");

        if (!packet.RecordId.HasValue || packet.RecordId == 0)
            GeneralTracer.Warn("PushDownloadSource: no push record ID; update status cannot be reported.");

        _asset = new DownloadAsset(
            Name: name,
            Url: packet.Url!,
            Size: packet.Size,
            SHA256: packet.Hash,
            Version: packet.Version!,
            PackageType: packet.PackageType,
            IsForcibly: packet.IsForcibly == true,
            IsFreeze: packet.IsFreeze == true,
            RecordId: packet.RecordId ?? 0,
            AppType: packet.AppType);
    }

    public Task<DownloadSourceResult> ListAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new DownloadSourceResult
        {
            Assets = new[] { _asset },
            HasMainUpdate = _asset.AppType == (int)AppType.Client,
            HasUpgradeUpdate = _asset.AppType == (int)AppType.Upgrade
        });
    }
}
