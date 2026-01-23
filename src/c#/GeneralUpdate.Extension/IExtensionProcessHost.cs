using System;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides process isolation support for extensions.
    /// </summary>
    public interface IExtensionProcessHost
    {
        /// <summary>
        /// Starts an extension in an isolated process.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <param name="startupInfo">The startup information for the process.</param>
        /// <returns>A task that represents the asynchronous operation, containing the process ID.</returns>
        Task<int> StartProcessAsync(string extensionId, ProcessStartupInfo startupInfo);

        /// <summary>
        /// Stops an extension process.
        /// </summary>
        /// <param name="processId">The process ID to stop.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> StopProcessAsync(int processId);

        /// <summary>
        /// Gets a value indicating whether a process is currently running.
        /// </summary>
        /// <param name="processId">The process ID to check.</param>
        /// <returns>True if the process is running; otherwise, false.</returns>
        bool IsProcessRunning(int processId);

        /// <summary>
        /// Sends a message to an extension process.
        /// </summary>
        /// <param name="processId">The process ID to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response.</returns>
        Task<object> SendMessageAsync(int processId, object message);

        /// <summary>
        /// Monitors the health of an extension process.
        /// </summary>
        /// <param name="processId">The process ID to monitor.</param>
        /// <returns>A task that represents the asynchronous operation, containing the health status.</returns>
        Task<ProcessHealthStatus> MonitorHealthAsync(int processId);
    }

    /// <summary>
    /// Represents startup information for an extension process.
    /// </summary>
    public class ProcessStartupInfo
    {
        /// <summary>
        /// Gets or sets the executable path.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Gets or sets the working directory.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to redirect standard input/output.
        /// </summary>
        public bool RedirectStandardIO { get; set; }
    }

    /// <summary>
    /// Represents the health status of a process.
    /// </summary>
    public class ProcessHealthStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether the process is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage.
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Gets or sets the memory usage in MB.
        /// </summary>
        public long MemoryUsageMB { get; set; }

        /// <summary>
        /// Gets or sets any error messages.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
