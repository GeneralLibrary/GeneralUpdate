using System;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Internal.JsonContext;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralUpdate.ClientCore.Hubs;

/// <summary>
/// Upgrade the push notification service.
/// </summary>
/// <param name="url">Subscription address, for example: http://127.0.0.1/UpgradeHub</param>
/// <param name="token">ID4 authentication token string.</param>
/// <param name="args">Parameters to be sent to the server upon connection (recommended as a JSON string).</param>
public class UpgradeHubService : IUpgradeHubService
{
    private const string Onlineflag = "Online";
    private const string ReceiveMessageflag = "ReceiveMessage";
    private HubConnection? _connection;

    public UpgradeHubService(string url, string? token = null, string? appkey = null)
        => _connection = BuildHubConnection(url, token, appkey);

    private HubConnection BuildHubConnection(string url, string? token = null, string? appkey = null)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl(url, config =>
            {
                if (!string.IsNullOrWhiteSpace(token))
                    config.AccessTokenProvider = () => Task.FromResult(token);

                if (!string.IsNullOrWhiteSpace(appkey))
                    config.Headers.Add("appkey", appkey);
            }).WithAutomaticReconnect(new RandomRetryPolicy());
        builder.Services.Configure<JsonHubProtocolOptions>(o =>
        {
            o.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, PacketJsonContext.Default);
        });
        return builder.Build();
    }

    public void AddListenerReceive(Action<string> receiveMessageCallback)
        => _connection?.On(ReceiveMessageflag, receiveMessageCallback);

    public void AddListenerOnline(Action<string> onlineMessageCallback)
        => _connection?.On(Onlineflag, onlineMessageCallback);

    public void AddListenerReconnected(Func<string?, Task>? reconnectedCallback)
        => _connection!.Reconnected += reconnectedCallback;

    public void AddListenerClosed(Func<Exception?, Task> closeCallback)
        => _connection!.Closed += closeCallback;

    public async Task StartAsync()
    {
        try
        {
            await _connection!.StartAsync();
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StartAsync method in the UpgradeHubService class throws an exception." , e);
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await _connection!.StopAsync();
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StopAsync method in the UpgradeHubService class throws an exception." , e);
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _connection!.DisposeAsync();
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The DisposeAsync method in the UpgradeHubService class throws an exception." , e);
        }
    }
}