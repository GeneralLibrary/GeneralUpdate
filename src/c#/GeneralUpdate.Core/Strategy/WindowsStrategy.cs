using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// Update policy based on the Windows platform.
    /// </summary>
    public class WindowsStrategy : AbstractStrategy
    {
        protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
        {
            GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}");
            return base.CreatePipelineContext(version, patchPath);
        }

        protected override PipelineBuilder BuildPipeline(PipelineContext context)
        {
            GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}");
            var builder = new PipelineBuilder(context)
                .UseMiddleware<HashMiddleware>()
                .UseMiddleware<CompressMiddleware>()
                .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled);
            return builder;
        }

        protected override async Task OnExecuteCompleteAsync()
        {
            GeneralTracer.Info("GeneralUpdate.Core.WindowsStrategy.OnExecuteComplete: all versions processed, starting application.");
            await StartAppAsync();
        }

        public override async Task StartAppAsync()
        {
            try
            {
                var mainAppPath = CheckPath(_configinfo.InstallPath, _configinfo.MainAppName);
                if (string.IsNullOrEmpty(mainAppPath))
                    throw new Exception($"Can't find the app {mainAppPath}!");

                GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.StartApp: launching main app={mainAppPath}");
                Process.Start(mainAppPath);
                GeneralTracer.Info("GeneralUpdate.Core.WindowsStrategy.StartApp: main app launched successfully.");

                var bowlAppPath = CheckPath(_configinfo.InstallPath, _configinfo.Bowl);
                if (!string.IsNullOrEmpty(bowlAppPath))
                {
                    GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.StartApp: launching Bowl process={bowlAppPath}");
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
                GeneralTracer.Info("GeneralUpdate.Core.WindowsStrategy.StartApp: releasing tracer and terminating updater process.");
                GeneralTracer.Dispose();
                await GracefulExit.CurrentProcessAsync();
            }
        }
    }
}
