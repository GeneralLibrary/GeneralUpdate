using System.Diagnostics;
using System.IO;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>macOS update strategy — follows Linux conventions for file operations.</summary>
public class MacStrategy : AbstractStrategy
{
    public override void Execute()
    {
        GeneralTracer.Info("MacStrategy: executing macOS update");
        StartApp();
    }

    public override void StartApp()
    {
        var mainApp = Path.Combine(_configinfo.InstallPath, _configinfo.MainAppName);
        if (File.Exists(mainApp))
        {
            GeneralTracer.Info($"MacStrategy: starting {mainApp}");
            Process.Start(mainApp);
        }
    }

    public override void Create(GlobalConfigInfo configInfo) => _configinfo = configInfo;
}
