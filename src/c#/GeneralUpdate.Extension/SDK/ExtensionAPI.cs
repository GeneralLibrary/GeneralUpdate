using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.Extensions.SDK
{
    /// <summary>
    /// Default implementation of IExtensionAPI for host application integration.
    /// </summary>
    public class ExtensionAPI : IExtensionAPI
    {
        private readonly Dictionary<string, Func<object[], object>> _commands;
        private readonly Dictionary<string, List<Action<object>>> _eventSubscribers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionAPI"/> class.
        /// </summary>
        /// <param name="hostName">The name of the host application.</param>
        /// <param name="hostVersion">The version of the host application.</param>
        public ExtensionAPI(string hostName, string hostVersion)
        {
            HostName = hostName ?? "Unknown Host";
            HostVersion = hostVersion ?? "1.0.0";
            _commands = new Dictionary<string, Func<object[], object>>();
            _eventSubscribers = new Dictionary<string, List<Action<object>>>();
        }

        /// <summary>
        /// Gets the version of the host application.
        /// </summary>
        public string HostVersion { get; }

        /// <summary>
        /// Gets the name of the host application.
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// Executes a command in the host application.
        /// </summary>
        /// <param name="commandId">The unique identifier of the command to execute.</param>
        /// <param name="parameters">Optional parameters for the command.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command.</returns>
        public Task<object> ExecuteCommandAsync(string commandId, params object[] parameters)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(commandId))
                    throw new ArgumentException("Command ID cannot be null or empty", nameof(commandId));

                if (_commands.TryGetValue(commandId, out var handler))
                {
                    var result = handler(parameters);
                    return Task.FromResult(result);
                }

                throw new InvalidOperationException($"Command not found: {commandId}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to execute command: {commandId}", ex);
            }
        }

        /// <summary>
        /// Registers a command that can be invoked by the host application or other extensions.
        /// </summary>
        /// <param name="commandId">The unique identifier for the command.</param>
        /// <param name="handler">The handler to execute when the command is invoked.</param>
        public void RegisterCommand(string commandId, Func<object[], object> handler)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID cannot be null or empty", nameof(commandId));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _commands[commandId] = handler;
        }

        /// <summary>
        /// Unregisters a previously registered command.
        /// </summary>
        /// <param name="commandId">The unique identifier of the command to unregister.</param>
        public void UnregisterCommand(string commandId)
        {
            if (!string.IsNullOrWhiteSpace(commandId))
            {
                _commands.Remove(commandId);
            }
        }

        /// <summary>
        /// Shows a notification to the user.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="severity">The severity level (e.g., "Info", "Warning", "Error").</param>
        public void ShowNotification(string message, string severity)
        {
            // In real implementation, would show UI notification
            Console.WriteLine($"[{severity}] {message}");
        }

        /// <summary>
        /// Shows a message dialog to the user.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message to display.</param>
        /// <param name="buttons">The buttons to display (e.g., "OK", "YesNo").</param>
        /// <returns>A task that represents the asynchronous operation, containing the user's choice.</returns>
        public Task<string> ShowDialogAsync(string title, string message, string buttons)
        {
            // In real implementation, would show UI dialog and await user response
            Console.WriteLine($"Dialog: {title} - {message} [{buttons}]");
            return Task.FromResult("OK");
        }

        /// <summary>
        /// Gets a service from the host application.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The service instance, or null if not available.</returns>
        public T GetService<T>() where T : class
        {
            // In real implementation, would use dependency injection container
            return null;
        }

        /// <summary>
        /// Subscribes to an event in the host application.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        /// <param name="handler">The handler to invoke when the event occurs.</param>
        public void SubscribeToEvent(string eventName, Action<object> handler)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (!_eventSubscribers.ContainsKey(eventName))
            {
                _eventSubscribers[eventName] = new List<Action<object>>();
            }

            _eventSubscribers[eventName].Add(handler);
        }

        /// <summary>
        /// Unsubscribes from an event in the host application.
        /// </summary>
        /// <param name="eventName">The name of the event to unsubscribe from.</param>
        /// <param name="handler">The handler to remove.</param>
        public void UnsubscribeFromEvent(string eventName, Action<object> handler)
        {
            if (string.IsNullOrWhiteSpace(eventName) || handler == null)
                return;

            if (_eventSubscribers.TryGetValue(eventName, out var handlers))
            {
                handlers.Remove(handler);
            }
        }

        /// <summary>
        /// Reads a resource from the host application.
        /// </summary>
        /// <param name="resourcePath">The path to the resource.</param>
        /// <returns>A task that represents the asynchronous operation, containing the resource content.</returns>
        public Task<byte[]> ReadResourceAsync(string resourcePath)
        {
            // In real implementation, would read from embedded resources
            return Task.FromResult(new byte[0]);
        }

        /// <summary>
        /// Opens a file or URL in the host application or default system handler.
        /// </summary>
        /// <param name="path">The file path or URL to open.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        public Task<bool> OpenAsync(string path)
        {
            try
            {
                // In real implementation, would use Process.Start or similar
                Console.WriteLine($"Opening: {path}");
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Triggers an event for all subscribers.
        /// </summary>
        /// <param name="eventName">The name of the event to trigger.</param>
        /// <param name="eventData">The event data.</param>
        public void TriggerEvent(string eventName, object eventData)
        {
            if (_eventSubscribers.TryGetValue(eventName, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler(eventData);
                    }
                    catch
                    {
                        // Swallow individual handler exceptions
                    }
                }
            }
        }
    }
}
