using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

public class LinuxStrategy : AbstractStrategy
{
    protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}");
        var context = base.CreatePipelineContext(version, patchPath);
        return context;
    }

    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}");
        var builder = new PipelineBuilder(context)
            .UseMiddleware<HashMiddleware>()
            .UseMiddleware<CompressMiddleware>()
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
        return builder;
    }

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
