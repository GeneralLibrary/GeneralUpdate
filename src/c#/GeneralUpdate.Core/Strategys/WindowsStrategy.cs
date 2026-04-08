using System;
using System.Diagnostics;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategys
{
    /// <summary>
    /// Update policy based on the Windows platform.
    /// </summary>
    public class WindowsStrategy : AbstractStrategy
    {
        protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
        {
            GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}, driveEnabled={_configinfo.DriveEnabled}");
            var context = base.CreatePipelineContext(version, patchPath);
            
            // Driver middleware (Windows-specific)
            if (_configinfo.DriveEnabled == true)
            {
                context.Add("DriverDirectory", _configinfo.DriverDirectory);
                GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.CreatePipelineContext: driver update enabled, DriverDirectory={_configinfo.DriverDirectory}");
            }
            
            return context;
        }

        protected override PipelineBuilder BuildPipeline(PipelineContext context)
        {
            GeneralTracer.Info($"GeneralUpdate.Core.WindowsStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}, DriveEnabled={_configinfo.DriveEnabled}");
            var builder = new PipelineBuilder(context)
                .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
                .UseMiddleware<CompressMiddleware>()
                .UseMiddleware<HashMiddleware>();
#if NET10_0_OR_GREATER
            builder.UseMiddlewareIf<DrivelutionMiddleware>(_configinfo.DriveEnabled == true);
#endif
            return builder;
        }

        protected override void OnExecuteComplete()
        {
            GeneralTracer.Info("GeneralUpdate.Core.WindowsStrategy.OnExecuteComplete: all versions processed, starting application.");
            StartApp();
        }

        public override void StartApp()
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
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}