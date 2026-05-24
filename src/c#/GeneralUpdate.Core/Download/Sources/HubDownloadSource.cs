using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Hubs;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Download.Sources;

/// <summary>
/// SignalR Hub download source — receives update push notifications
/// and converts them to DownloadAssets for the orchestrator.
/// </summary>
public class HubDownloadSource : IDownloadSource, IDisposable
{
    private readonly string _hubUrl;
    private readonly string? _token;
    private readonly string? _appKey;
    private readonly ConcurrentBag<DownloadAsset> _assets = new();
    private readonly TaskCompletionSource<bool> _initializedTcs = new();
    private UpgradeHubService? _hub;

    public HubDownloadSource(string hubUrl, string? token = null, string? appKey = null)
    {
        _hubUrl = hubUrl;
        _token = token;
        _appKey = appKey;
    }

    /// <summary>Start listening to the SignalR hub.</summary>
    public async Task StartAsync()
    {
        try
        {
            _hub = new UpgradeHubService(_hubUrl, _token, _appKey);
            _hub.AddListenerReceive(OnReceiveMessage);
            await _hub.StartAsync().ConfigureAwait(false);
            _initializedTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _initializedTcs.TrySetException(ex);
        }
    }

    private void OnReceiveMessage(string json)
    {
        try
        {
            var packet = System.Text.Json.JsonSerializer.Deserialize<PacketDTO>(json);
            if (packet != null)
            {
                var asset = DownloadPlanBuilder.MapToAsset(packet);
                _assets.Add(asset);
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"HubDownloadSource: failed to parse message: {ex.Message}");
        }
    }

    /// <summary>Get accumulated download assets from hub pushes.</summary>
    public async Task<IReadOnlyList<DownloadAsset>> ListAsync(CancellationToken token = default)
    {
        // Wait for hub initialization
        await _initializedTcs.Task.ConfigureAwait(false);

        // Wait a brief moment for any pending messages to arrive
        try { await Task.Delay(100, token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        return _assets.ToList();
    }

    public void Dispose()
    {
        _hub?.DisposeAsync().GetAwaiter().GetResult();
    }
}
