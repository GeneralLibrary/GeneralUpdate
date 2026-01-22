using System;
using System.Threading.Tasks;

namespace MyApp.Extensions.SDK
{
    /// <summary>
    /// Provides APIs for extensions to interact with the host application.
    /// </summary>
    public interface IExtensionAPI
    {
        /// <summary>
        /// Gets the version of the host application.
        /// </summary>
        string HostVersion { get; }

        /// <summary>
        /// Gets the name of the host application.
        /// </summary>
        string HostName { get; }

        /// <summary>
        /// Executes a command in the host application.
        /// </summary>
        /// <param name="commandId">The unique identifier of the command to execute.</param>
        /// <param name="parameters">Optional parameters for the command.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command.</returns>
        Task<object> ExecuteCommandAsync(string commandId, params object[] parameters);

        /// <summary>
        /// Registers a command that can be invoked by the host application or other extensions.
        /// </summary>
        /// <param name="commandId">The unique identifier for the command.</param>
        /// <param name="handler">The handler to execute when the command is invoked.</param>
        void RegisterCommand(string commandId, Func<object[], object> handler);

        /// <summary>
        /// Unregisters a previously registered command.
        /// </summary>
        /// <param name="commandId">The unique identifier of the command to unregister.</param>
        void UnregisterCommand(string commandId);

        /// <summary>
        /// Shows a notification to the user.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="severity">The severity level (e.g., "Info", "Warning", "Error").</param>
        void ShowNotification(string message, string severity);

        /// <summary>
        /// Shows a message dialog to the user.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message to display.</param>
        /// <param name="buttons">The buttons to display (e.g., "OK", "YesNo").</param>
        /// <returns>A task that represents the asynchronous operation, containing the user's choice.</returns>
        Task<string> ShowDialogAsync(string title, string message, string buttons);

        /// <summary>
        /// Gets a service from the host application.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The service instance, or null if not available.</returns>
        T GetService<T>() where T : class;

        /// <summary>
        /// Subscribes to an event in the host application.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        /// <param name="handler">The handler to invoke when the event occurs.</param>
        void SubscribeToEvent(string eventName, Action<object> handler);

        /// <summary>
        /// Unsubscribes from an event in the host application.
        /// </summary>
        /// <param name="eventName">The name of the event to unsubscribe from.</param>
        /// <param name="handler">The handler to remove.</param>
        void UnsubscribeFromEvent(string eventName, Action<object> handler);

        /// <summary>
        /// Reads a resource from the host application.
        /// </summary>
        /// <param name="resourcePath">The path to the resource.</param>
        /// <returns>A task that represents the asynchronous operation, containing the resource content.</returns>
        Task<byte[]> ReadResourceAsync(string resourcePath);

        /// <summary>
        /// Opens a file or URL in the host application or default system handler.
        /// </summary>
        /// <param name="path">The file path or URL to open.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> OpenAsync(string path);
    }
}
