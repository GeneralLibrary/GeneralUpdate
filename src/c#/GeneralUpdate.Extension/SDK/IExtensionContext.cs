using System;
using System.Collections.Generic;

namespace MyApp.Extensions.SDK
{
    /// <summary>
    /// Provides runtime context for an extension, including access to extension-specific resources and configuration.
    /// </summary>
    public interface IExtensionContext
    {
        /// <summary>
        /// Gets the unique identifier of the extension.
        /// </summary>
        string ExtensionId { get; }

        /// <summary>
        /// Gets the version of the extension.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the path to the extension's installation directory.
        /// </summary>
        string ExtensionPath { get; }

        /// <summary>
        /// Gets the path to the extension's storage directory for user data.
        /// </summary>
        string StoragePath { get; }

        /// <summary>
        /// Gets the path to the extension's global storage directory.
        /// </summary>
        string GlobalStoragePath { get; }

        /// <summary>
        /// Gets the configuration settings for the extension.
        /// </summary>
        Dictionary<string, object> Configuration { get; }

        /// <summary>
        /// Gets the environment variables available to the extension.
        /// </summary>
        Dictionary<string, string> Environment { get; }

        /// <summary>
        /// Gets the host application API.
        /// </summary>
        IExtensionAPI API { get; }

        /// <summary>
        /// Gets the logger for the extension.
        /// </summary>
        IExtensionLogger Logger { get; }

        /// <summary>
        /// Saves a value to the extension's storage.
        /// </summary>
        /// <param name="key">The key to store the value under.</param>
        /// <param name="value">The value to store.</param>
        void SaveState(string key, object value);

        /// <summary>
        /// Retrieves a value from the extension's storage.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve.</typeparam>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <returns>The stored value, or default if not found.</returns>
        T GetState<T>(string key);
    }

    /// <summary>
    /// Provides logging functionality for extensions.
    /// </summary>
    public interface IExtensionLogger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Info(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Warn(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Error(string message);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Debug(string message);
    }
}
