using System;
using System.Collections.Generic;

namespace MyApp.Extensions.SDK
{
    /// <summary>
    /// Represents an event that triggers the activation of an extension, similar to VS Code activation events.
    /// </summary>
    public class ExtensionActivationEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier of the activation event.
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// Gets or sets the type of activation event (e.g., "onCommand", "onLanguage", "onView", "onStartup", "onFileSystem").
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets the pattern or condition that triggers the event.
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the activation event.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the source of the event.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Predefined activation event: Extension is activated on application startup.
        /// </summary>
        /// <returns>An activation event for startup.</returns>
        public static ExtensionActivationEvent OnStartup()
        {
            return new ExtensionActivationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "onStartup",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Predefined activation event: Extension is activated when a specific command is invoked.
        /// </summary>
        /// <param name="commandId">The command identifier.</param>
        /// <returns>An activation event for a command.</returns>
        public static ExtensionActivationEvent OnCommand(string commandId)
        {
            return new ExtensionActivationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "onCommand",
                Pattern = commandId,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Predefined activation event: Extension is activated when a file with a specific language is opened.
        /// </summary>
        /// <param name="languageId">The language identifier (e.g., "csharp", "javascript").</param>
        /// <returns>An activation event for a language.</returns>
        public static ExtensionActivationEvent OnLanguage(string languageId)
        {
            return new ExtensionActivationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "onLanguage",
                Pattern = languageId,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Predefined activation event: Extension is activated when a specific view is opened.
        /// </summary>
        /// <param name="viewId">The view identifier.</param>
        /// <returns>An activation event for a view.</returns>
        public static ExtensionActivationEvent OnView(string viewId)
        {
            return new ExtensionActivationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "onView",
                Pattern = viewId,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Predefined activation event: Extension is activated when a file system matching a pattern is accessed.
        /// </summary>
        /// <param name="filePattern">The file pattern (e.g., "*.txt", "**/*.cs").</param>
        /// <returns>An activation event for a file system pattern.</returns>
        public static ExtensionActivationEvent OnFileSystem(string filePattern)
        {
            return new ExtensionActivationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "onFileSystem",
                Pattern = filePattern,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
