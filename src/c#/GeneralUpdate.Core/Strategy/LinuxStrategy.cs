using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Linux platform-specific update strategy.
/// Implements the update flow for the Linux operating system, including pipeline building,
/// hash verification, decompression, and patch application.
/// </summary>
/// <remarks>
/// <para>
/// This class inherits from <c>AbstractStrategy</c> and provides complete update lifecycle management for the Linux environment.
/// </para>
/// <para>
/// Core flow:
/// <list type="number">
///   <item><c>BuildPipeline</c> — Builds the middleware pipeline, executing hash verification, decompression,
///        and (optionally) patch application in order.</item>
///   <item><c>CreatePipelineContext</c> — Creates the pipeline context containing version information and patch path.</item>
///   <item><c>StartAppAsync</c> — Starts the updated main application using <c>Process.Start</c>,
///        then releases the tracer and gracefully exits the current updater process.</item>
/// </list>
/// </para>
/// <para>
/// Unlike the Windows strategy, the Linux strategy does not include Bowl helper process launch logic.
/// The patching feature is controlled by the <c>PatchEnabled</c> configuration.
/// </para>
/// </remarks>
public class LinuxStrategy : AbstractStrategy
{
    /// <summary>
    /// Creates the pipeline context containing target version information and patch path.
    /// </summary>
    /// <param name="version">The target version information.</param>
    /// <param name="patchPath">The path to the patch files.</param>
    /// <returns>A <c>PipelineContext</c> instance containing version information and patch path.</returns>
    /// <remarks>
    /// This method is called by the pipeline execution flow in <c>AbstractStrategy</c>.
    /// It logs the version number and patch path, then calls the base class's <c>CreatePipelineContext</c> method to create the context object.
    /// </remarks>
    protected override PipelineContext CreatePipelineContext(VersionEntry version, string patchPath)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}");
        var context = base.CreatePipelineContext(version, patchPath);
        return context;
    }

    /// <summary>
    /// Builds the Linux platform update middleware pipeline.
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
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}");
        var builder = new PipelineBuilder(context)
            .UseMiddleware<HashMiddleware>()
            .UseMiddleware<CompressMiddleware>()
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
        return builder;
    }

    /// <summary>
    /// Asynchronously starts the updated main application, then exits the current updater process.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method is called after the update process completes. The execution steps are as follows:
    /// </para>
    /// <list type="number">
    ///   <item>Uses the <c>LaunchAppName</c> property to get the main application name; throws if not set.</item>
    ///   <item>Calls <c>ResolveAppPath</c> to resolve the full path of the application.</item>
    ///   <item>Starts the main application using <c>Process.Start</c>.</item>
    ///   <item>Disposes the <c>GeneralTracer</c> resources.</item>
    ///   <item>Calls <c>GracefulExit.CurrentProcessAsync()</c> to gracefully terminate the updater process.</item>
    /// </list>
    /// <para>
    /// Note: The Linux strategy does not support the Bowl helper process when starting the application.
    /// Any exception is caught and dispatched as an <c>ExceptionEventArgs</c> event via <c>EventManager</c>.
    /// </para>
    /// </remarks>
    public override async Task StartAppAsync()
    {
        try
        {
            var appName = LaunchAppName ?? throw new InvalidOperationException("LaunchAppName must be set before calling StartAppAsync.");
            var appPath = ResolveAppPath(appName, UseUpdatePath);
            if (string.IsNullOrEmpty(appPath))
                throw new Exception($"Can't find the app {appName}!");

            GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.StartApp: launching app={appPath}");
            Process.Start(appPath);
            GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.StartApp: app launched successfully.");
        }
        catch (Exception e)
        {
            GeneralTracer.Error(
                "The StartApp method in the GeneralUpdate.Core.LinuxStrategy class throws an exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.StartApp: releasing tracer and terminating updater process.");
            GeneralTracer.Dispose();
            await GracefulExit.CurrentProcessAsync();
        }
    }
}
