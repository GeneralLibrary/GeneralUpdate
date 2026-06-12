using System;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// macOS platform-specific update strategy.
/// Implements the update flow for the macOS operating system, including pipeline building,
/// hash verification, decompression, and patch application.
/// </summary>
/// <remarks>
/// <para>
/// This class inherits from <c>AbstractStrategy</c> and provides complete update lifecycle management for the macOS environment.
/// Its pipeline flow is similar to the Linux strategy, but additionally verifies file existence in <c>StartAppAsync</c>.
/// </para>
/// <para>
/// Core flow:
/// <list type="number">
///   <item><c>BuildPipeline</c> — Builds the middleware pipeline, executing hash verification, decompression,
///        and (optionally) patch application in order.</item>
///   <item><c>ExecuteAsync</c> — Executes the base class pipeline flow and logs start information
///        (uses <c>ConfigureAwait(false)</c> to avoid context-switch deadlocks).</item>
///   <item><c>Create</c> — Directly stores configuration information in the internal field.</item>
///   <item><c>StartAppAsync</c> — Starts the updated main application, then exits the current updater process.</item>
/// </list>
/// </para>
/// </remarks>
public class MacStrategy : AbstractStrategy
{
    /// <summary>
    /// Asynchronously executes the main update strategy flow.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method first logs the execution start information, then calls the base class's <c>ExecuteAsync</c> method
    /// to execute the actual pipeline flow. Uses <c>ConfigureAwait(false)</c> to avoid deadlocks in UI contexts.
    /// </remarks>
    public override async Task ExecuteAsync()
    {
        GeneralTracer.Info("MacStrategy: executing pipeline");
        await base.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously starts the updated main application, then exits the current updater process.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method is called after the update process completes. The differences from the Windows/Linux strategies are:
    /// </para>
    /// <list type="number">
    ///   <item>Uses <c>LaunchAppName</c> to get the main application name; throws if not set.</item>
    ///   <item>Calls <c>ResolveAppPath</c> to resolve the application path.</item>
    ///   <item>Verifies file existence using <c>File.Exists</c> before launching (macOS-specific).</item>
    ///   <item>Starts the main application using <c>Process.Start</c>.</item>
    ///   <item>Disposes the <c>GeneralTracer</c> resources and calls <c>GracefulExit.CurrentProcessAsync()</c> to exit the updater process.</item>
    /// </list>
    /// <para>
    /// Any exception is caught and dispatched as an <c>ExceptionEventArgs</c> event via <c>EventManager</c>.
    /// </para>
    /// </remarks>
    public override async Task StartAppAsync()
    {
        var launchedOrCompleted = false;
        try
        {
            var appName = LaunchAppName ?? throw new InvalidOperationException("LaunchAppName must be set before calling StartAppAsync.");
            var mainApp = ResolveAppPath(appName, UseUpdatePath);

            if (!string.IsNullOrEmpty(mainApp) && File.Exists(mainApp))
            {
                GeneralTracer.Info($"MacStrategy.StartApp: launching app={mainApp}");
                using var process = System.Diagnostics.Process.Start(mainApp);
                if (process == null || process.HasExited)
                    throw new InvalidOperationException($"Failed to start application: {mainApp}");
                GeneralTracer.Info($"MacStrategy.StartApp: app launched successfully (PID: {process.Id}).");
            }
            else
            {
                GeneralTracer.Info("MacStrategy.StartApp: no app to launch (app path not found or empty).");
            }

            launchedOrCompleted = true;
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StartApp method in MacStrategy threw an exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));

            // If the main app was already launched, still need to exit the updater.
            if (!launchedOrCompleted) return;
        }

        // Terminate the updater when the main app was launched OR when there was
        // no app to launch at all (both are normal terminal states for the updater).
        GeneralTracer.Info("MacStrategy.StartApp: releasing tracer and terminating updater process.");
        GeneralTracer.Dispose();
        await GracefulExit.CurrentProcessAsync();
    }

    /// <summary>
    /// Creates and initializes the strategy instance using global configuration information.
    /// </summary>
    /// <param name="configInfo">Global configuration information containing settings such as install path, application name, and version number.</param>
    /// <remarks>
    /// The MacStrategy's <c>Create</c> implementation directly stores the configuration information in the internal field <c>_configinfo</c>,
    /// without calling the base class implementation. This provides a more lightweight initialization approach.
    /// </remarks>
    public override void Create(UpdateContext configInfo) => _configinfo = configInfo;

    /// <summary>
    /// Builds the macOS platform update middleware pipeline.
    /// </summary>
    /// <param name="context">The pipeline context containing version and patch information.</param>
    /// <returns>A configured <c>PipelineBuilder</c> instance containing hash verification, decompression,
    /// and (optionally) patch middleware.</returns>
    /// <remarks>
    /// <para>
    /// The pipeline assembles middleware in the following order:
    /// </para>
    /// <list type="number">
    ///   <item><c>HashMiddleware</c> — Computes and verifies file hashes to ensure data integrity.</item>
    ///   <item><c>CompressMiddleware</c> — Decompresses the downloaded update package.</item>
    ///   <item><c>PatchMiddleware</c> — (Optional) Applies binary patches. Only enabled when <c>_configinfo.PatchEnabled</c> is true.</item>
    /// </list>
    /// </remarks>
    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        GeneralTracer.Info($"MacStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}");
        var builder = new PipelineBuilder(context)
            .UseMiddleware<HashMiddleware>()
            .UseMiddleware<CompressMiddleware>()
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
        return builder;
    }
}
