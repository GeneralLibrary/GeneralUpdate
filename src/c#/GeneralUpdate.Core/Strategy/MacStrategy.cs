using System;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy;

/// <summary>macOS update strategy — follows Linux conventions.</summary>
public class MacStrategy : AbstractStrategy
{
    public override async Task ExecuteAsync()
    {
        GeneralTracer.Info("MacStrategy: executing pipeline");
        await base.ExecuteAsync().ConfigureAwait(false);
    }

    public override async Task StartAppAsync()
    {
        try
        {
            var mainApp = Path.Combine(
                _configinfo.InstallPath ?? string.Empty,
                _configinfo.MainAppName ?? string.Empty);

            if (!string.IsNullOrEmpty(_configinfo.MainAppName) && File.Exists(mainApp))
            {
                GeneralTracer.Info($"MacStrategy: starting {mainApp}");
                System.Diagnostics.Process.Start(mainApp);
            }
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StartApp method in MacStrategy threw an exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            GeneralTracer.Info("MacStrategy.StartApp: releasing tracer and terminating updater process.");
            GeneralTracer.Dispose();
            await GracefulExit.CurrentProcessAsync();
        }
    }

    public override void Create(GlobalConfigInfo configInfo) => _configinfo = configInfo;

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
