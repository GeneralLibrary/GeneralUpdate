using System;
using System.Threading.Tasks;

namespace MyApp.Extensions.Runtime
{
    /// <summary>
    /// Provides an interface for hosting and managing extension runtimes.
    /// </summary>
    public interface IRuntimeHost
    {
        /// <summary>
        /// Gets the type of runtime this host supports.
        /// </summary>
        RuntimeType RuntimeType { get; }

        /// <summary>
        /// Starts the runtime host.
        /// </summary>
        /// <param name="environmentInfo">The runtime environment information.</param>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> StartAsync(RuntimeEnvironmentInfo environmentInfo);

        /// <summary>
        /// Stops the runtime host.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation, indicating success or failure.</returns>
        Task<bool> StopAsync();

        /// <summary>
        /// Invokes a method or function in the runtime.
        /// </summary>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the invocation.</returns>
        Task<object> InvokeAsync(string methodName, params object[] parameters);

        /// <summary>
        /// Performs a health check on the runtime host.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation, indicating whether the runtime is healthy.</returns>
        Task<bool> HealthCheckAsync();

        /// <summary>
        /// Gets a value indicating whether the runtime host is currently running.
        /// </summary>
        bool IsRunning { get; }
    }
}
