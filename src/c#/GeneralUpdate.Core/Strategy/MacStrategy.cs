using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

/// <summary>macOS update strategy — follows Linux conventions with platform-specific paths.</summary>
public class MacStrategy : AbstractStrategy
{
    public override void Execute()
    {
        GeneralTracer.Info("MacStrategy: executing macOS update");
        var mainApp = Path.Combine(
            _configinfo.InstallPath ?? string.Empty,
            _configinfo.MainAppName ?? string.Empty);

        if (!string.IsNullOrEmpty(_configinfo.MainAppName) && File.Exists(mainApp))
            StartApp();
    }

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
}
