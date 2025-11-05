using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using GeneralUpdate.Common.Shared.Service;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategys
{
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
                            //Hash middleware
                            context.Add("Hash", version.Hash);
                            //Zip middleware
                            context.Add("Format", _configinfo.Format);
                            context.Add("Name", version.Name);
                            context.Add("Encoding", _configinfo.Encoding);
                            //Patch middleware
                            context.Add("SourcePath", _configinfo.InstallPath);
                            context.Add("PatchPath", patchPath);
                            context.Add("PatchEnabled", _configinfo.PatchEnabled);
                            //Driver middleware
                            if (_configinfo.DriveEnabled == true)
                            {
                                context.Add("DriverOutPut", StorageManager.GetTempDirectory("DriverOutPut"));
                                context.Add("FieldMappings", _configinfo.FieldMappings);
                            }

                            var pipelineBuilder = new PipelineBuilder(context)
                                .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
                                .UseMiddleware<CompressMiddleware>()
                                .UseMiddleware<HashMiddleware>()
                                .UseMiddlewareIf<DriverMiddleware>(_configinfo.DriveEnabled);
                            await pipelineBuilder.Build();
                            status = ReportType.Success;
                        }
                        catch (Exception e)
                        {
                            status = ReportType.Failure;
                            GeneralTracer.Error("The Execute method in the GeneralUpdate.Core.WindowsStrategy class throws an exception.", e);
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
                    StartApp();
                }
                catch (Exception e)
                {
                    GeneralTracer.Error("The Execute method in the GeneralUpdate.Core.WindowsStrategy class throws an exception.", e);
                    EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
                }
            });
        }

        public override void StartApp()
        {
            try
            {
                var mainAppPath = CheckPath(_configinfo.InstallPath, _configinfo.MainAppName);
                if (string.IsNullOrEmpty(mainAppPath))
                    throw new Exception($"Can't find the app {mainAppPath}!");
                
                Process.Start(mainAppPath);

                var bowlAppPath = CheckPath(_configinfo.InstallPath, _configinfo.Bowl);
                if (!string.IsNullOrEmpty(bowlAppPath))
                {
                    Process.Start(bowlAppPath);
                }
            }
            catch (Exception e)
            {
                GeneralTracer.Error("The StartApp method in the GeneralUpdate.Core.WindowsStrategy class throws an exception.", e);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
            }
            finally
            {
                GeneralTracer.Dispose();
                Process.GetCurrentProcess().Kill();
            }
        }

        private string CheckPath(string path,string name)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name)) return string.Empty;
            var tempPath = Path.Combine(path, name);
            return File.Exists(tempPath) ? tempPath : string.Empty;
        }
    }
}