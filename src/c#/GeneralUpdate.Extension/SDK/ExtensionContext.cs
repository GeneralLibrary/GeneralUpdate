using System;
using System.Collections.Generic;
using System.IO;

namespace MyApp.Extensions.SDK
{
    /// <summary>
    /// Default implementation of IExtensionContext providing runtime context for extensions.
    /// </summary>
    public class ExtensionContext : IExtensionContext
    {
        private readonly Dictionary<string, object> _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionContext"/> class.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="version">The version of the extension.</param>
        /// <param name="extensionPath">The path to the extension's installation directory.</param>
        /// <param name="api">The host application API.</param>
        public ExtensionContext(string extensionId, string version, string extensionPath, IExtensionAPI api)
        {
            ExtensionId = extensionId ?? throw new ArgumentNullException(nameof(extensionId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            ExtensionPath = extensionPath ?? throw new ArgumentNullException(nameof(extensionPath));
            API = api ?? throw new ArgumentNullException(nameof(api));

            _state = new Dictionary<string, object>();
            Configuration = new Dictionary<string, object>();
            Environment = new Dictionary<string, string>();

            // Set up storage paths
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            StoragePath = Path.Combine(appData, "Extensions", extensionId);
            GlobalStoragePath = Path.Combine(appData, "Extensions", "Global");

            // Create storage directories
            if (!Directory.Exists(StoragePath))
                Directory.CreateDirectory(StoragePath);
            if (!Directory.Exists(GlobalStoragePath))
                Directory.CreateDirectory(GlobalStoragePath);

            // Create logger
            Logger = new ExtensionLogger(extensionId);
        }

        /// <summary>
        /// Gets the unique identifier of the extension.
        /// </summary>
        public string ExtensionId { get; }

        /// <summary>
        /// Gets the version of the extension.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the path to the extension's installation directory.
        /// </summary>
        public string ExtensionPath { get; }

        /// <summary>
        /// Gets the path to the extension's storage directory for user data.
        /// </summary>
        public string StoragePath { get; }

        /// <summary>
        /// Gets the path to the extension's global storage directory.
        /// </summary>
        public string GlobalStoragePath { get; }

        /// <summary>
        /// Gets the configuration settings for the extension.
        /// </summary>
        public Dictionary<string, object> Configuration { get; }

        /// <summary>
        /// Gets the environment variables available to the extension.
        /// </summary>
        public Dictionary<string, string> Environment { get; }

        /// <summary>
        /// Gets the host application API.
        /// </summary>
        public IExtensionAPI API { get; }

        /// <summary>
        /// Gets the logger for the extension.
        /// </summary>
        public IExtensionLogger Logger { get; }

        /// <summary>
        /// Saves a value to the extension's storage.
        /// </summary>
        /// <param name="key">The key to store the value under.</param>
        /// <param name="value">The value to store.</param>
        public void SaveState(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            _state[key] = value;
        }

        /// <summary>
        /// Retrieves a value from the extension's storage.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve.</typeparam>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <returns>The stored value, or default if not found.</returns>
        public T GetState<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return default;

            if (_state.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return default;
        }
    }

    /// <summary>
    /// Default implementation of IExtensionLogger for logging.
    /// </summary>
    public class ExtensionLogger : IExtensionLogger
    {
        private readonly string _extensionId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionLogger"/> class.
        /// </summary>
        /// <param name="extensionId">The extension identifier for logging context.</param>
        public ExtensionLogger(string extensionId)
        {
            _extensionId = extensionId;
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Info(string message)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [INFO] [{_extensionId}] {message}");
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Warn(string message)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [WARN] [{_extensionId}] {message}");
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Error(string message)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [ERROR] [{_extensionId}] {message}");
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Debug(string message)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [DEBUG] [{_extensionId}] {message}");
        }
    }
}
