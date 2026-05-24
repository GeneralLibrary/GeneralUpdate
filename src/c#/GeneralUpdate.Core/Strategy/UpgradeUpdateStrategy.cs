using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Event;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// Upgrade-side update strategy. Receives process info from the client side,
/// applies updates via the pipeline, and starts the main application.
/// </summary>
/// <remarks>
/// This is the AppType.UpgradeApp role strategy. It composes an OS-specific
/// strategy for platform operations (Windows/Linux/Mac).
///
/// <b>Design:</b> Upgrade does NOT validate versions or download packages.
/// The client has already validated versions, downloaded all packages, and
/// passed the results via ProcessInfo. Upgrade only applies updates and
/// starts the main application — zero network.
/// </remarks>
public class UpgradeUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private IStrategy? _osStrategy;

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _osStrategy = ResolveOsStrategy();
    }

    public async Task ExecuteAsync()
    {
        if (_configInfo == null) throw new InvalidOperationException("UpgradeUpdateStrategy not configured.");

        try
        {
            GeneralTracer.Debug("UpgradeUpdateStrategy.ExecuteAsync start.");

            ApplyRuntimeOptions();
            _osStrategy!.Create(_configInfo);

            // Apply updates via OS-specific pipeline (Hash → Compress → Patch)
            if (_configInfo.UpdateVersions?.Count > 0)
            {
                GeneralTracer.Info($"UpgradeUpdateStrategy: applying {_configInfo.UpdateVersions.Count} update(s).");
                await _osStrategy.ExecuteAsync();
            }
            else
            {
                GeneralTracer.Info("UpgradeUpdateStrategy: no updates to apply, starting application directly.");
            }

            _osStrategy.StartApp();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("UpgradeUpdateStrategy.ExecuteAsync failed.", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, ex.Message));
        }
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
    }

    public void StartApp()
    {
        _osStrategy?.StartApp();
    }

    #region Helpers

    private static IStrategy ResolveOsStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacStrategy();
        throw new PlatformNotSupportedException("The current operating system is not supported!");
    }

    private void ApplyRuntimeOptions()
    {
        _configInfo!.Encoding = Encoding.UTF8;
        _configInfo.Format = Format.ZIP;
    }

    #endregion
}
