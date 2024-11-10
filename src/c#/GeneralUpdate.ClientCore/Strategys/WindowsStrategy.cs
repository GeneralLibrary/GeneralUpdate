using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Pipeline;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.ClientCore.Strategys;

/// <summary>
/// Update policy based on the Windows platform.
/// </summary>
public class WindowsStrategy : AbstractStrategy
{
    private GlobalConfigInfo _configinfo = new();

    public override void Create(GlobalConfigInfo parameter) => _configinfo = parameter;

    public override void Execute()
    {
        Task.Run(async () =>
        {
            try
            {
                var status = 0;
                var patchPath = GeneralFileManager.GetTempDirectory(PATCHS);
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
                        context.Add("BlackFiles", BlackListManager.Instance.BlackFiles);
                        context.Add("BlackFileFormats", BlackListManager.Instance.BlackFileFormats);

                        var pipelineBuilder = new PipelineBuilder(context)
                            .UseMiddleware<PatchMiddleware>()
                            .UseMiddleware<ZipMiddleware>()
                            .UseMiddleware<HashMiddleware>();
                        await pipelineBuilder.Build();
                        status = 2;
                    }
                    catch (Exception e)
                    {
                        status = 3;
                        EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
                    }
                    finally
                    {
                        await VersionService.Report(_configinfo.ReportUrl, version.RecordId, status, version.AppType);
                    }
                }

                if (!string.IsNullOrEmpty(_configinfo.UpdateLogUrl))
                {
                    OpenBrowser(_configinfo.UpdateLogUrl);
                }

                Clear(patchPath);
                Clear(_configinfo.TempPath);
                StartApp(_configinfo.AppName);
            }
            catch (Exception e)
            {
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
            }
        });
    }

    public override void StartApp(string appName)
    {
        var path = Path.Combine(_configinfo.InstallPath, appName);
        Environment.SetEnvironmentVariable("ProcessInfo", _configinfo.ProcessInfo, EnvironmentVariableTarget.User);
        Process.Start(path);
    }
}