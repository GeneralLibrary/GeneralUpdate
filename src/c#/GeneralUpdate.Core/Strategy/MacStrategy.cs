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
    public override void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public override async Task ExecuteAsync()
    {
        GeneralTracer.Info("MacStrategy: executing pipeline");
        await base.ExecuteAsync().ConfigureAwait(false);
    }

    public override void StartApp()
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

    public override void Create(GlobalConfigInfo configInfo) => _configinfo = configInfo;

    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        var builder = new PipelineBuilder(context)
            .UseMiddleware<HashMiddleware>()
            .UseMiddleware<CompressMiddleware>()
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
        return builder;
    }
}
