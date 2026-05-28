using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Hooks;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// Defines the contract interface for update strategies.
    /// All platform-specific update strategies (Windows, Linux, macOS, and Oss) must implement this interface
    /// to provide complete update lifecycle management.
    /// </summary>
    /// <remarks>
    /// <para>The update lifecycle executes in the following order:</para>
    /// <list type="number">
    ///   <item><see cref="Create"/> — Initializes the strategy with global configuration.</item>
    ///   <item><see cref="ExecuteAsync"/> — Executes the update process (hash verification, decompression, patch application, etc.).</item>
    ///   <item><see cref="StartAppAsync"/> — Starts the updated main application and exits the updater process.</item>
    /// </list>
    /// <para>
    /// To extend with a custom strategy, implement this interface.
    /// Platform-specific strategies (such as <c>WindowsStrategy</c>, <c>LinuxStrategy</c>, <c>MacStrategy</c>)
    /// typically inherit from the <c>AbstractStrategy</c> base class, which provides a standard pipeline execution implementation.
    /// </para>
    /// </remarks>
    public interface IStrategy
    {
        /// <summary>
        /// Gets or sets the update lifecycle hooks, used to execute custom callbacks before and after the update.
        /// </summary>
        /// <remarks>
        /// <see cref="IUpdateHooks"/> provides multiple overridable methods, including:
        /// <see cref="IUpdateHooks.OnBeforeUpdateAsync"/>, <see cref="IUpdateHooks.OnAfterUpdateAsync"/>,
        /// <see cref="IUpdateHooks.OnBeforeStartAppAsync"/>, and error-handling callbacks.
        /// The default implementation uses <c>NoOpUpdateHooks</c> (no operation).
        /// </remarks>
        IUpdateHooks Hooks { get; set; }

        /// <summary>
        /// Gets or sets the update status reporter, used to report update progress and results to the server or event system.
        /// </summary>
        /// <remarks>
        /// The reporter can be used to report update status (updating, success, failure) to a remote service
        /// (such as GeneralSpacestation) or trigger local events via <c>EventManager</c>.
        /// The default implementation uses <c>NoOpUpdateReporter</c> (no operation).
        /// </remarks>
        IUpdateReporter Reporter { get; set; }

        /// <summary>
        /// Asynchronously executes the main update strategy workflow.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// This method is the core of the update process. A typical execution flow includes:
        /// <list type="bullet">
        ///   <item>Downloading update packages or retrieving version information from a configuration source.</item>
        ///   <item>Executing hash verification, decompression, and patch application through the middleware pipeline.</item>
        ///   <item>Triggering pre- and post-update lifecycle hooks.</item>
        ///   <item>Reporting update status (start, progress, completion, or failure).</item>
        /// </list>
        /// </remarks>
        Task ExecuteAsync();

        /// <summary>
        /// Asynchronously starts the updated main application, then exits the current updater process.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// This method is called after the update process completes. It will:
        /// </para>
        /// <list type="number">
        ///   <item>Resolve the executable path of the main application.</item>
        ///   <item>Start the main application using <c>Process.Start</c>.</item>
        ///   <item>Call <c>GracefulExit.CurrentProcessAsync()</c> to gracefully terminate the updater process.</item>
        /// </list>
        /// <para>
        /// For the Windows strategy, if a <c>Bowl</c> helper process is configured, it will also be started.
        /// </para>
        /// </remarks>
        Task StartAppAsync();

        /// <summary>
        /// Creates and initializes the strategy instance using global configuration information.
        /// </summary>
        /// <param name="parameter">Global configuration information containing settings such as install path, application name, version number, etc.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is null.</exception>
        /// <remarks>
        /// This method must be called before <see cref="ExecuteAsync"/> to provide all configuration parameters required for strategy execution.
        /// Configuration information includes key settings such as <c>InstallPath</c>, <c>MainAppName</c>, <c>UpdateAppName</c>, <c>ClientVersion</c>,
        /// and <c>PatchEnabled</c>.
        /// </remarks>
        void Create(GlobalConfigInfo parameter);
    }
}