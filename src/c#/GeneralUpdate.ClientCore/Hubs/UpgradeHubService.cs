using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace GeneralUpdate.ClientCore.Hubs;

/// <summary>
/// Upgrade the push notification service.
/// </summary>
/// <param name="url">Subscription address, for example: http://127.0.0.1/UpgradeHub</param>
/// <param name="token">ID4 authentication token string.</param>
/// <param name="args">Parameters to be sent to the server upon connection (recommended as a JSON string).</param>
public class UpgradeHubService(string url, string? token = null, string? appkey = null) : IUpgradeHubService
{
    private const string Onlineflag = "Online";
    private const string ReceiveMessageflag = "ReceiveMessage";
    
    private readonly HubConnection? _connection = new HubConnectionBuilder()
        .WithUrl(url, config =>
        {
            if (!string.IsNullOrWhiteSpace(token))
                config.AccessTokenProvider = () => Task.FromResult(token);
            
            if (!string.IsNullOrWhiteSpace(appkey))
                config.Headers.Add("appkey", appkey);
        })
        .WithAutomaticReconnect(new RandomRetryPolicy())
        .Build();
    
    public void AddListenerReceive(Action<string> receiveMessageCallback)
        => _connection?.On(ReceiveMessageflag, receiveMessageCallback);

    public void AddListenerOnline(Action<string> onlineMessageCallback)
        => _connection?.On(Onlineflag, onlineMessageCallback);

    public void AddListenerReconnected(Func<string?, Task>? reconnectedCallback)
        => _connection!.Reconnected += reconnectedCallback;

    public void AddListenerClosed(Func<Exception?, Task> closeCallback)
        => _connection!.Closed += closeCallback;
    
    public async Task StartAsync()
        => await _connection!.StartAsync();
    
    public async Task StopAsync()
        => await _connection!.StopAsync();

    public async Task DisposeAsync()
        => await _connection!.DisposeAsync();
}