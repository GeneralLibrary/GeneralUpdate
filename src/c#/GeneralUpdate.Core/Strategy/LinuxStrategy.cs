using System;
using System.Diagnostics;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

public class LinuxStrategy : AbstractStrategy
{
    protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}, driveEnabled={_configinfo.DriveEnabled}");
        var context = base.CreatePipelineContext(version, patchPath);
        
        // Driver middleware (Linux-specific)
        if (_configinfo.DriveEnabled == true)
        {
            context.Add("DriverDirectory", _configinfo.DriverDirectory);
            GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.CreatePipelineContext: driver update enabled, DriverDirectory={_configinfo.DriverDirectory}");
        }
        
        return context;
    }

    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}, DriveEnabled={_configinfo.DriveEnabled}");
        var builder = new PipelineBuilder(context)
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
            .UseMiddleware<CompressMiddleware>()
            .UseMiddleware<HashMiddleware>();
        // DrivelutionMiddleware: add GeneralUpdate.Drivelution project reference to enable
        return builder;
    }

    public override void Execute()
    {
        ExecuteAsync().Wait();
    }

    protected override void OnExecuteComplete()
    {
        GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.OnExecuteComplete: all versions processed, starting application.");
        StartApp();
    }

    public override void StartApp()
    {
        try
        {
            var mainAppPath = CheckPath(_configinfo.InstallPath, _configinfo.MainAppName);
            if (string.IsNullOrEmpty(mainAppPath))
                throw new Exception($"Can't find the app {mainAppPath}!");

            GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.StartApp: launching main app={mainAppPath}");
            Process.Start(mainAppPath);
            GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.StartApp: main app launched successfully.");
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
            GracefulExit.CurrentProcessAsync().GetAwaiter().GetResult();
        }
    }

}
