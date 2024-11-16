using System;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore.Hubs;

public interface IUpgradeHubService
{
        /// <summary>
    /// Add a listener to receive upgrade information pushed from the server.
    /// </summary>
    /// <param name="receiveMessageCallback">string : group name , string :  received message content.</param>
    public void AddReceiveListener(Action<string, string> receiveMessageCallback);

    /// <summary>
    /// Add a listener to receive online and offline notifications.
    /// </summary>
    /// <param name="onlineMessageCallback">string : Offline or online information.</param>
    public void AddOnlineListener(Action<string> onlineMessageCallback);

    /// <summary>
    /// Add a listener to receive reconnection notifications.
    /// </summary>
    /// <param name="reconnectedCallback">string? : Reconnection information.</param>
    public void AddReconnectedListener(Func<string?, Task>? reconnectedCallback);

    /// <summary>
    /// Add a listener to receive disconnection notifications.
    /// </summary>
    /// <param name="closeCallback">Exception? : Offline exception information.</param>
    public void AddClosedListener(Func<Exception?, Task> closeCallback);
    
    /// <summary>
    /// Start subscribing to upgrade push notifications, and the content of the notifications should be agreed upon independently (it is recommended to use JSON data format).
    /// </summary>
    public Task StartAsync();
    
    /// <summary>
    /// When closing the connection, any ongoing message processing will be completed, but no new messages will be accepted.
    /// This should be called before the application closes or goes to sleep, so it can reconnect when it resumes next time.
    /// </summary>
    public Task StopAsync();

    /// <summary>
    /// The Hub instance will be completely disposed of and cannot be used for reconnection.
    /// </summary>
    public Task DisposeAsync();
}