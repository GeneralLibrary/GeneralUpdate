using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Pipeline;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.ClientCore.Strategys;

/// <summary>
/// Update policy based on the Windows platform.
/// </summary>
public class WindowsStrategy : AbstractStrategy
{
    private GlobalConfigInfo _configinfo = new();

    public override void Create(GlobalConfigInfo parameter) => _configinfo = parameter;

    public override async Task ExecuteAsync()
    {
        try
        {
            var status = ReportType.None;
            var patchPath = StorageManager.GetTempDirectory(Patchs);
            foreach (var version in _configinfo.UpdateVersions)
            {
                try
                {
                    var context = new PipelineContext();
                    //Common
                    context.Add("ZipFilePath",
                        Path.Combine(_configinfo.TempPath, $"{version.Name}{_configinfo.Format}"));
                    //hash middleware
                    context.Add("Hash", version.Hash);
                    //zip middleware
                    context.Add("Format", _configinfo.Format);
                    context.Add("Name", version.Name);
                    context.Add("Encoding", _configinfo.Encoding);
                    //patch middleware
                    context.Add("SourcePath", _configinfo.InstallPath);
                    context.Add("PatchPath", patchPath);

                    var pipelineBuilder = new PipelineBuilder(context)
                        .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
                        .UseMiddleware<CompressMiddleware>()
                        .UseMiddleware<HashMiddleware>();
                    await pipelineBuilder.Build();
                    status = ReportType.Success;
                }
                catch (Exception e)
                {
                    status = ReportType.Failure;
                    GeneralTracer.Error("The ExecuteAsync method in the GeneralUpdate.ClientCore.WindowsStrategy class throws an exception.", e);
                    EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
                }
                finally
                {
                    await VersionService.Report(_configinfo.ReportUrl
                        , version.RecordId
                        , status
                        , version.AppType
                        , _configinfo.Scheme
                        , _configinfo.Token);
                }
            }

            if (!string.IsNullOrEmpty(_configinfo.UpdateLogUrl))
            {
                OpenBrowser(_configinfo.UpdateLogUrl);
            }

            Clear(patchPath);
            Clear(_configinfo.TempPath);
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The ExecuteAsync method in the GeneralUpdate.ClientCore.WindowsStrategy class throws an exception." , e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
    }

    public override void StartApp()
    {
        try
        {
            Environments.SetEnvironmentVariable("ProcessInfo", _configinfo.ProcessInfo);
            var appPath = Path.Combine(_configinfo.InstallPath, _configinfo.AppName);
            if (File.Exists(appPath))
            {
                Environments.SetEnvironmentVariable("ProcessInfo", _configinfo.ProcessInfo);
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = appPath
                });
            }
        }
        catch (Exception e)
        {
            GeneralTracer.Error("The StartApp method in the GeneralUpdate.ClientCore.WindowsStrategy class throws an exception." , e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}