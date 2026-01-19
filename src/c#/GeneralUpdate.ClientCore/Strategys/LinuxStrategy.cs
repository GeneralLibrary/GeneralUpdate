using System;
using System.Diagnostics;
using System.IO;
using GeneralUpdate.ClientCore.Pipeline;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.ClientCore.Strategys;

/// <summary>
/// Update policy based on the linux platform.
/// </summary>
public class LinuxStrategy : AbstractStrategy
{
    protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
    {
        var context = base.CreatePipelineContext(version, patchPath);
        
        // Add ClientCore-specific context items (blacklists for Linux)
        context.Add("BlackFiles", _configinfo.BlackFiles);
        context.Add("BlackFileFormats", _configinfo.BlackFormats);
        context.Add("SkipDirectorys", _configinfo.SkipDirectorys);
        
        return context;
    }

    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        return new PipelineBuilder(context)
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
            .UseMiddleware<CompressMiddleware>()
            .UseMiddleware<HashMiddleware>();
    }

    public override void StartApp()
    {
        try
        {
            Environments.SetEnvironmentVariable("ProcessInfo", _configinfo.ProcessInfo);
            var appPath = Path.Combine(_configinfo.InstallPath, _configinfo.AppName);
            if (File.Exists(appPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = appPath
                });
            }
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StartApp method in the GeneralUpdate.ClientCore.LinuxStrategy class throws an exception." , e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            GeneralTracer.Dispose();
            Process.GetCurrentProcess().Kill();
        }
    }
}